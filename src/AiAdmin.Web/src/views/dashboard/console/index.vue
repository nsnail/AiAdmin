<!-- 运营大屏：卡片以组件形式注册，支持拖拽排序、显隐和宽度自定义，布局按用户保存在浏览器本地 -->
<template>
    <div class="dashboard-page">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div class="text-xs text-g-500">{{ updatedText }}</div>
            <div class="flex flex-wrap items-center gap-2">
                <ElSelect v-model="days" @change="loadData" class="w-32!">
                    <ElOption v-for="item in dayOptions" :key="item" :label="t('dashboard.lastDays', { days: item })" :value="item" />
                </ElSelect>
                <ElButton :loading="loading" @click="loadData">
                    <ArtSvgIcon class="mr-1" icon="ri:refresh-line" />{{ t('dashboard.refresh') }}
                </ElButton>
                <ElButton v-if="!editing" @click="editing = true">
                    <ArtSvgIcon class="mr-1" icon="ri:layout-grid-line" />{{ t('dashboard.customize') }}
                </ElButton>
                <template v-else>
                    <ElButton @click="resetLayout">{{ t('dashboard.resetLayout') }}</ElButton>
                    <ElButton @click="editing = false" type="primary">{{ t('dashboard.done') }}</ElButton>
                </template>
            </div>
        </div>

        <ElAlert v-if="editing" :closable="false" :title="t('dashboard.editTip')" class="mb-4" show-icon type="info" />

        <VueDraggable v-model="visibleItems" :animation="200" :disabled="!editing" class="grid grid-cols-12 gap-4" handle=".drag-handle">
            <div
                v-for="item in visibleItems"
                :class="['widget-cell relative', editing ? 'is-editing' : '']"
                :key="item.id"
                :style="{ '--span': item.span, height: definitionMap[item.id].height }">
                <component :is="definitionMap[item.id].component" />
                <div v-if="editing" class="widget-toolbar">
                    <ArtSvgIcon class="drag-handle cursor-move text-lg" icon="ri:drag-move-2-line" />
                    <span class="mx-2 truncate text-xs">{{ t(definitionMap[item.id].titleKey) }}</span>
                    <ElSelect v-model="item.span" @change="saveLayout" class="w-20!" size="small">
                        <ElOption v-for="span in widgetSpanOptions" :key="span" :label="spanLabel(span)" :value="span" />
                    </ElSelect>
                    <ElButton @click="setVisible(item.id, false)" class="ml-1" link size="small" type="danger">
                        <ArtSvgIcon icon="ri:eye-off-line" />
                    </ElButton>
                </div>
            </div>
        </VueDraggable>

        <div v-if="editing && hiddenItems.length" class="art-card mt-4 p-4">
            <div class="mb-2 text-sm font-medium text-g-900">{{ t('dashboard.hiddenWidgets') }}</div>
            <div class="flex flex-wrap gap-2">
                <ElButton v-for="item in hiddenItems" :key="item.id" @click="setVisible(item.id, true)" size="small">
                    <ArtSvgIcon class="mr-1" icon="ri:add-line" />{{ t(definitionMap[item.id].titleKey) }}
                </ElButton>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { computed, onMounted, provide, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { VueDraggable } from 'vue-draggable-plus'
import { fetchGetDashboardOverview, type DashboardOverview } from '@/api/dashboard'
import { useUserStore } from '@/store/modules/user'
import { dashboardContextKey } from './widgets/context'
import { dashboardWidgets, widgetSpanOptions, type DashboardWidgetDefinition } from './widgets/registry'

defineOptions({ name: 'Console' })

/** 单个卡片的布局状态 */
interface LayoutItem {
    id: string
    span: number
    visible: boolean
}

const { t } = useI18n()
const userStore = useUserStore()

const dayOptions = [7, 14, 30]
const days = ref(14)
const loading = ref(false)
const editing = ref(false)
const overview = ref<DashboardOverview | null>(null)

provide(dashboardContextKey, { overview, loading, days })

const definitionMap = Object.fromEntries(dashboardWidgets.map((x) => [x.id, x])) as Record<string, DashboardWidgetDefinition>

/** 布局存储键，按用户隔离 */
const storageKey = computed(() => `dashboard-layout:${userStore.info?.userId ?? 'anonymous'}`)

/**
 * 生成默认布局
 * @returns 默认布局列表
 */
function defaultLayout(): LayoutItem[] {
    return dashboardWidgets.map((x) => ({ id: x.id, span: x.defaultSpan, visible: x.defaultVisible }))
}

/**
 * 读取已保存布局，并与当前注册的卡片合并：已下线卡片丢弃，新增卡片按默认值追加
 * @returns 合并后的布局
 */
function loadLayout(): LayoutItem[] {
    try {
        const raw = localStorage.getItem(storageKey.value)
        const saved = raw ? (JSON.parse(raw) as LayoutItem[]) : []
        const valid = saved.filter((x) => definitionMap[x.id] && widgetSpanOptions.includes(x.span))
        const known = new Set(valid.map((x) => x.id))
        const added = defaultLayout().filter((x) => !known.has(x.id))
        return [...valid, ...added]
    } catch {
        return defaultLayout()
    }
}

const layout = ref<LayoutItem[]>(loadLayout())

/** 保存布局到浏览器本地，存储不可用时忽略 */
function saveLayout() {
    try {
        localStorage.setItem(storageKey.value, JSON.stringify(layout.value))
    } catch {
        // 隐私模式或存储被禁用时仅本次会话生效
    }
}

watch(layout, saveLayout, { deep: true })
watch(storageKey, () => (layout.value = loadLayout()))

/**
 * 可见卡片列表：拖拽组件只操作可见项，写回时按原位置把新顺序填回可见槽位，隐藏卡片位置保持不变
 */
const visibleItems = computed<LayoutItem[]>({
    get: () => layout.value.filter((x) => x.visible),
    set: (sorted) => {
        let cursor = 0
        layout.value = layout.value.map((x) => (x.visible ? sorted[cursor++] : x))
    },
})
const hiddenItems = computed(() => layout.value.filter((x) => !x.visible))

/**
 * 设置卡片显隐
 * @param id 卡片标识
 * @param visible 是否显示
 */
function setVisible(id: string, visible: boolean) {
    const target = layout.value.find((x) => x.id === id)
    if (target) {
        target.visible = visible
    }
}

/** 恢复默认布局 */
function resetLayout() {
    layout.value = defaultLayout()
}

/**
 * 栅格宽度的展示文本
 * @param span 栅格数
 */
function spanLabel(span: number) {
    return `${Math.round((span / 12) * 100)}%`
}

const updatedText = computed(() => (overview.value ? t('dashboard.updatedAt', { time: new Date(overview.value.generatedAt).toLocaleString() }) : ''))

/** 加载仪表盘总览数据 */
async function loadData() {
    loading.value = true
    try {
        overview.value = await fetchGetDashboardOverview(days.value)
    } finally {
        loading.value = false
    }
}

onMounted(loadData)
</script>

<style lang="scss" scoped>
.widget-cell {
    grid-column: span var(--span) / span var(--span);
    min-width: 0;

    &.is-editing {
        outline: 1px dashed var(--el-color-primary);
        outline-offset: 2px;
        border-radius: 8px;
    }
}

.widget-toolbar {
    position: absolute;
    top: 6px;
    right: 6px;
    z-index: 10;
    display: flex;
    align-items: center;
    max-width: calc(100% - 12px);
    padding: 2px 6px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color);
    border-radius: 6px;
    box-shadow: var(--el-box-shadow-light);
}

@media (max-width: 1024px) {
    .widget-cell {
        grid-column: span 12 / span 12;
    }
}
</style>
<!-- 常用功能：用户把常用页面保存为磁贴，点击直达；磁贴按用户保存在浏览器本地，只能添加当前账号有权限访问的页面 -->
<template>
    <div class="tiles-page art-full-height">
        <ElCard class="art-table-card">
            <template #header>
                <div class="flex flex-wrap items-center justify-between gap-3">
                    <div class="text-xs text-g-500">{{ t('quickTiles.tip') }}</div>
                    <div class="flex flex-wrap items-center gap-2">
                        <ElButton v-if="!editing" @click="editing = true">
                            <ArtSvgIcon class="mr-1" icon="ri:edit-line" />{{ t('quickTiles.edit') }}
                        </ElButton>
                        <ElButton v-else @click="editing = false" type="primary">{{ t('quickTiles.done') }}</ElButton>
                        <ElButton @click="openPicker" type="primary">
                            <ArtSvgIcon class="mr-1" icon="ri:add-line" />{{ t('quickTiles.add') }}
                        </ElButton>
                    </div>
                </div>
            </template>

            <ElAlert v-if="editing" :closable="false" :title="t('quickTiles.editTip')" class="mb-4" show-icon type="info" />

            <ElEmpty v-if="tiles.length === 0" :description="t('quickTiles.empty')">
                <ElButton @click="openPicker" type="primary">{{ t('quickTiles.add') }}</ElButton>
            </ElEmpty>

            <VueDraggable v-else v-model="tiles" :animation="200" :disabled="!editing" class="tile-grid">
                <div
                    v-for="tile in tileItems"
                    :aria-label="tile.label"
                    :class="['tile-card', editing ? 'is-editing' : '']"
                    :key="tile.path"
                    @click="open(tile.path)"
                    @keydown.enter="open(tile.path)"
                    role="button"
                    tabindex="0">
                    <ArtSvgIcon :icon="tile.icon || 'ri:apps-2-line'" class="tile-icon text-theme" />
                    <div class="tile-title">{{ tile.label }}</div>
                    <div class="tile-group">{{ tile.group }}</div>
                    <ElButton
                        v-if="editing"
                        :aria-label="t('quickTiles.remove')"
                        @click.stop="remove(tile.path)"
                        circle
                        class="tile-remove"
                        size="small"
                        type="danger">
                        <ArtSvgIcon icon="ri:close-line" />
                    </ElButton>
                </div>
            </VueDraggable>
        </ElCard>

        <ElDialog v-model="pickerVisible" :title="t('quickTiles.pickerTitle')" destroy-on-close width="560px">
            <ElInput v-model="keyword" :placeholder="t('quickTiles.search')" clearable />
            <div class="picker-list">
                <ElEmpty v-if="pickerGroups.length === 0" :description="t('quickTiles.noCandidates')" />
                <div v-for="group in pickerGroups" :key="group.title" class="picker-group">
                    <div class="picker-group-title">{{ group.title }}</div>
                    <ElCheckboxGroup v-model="draft">
                        <ElCheckbox v-for="item in group.items" :disabled="isDraftFull(item.path)" :key="item.path" :value="item.path">
                            <ArtSvgIcon :icon="item.icon || 'ri:apps-2-line'" class="mr-1" />{{ item.label }}
                        </ElCheckbox>
                    </ElCheckboxGroup>
                </div>
            </div>
            <template #footer>
                <span class="mr-3 text-xs text-g-500">{{ t('quickTiles.count', { count: draft.length, max: MAX_QUICK_TILES }) }}</span>
                <ElButton @click="pickerVisible = false">{{ t('quickTiles.cancel') }}</ElButton>
                <ElButton @click="confirmPicker" type="primary">{{ t('quickTiles.confirm') }}</ElButton>
            </template>
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { VueDraggable } from 'vue-draggable-plus'
import { useMenuStore } from '@/store/modules/menu'
import { useUserStore } from '@/store/modules/user'
import { formatMenuTitle } from '@/utils'
import { collectTileCandidates, MAX_QUICK_TILES, sanitizeTiles } from '@/utils/navigation/quickTiles'

defineOptions({ name: 'QuickTiles' })

const STORAGE_KEY_PREFIX = 'quick-tiles'

const { t } = useI18n()
const router = useRouter()
const menuStore = useMenuStore()
const userStore = useUserStore()

const editing = ref(false)
const pickerVisible = ref(false)
const keyword = ref('')
/** 选择框中的临时勾选，点击确定后才写入磁贴 */
const draft = ref<string[]>([])
const tiles = ref<string[]>([])

/** 当前账号可访问的全部页面，菜单变化后自动刷新 */
const candidates = computed(() => collectTileCandidates(menuStore.menuList))
const candidateMap = computed(() => new Map(candidates.value.map((x) => [x.path, x])))

/** 按用户隔离的存储键，未登录时使用匿名标识 */
const storageKey = computed(() => `${STORAGE_KEY_PREFIX}:${userStore.info?.userId ?? 'anonymous'}`)

/** 渲染用的磁贴数据，标题在这里翻译，切换语言后自动更新 */
const tileItems = computed(() =>
    tiles.value.flatMap((path) => {
        const item = candidateMap.value.get(path)
        return item ? [{ path, icon: item.icon, label: formatMenuTitle(item.title), group: formatMenuTitle(item.groupTitle) }] : []
    }),
)

/** 选择框里按目录分组并按关键字过滤后的候选 */
const pickerGroups = computed(() => {
    const word = keyword.value.trim().toLowerCase()
    const groups = new Map<string, { title: string; items: { path: string; icon: string; label: string }[] }>()
    for (const item of candidates.value) {
        const label = formatMenuTitle(item.title)
        const title = formatMenuTitle(item.groupTitle)
        if (word && !label.toLowerCase().includes(word) && !title.toLowerCase().includes(word)) continue
        const group = groups.get(title) ?? { title, items: [] }
        group.items.push({ path: item.path, icon: item.icon, label })
        groups.set(title, group)
    }
    return [...groups.values()]
})

/**
 * 从本地存储读取磁贴，存储不可用或内容损坏时视为空
 * @returns 已保存的页面路径
 */
const load = (): string[] => {
    try {
        const raw = localStorage.getItem(storageKey.value)
        return raw ? sanitizeTiles(JSON.parse(raw), candidates.value) : []
    } catch {
        return []
    }
}

/**
 * 把磁贴写入本地存储，存储不可用时仅本次会话生效
 */
const save = (): void => {
    try {
        localStorage.setItem(storageKey.value, JSON.stringify(tiles.value))
    } catch {
        // 隐私模式或存储被禁用时忽略
    }
}

// 菜单在路由初始化后才有数据，用户切换账号时也要重新读取；已失去权限的页面在读取时被清洗掉
watch([storageKey, candidates], () => (tiles.value = load()), { immediate: true })
watch(tiles, save, { deep: true })

/**
 * 判断勾选数量是否已达上限，已勾选的项仍允许取消
 * @param path 页面路径
 * @returns 达到上限且该项未勾选时为 true
 */
const isDraftFull = (path: string): boolean => draft.value.length >= MAX_QUICK_TILES && !draft.value.includes(path)

/**
 * 打开添加对话框，以当前磁贴作为初始勾选
 */
const openPicker = (): void => {
    draft.value = [...tiles.value]
    keyword.value = ''
    pickerVisible.value = true
}

/**
 * 确认选择：已有磁贴保持原有顺序，新勾选的追加到末尾，取消勾选的移除
 */
const confirmPicker = (): void => {
    const kept = tiles.value.filter((path) => draft.value.includes(path))
    const added = draft.value.filter((path) => !kept.includes(path))
    tiles.value = [...kept, ...added]
    pickerVisible.value = false
}

/**
 * 移除一个磁贴
 * @param path 页面路径
 */
const remove = (path: string): void => {
    tiles.value = tiles.value.filter((x) => x !== path)
}

/**
 * 直达页面，编辑状态下点击只用于拖拽排序，不跳转
 * @param path 页面路径
 */
const open = (path: string): void => {
    if (editing.value) return
    void router.push(path)
}
</script>

<style scoped>
.tile-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(168px, 1fr));
    gap: 16px;
}

.tile-card {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: 6px;
    align-items: center;
    justify-content: center;
    min-height: 132px;
    padding: 16px;
    text-align: center;
    cursor: pointer;
    background: var(--default-box-color);
    border: 1px solid var(--art-card-border);
    border-radius: calc(var(--custom-radius) / 2 + 2px);
    transition: border-color 0.15s;
}

.tile-card:hover,
.tile-card:focus-visible {
    border-color: var(--el-color-primary);
    outline: none;
}

.tile-card.is-editing {
    border-style: dashed;
    cursor: grab;
}

.tile-icon {
    font-size: 32px;
}

.tile-title {
    font-size: 15px;
    font-weight: 500;
    line-height: 1.3;
    word-break: break-word;
}

.tile-group {
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.tile-remove {
    position: absolute;
    top: 6px;
    right: 6px;
}

.picker-list {
    max-height: 50vh;
    margin-top: 12px;
    overflow-y: auto;
}

.picker-group {
    margin-bottom: 12px;
}

.picker-group-title {
    margin-bottom: 6px;
    font-size: 13px;
    color: var(--el-text-color-secondary);
}
</style>
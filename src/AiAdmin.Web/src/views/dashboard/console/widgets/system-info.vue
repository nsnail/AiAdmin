<!-- 系统信息卡片：展示系统 logo、名称、版本号和最近一次版本更新内容，数据由后端程序集元数据与 CHANGELOG 提供 -->
<template>
    <div class="art-card flex h-full flex-col gap-3 px-5 py-4">
        <div class="flex items-center gap-4">
            <ArtLogo :size="48" class="shrink-0" />
            <div class="min-w-0 flex-1">
                <div class="truncate text-lg font-medium">{{ info?.name ?? AppConfig.systemInfo.name }}</div>
                <div class="mt-1 text-xs text-g-600">
                    {{ t('dashboard.widgets.systemInfo.version') }} {{ info ? `v${info.version}` : '-' }}
                    <span v-if="info?.releaseDate"> · {{ info.releaseDate }}</span>
                </div>
            </div>
        </div>
        <ElPopover :disabled="!overflowed" :width="360" placement="bottom" trigger="hover">
            <template #reference>
                <div class="min-h-0 flex-1 overflow-hidden border-t border-g-300 pt-3 text-xs" ref="notesRef">
                    <ReleaseNotes :info="info" />
                </div>
            </template>
            <div class="text-xs">
                <ReleaseNotes :info="info" />
            </div>
        </ElPopover>
    </div>
</template>

<script lang="ts" setup>
import { useResizeObserver } from '@vueuse/core'
import { nextTick, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchGetDashboardSystemInfo, type DashboardSystemInfo } from '@/api/dashboard'
import AppConfig from '@/config'
import ReleaseNotes from './release-notes.vue'

defineOptions({ name: 'DashboardSystemInfo' })

const { t } = useI18n()
/** 后端返回的系统信息 */
const info = ref<DashboardSystemInfo | null>(null)
/** 更新内容容器 */
const notesRef = ref<HTMLElement>()
/** 更新内容是否溢出容器，溢出时才启用气泡展示完整内容 */
const overflowed = ref(false)

/** 检测更新内容是否被容器裁剪 */
function checkOverflow() {
    const el = notesRef.value
    overflowed.value = !!el && el.scrollHeight > el.clientHeight + 1
}

useResizeObserver(notesRef, checkOverflow)
watch(info, () => nextTick(checkOverflow))

onMounted(async () => {
    try {
        info.value = await fetchGetDashboardSystemInfo()
    } catch {
        info.value = null
    }
})
</script>
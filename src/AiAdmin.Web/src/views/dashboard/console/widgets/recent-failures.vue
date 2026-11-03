<!-- 最近异常的作业执行记录 -->
<template>
    <WidgetCard :title="t('dashboard.widgets.recentFailures.title')" to="/system-management/scheduled-job">
        <div v-if="!rows.length" class="flex-cc h-full text-sm text-g-500">{{ t('dashboard.noFailures') }}</div>
        <ElScrollbar v-else v-loading="opening">
            <div
                v-for="item in rows"
                :class="['border-b border-g-200 py-2 text-sm last:border-0', canOpen ? 'cursor-pointer hover:bg-g-100' : '']"
                :key="item.id"
                @click="openDetail(item.id)">
                <div class="flex items-center justify-between">
                    <span class="truncate font-medium text-g-900">{{ item.jobName }}</span>
                    <span class="ml-3 shrink-0 text-xs text-g-500">{{ formatTime(item.startedAt) }}</span>
                </div>
                <p class="mt-1 truncate text-xs text-danger">
                    {{ t(`scheduledJob.status.${statusKey(item.status)}`) }}<template v-if="item.errorMessage"> · {{ item.errorMessage }}</template>
                </p>
            </div>
        </ElScrollbar>
    </WidgetCard>
    <ScheduledJobExecutionDialog v-model:visible="detailVisible" :execution="selectedExecution" />
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { fetchScheduledJobExecutions, type ScheduledJobExecution } from '@/api/system-manage'
import ScheduledJobExecutionDialog from '@/views/system/scheduled-job/modules/scheduled-job-execution-dialog.vue'
import { useDashboardContext, useWidgetLink } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRecentFailures' })

const { t } = useI18n()
const { overview, editing } = useDashboardContext()
const { linkable } = useWidgetLink(() => '/system-management/scheduled-job')
const rows = computed(() => overview.value?.recentFailures ?? [])
const formatTime = (value: string) => new Date(value).toLocaleString()
/** 详情弹窗是否显示 */
const detailVisible = ref(false)
/** 当前查看的执行记录 */
const selectedExecution = ref<ScheduledJobExecution>()
/** 是否正在加载详情，加载期间显示遮罩并忽略重复点击 */
const opening = ref(false)
/** 当前用户有计划作业页面权限（用于查询执行记录）且不在布局编辑模式时才可点击 */
const canOpen = computed(() => linkable() && !editing.value)

/**
 * 点击条目弹出执行记录详情，复用计划作业页的执行详情抽屉；列表只含摘要，详情按主键查完整记录
 * @param id 执行记录主键
 */
async function openDetail(id: string) {
    if (!canOpen.value || opening.value) return
    opening.value = true
    try {
        const page = await fetchScheduledJobExecutions(undefined, {
            current: 1,
            size: 1,
            dynamicFilter: { field: 'Id', operator: 'Equal', value: id },
        })
        const record = page.records?.[0]
        if (record) {
            selectedExecution.value = record
            detailVisible.value = true
        } else {
            ElMessage.warning(t('dashboard.executionNotFound'))
        }
    } finally {
        opening.value = false
    }
}
const statusKey = (status: number) => (status === 4 ? 'timeout' : 'failed')
</script>
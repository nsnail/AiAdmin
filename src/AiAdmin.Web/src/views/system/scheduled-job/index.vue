<template>
    <div class="art-full-height">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :default-filter="defaultFilter"
            :loading="loading"
            :pagination="pagination"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            resource="scheduled-job">
            <template #header-left>
                <ElButton v-ripple @click="openDialog()">{{ t('scheduledJob.actions.create') }}</ElButton>
            </template>
            <template #cronExpression="{ row }">
                <div class="cron-cell">
                    <code>{{ row.cronExpression }}</code>
                    <span class="cron-description">{{ describeCron(row.cronExpression) }}</span>
                </div>
            </template>
        </ArtTablePage>
        <ScheduledJobDialog v-model:visible="dialogVisible" :job-data="currentJob" :saving="saving" @submit="saveJob" />
        <ElDialog
            v-model="executionVisible"
            :title="t('scheduledJob.executionTitle', { name: executionJob?.name || t('scheduledJob.unknown') })"
            class="execution-dialog"
            destroy-on-close
            fullscreen>
            <ArtTablePage
                v-model:column-checks="executionColumnChecks"
                :columns="executionColumns"
                :data="executionData"
                :filter-fields="executionFilterFields"
                :filter-groups-fn="executionFilterGroups"
                :loading="executionLoading"
                :pagination="executionPagination"
                @filter-change="executionHandleFilterChange"
                @page-change="executionHandleCurrentChange"
                @refresh="executionRefreshData"
                @reset="executionResetSearchParams"
                @size-change="executionHandleSizeChange"
                @sort-change="executionHandleSortChange"
                resource="scheduled-job" />
            <template #footer
                ><ElButton @click="executionVisible = false">{{ t('scheduledJob.actions.close') }}</ElButton></template
            >
        </ElDialog>
        <ScheduledJobExecutionDialog v-model:visible="detailVisible" :execution="selectedExecution" />
    </div>
</template>

<script lang="ts" setup>
import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ArtButtonMore, { type ButtonMoreItem } from '@/components/core/forms/art-button-more/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchCreateScheduledJob,
    fetchDeleteScheduledJob,
    fetchGetScheduledJobs,
    fetchRunScheduledJob,
    fetchScheduledJobExecutionFilterFields,
    fetchScheduledJobExecutionFilterGroups,
    fetchScheduledJobExecutions,
    fetchUpdateScheduledJob,
    type SaveScheduledJob,
    type ScheduledJob,
    type ScheduledJobExecution,
    type ScheduledJobExecutionSearchParams,
    type ListFilterField,
    type DynamicFilter,
} from '@/api/system-manage'
import ScheduledJobDialog from './modules/scheduled-job-dialog.vue'
import ScheduledJobExecutionDialog from './modules/scheduled-job-execution-dialog.vue'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'ScheduledJobManagement' })
const { t, locale } = useI18n()
const dialogVisible = ref(false)
const saving = ref(false)
const currentJob = ref<ScheduledJob>()
const executionJob = ref<ScheduledJob>()
const executionVisible = ref(false)
const executionFilterFields = ref<ListFilterField[]>([])
const selectedExecution = ref<ScheduledJobExecution>()
const detailVisible = ref(false)
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const statusMap: Record<number, { key: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }> = {
    0: { key: 'waiting', type: 'info' },
    1: { key: 'running', type: 'primary' },
    2: { key: 'success', type: 'success' },
    3: { key: 'failed', type: 'danger' },
    4: { key: 'timeout', type: 'warning' },
}
const statusLabel = (status: number): string => t(`scheduledJob.status.${statusMap[status]?.key || 'unknown'}`)
const formatTime = (value: string | null): string => (value ? formatDateTime(value, locale.value) : '-')
const describeCron = (value?: string): string => {
    if (!value?.trim()) return '-'
    const parts = value.trim().split(/\s+/)
    if (parts.length === 5) parts.unshift('0')
    if (parts.length !== 6 || parts.some((part) => !/^[\d*/,-]+$/.test(part))) return t('cronEditor.description.invalid')
    const [second, minute, hour, day, month, week] = parts
    if (second === '*' && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everySecond')
    if (second === '0' && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everyMinute')
    if (second === '0' && minute === '0' && hour === '*' && day === '*' && month === '*' && week === '*') return t('cronEditor.description.hourly')
    const secondMatch = second.match(/^(?:\*|\d+)\/(\d+)$/)
    if (secondMatch && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everySeconds', { value: secondMatch[1] })
    const minuteMatch = minute.match(/^(?:\*|\d+)\/(\d+)$/)
    if (second === '0' && minuteMatch && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everyMinutes', { value: minuteMatch[1] })
    if (/^\d+$/.test(hour) && /^\d+$/.test(minute) && /^\d+$/.test(second) && day === '*' && month === '*' && week === '*') {
        return t('cronEditor.description.daily', {
            time: [hour, minute, second].map((item) => item.padStart(2, '0')).join(':'),
        })
    }
    return t('cronEditor.description.custom', { value: value.trim() })
}

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    refreshCreate,
    refreshUpdate,
    refreshRemove,
} = useTable({
    core: {
        apiFn: fetchGetScheduledJobs,
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter },
        columnsFactory: () => [
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 160,
                align: 'left',
                sortable: true,
                formatter: (row) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'name',
                queryField: 'Name',
                queryValueField: 'name',
                queryValueType: 'string',
                label: t('scheduledJob.fields.name'),
                minWidth: 150,
                sortable: true,
            },
            {
                prop: 'cronExpression',
                queryField: 'CronExpression',
                label: t('scheduledJob.fields.cronExpression'),
                minWidth: 180,
                sortable: true,
                useSlot: true,
            },
            {
                prop: 'requestMethod',
                queryField: 'RequestMethod',
                label: t('scheduledJob.fields.requestMethod'),
                width: 110,
                sortable: true,
                align: 'center',
                formatter: (row) => h(ElTag, { size: 'small', type: 'info' }, () => row.requestMethod),
            },
            {
                prop: 'requestUrl',
                queryField: 'RequestUrl',
                label: t('scheduledJob.fields.requestUrl'),
                minWidth: 260,
                sortable: true,
                showOverflowTooltip: true,
            },
            {
                prop: 'timeoutSeconds',
                queryField: 'TimeoutSeconds',
                queryValueType: 'number',
                label: t('scheduledJob.fields.timeoutSeconds'),
                width: 120,
                sortable: true,
                align: 'right',
            },
            {
                prop: 'isEnabled',
                queryField: 'IsEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 110,
                sortable: true,
                align: 'center',
                formatter: (row) => h(ArtEnabledSwitch, { modelValue: row.isEnabled, disabled: true }),
            },
            {
                prop: 'status',
                queryField: 'Status',
                queryValueType: 'number',
                label: t('scheduledJob.fields.status'),
                width: 120,
                sortable: true,
                align: 'center',
                formatter: (row) => {
                    const status = statusMap[row.status] || { key: 'unknown', type: 'info' as const }
                    return h(ElTag, { size: 'small', type: status.type }, () => statusLabel(row.status))
                },
            },
            {
                prop: 'lastTriggeredAt',
                queryField: 'LastTriggeredAt',
                queryValueType: 'date',
                label: t('scheduledJob.fields.lastTriggeredAt'),
                width: 180,
                sortable: true,
                formatter: (row) => formatTime(row.lastTriggeredAt),
            },
            {
                prop: 'lastFinishedAt',
                queryField: 'LastFinishedAt',
                queryValueType: 'date',
                label: t('scheduledJob.fields.lastFinishedAt'),
                width: 180,
                sortable: true,
                formatter: (row) => formatTime(row.lastFinishedAt),
            },
            {
                prop: 'lastError',
                queryField: 'LastError',
                label: t('scheduledJob.fields.lastError'),
                minWidth: 180,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row) => row.lastError || '-',
            },
            {
                prop: 'createdAt',
                queryField: 'CreatedAt',
                queryValueType: 'date',
                label: t('listFilter.common.createdAt'),
                width: 180,
                sortable: true,
                formatter: (row) => formatTime(row.createdAt),
            },
            {
                prop: 'operation',
                queryField: false,
                label: t('scheduledJob.actions.operation'),
                width: 70,
                fixed: 'right',
                formatter: (row) =>
                    h(ArtButtonMore, {
                        list: [
                            {
                                key: 'run',
                                label: t('scheduledJob.actions.run'),
                                icon: 'ri:play-circle-line',
                                disabled: row.status === 1,
                            },
                            { key: 'executions', label: t('scheduledJob.actions.executions'), icon: 'ri:history-line' },
                            { key: 'edit', label: t('scheduledJob.actions.edit'), icon: 'ri:edit-2-line' },
                            { key: 'delete', label: t('scheduledJob.actions.delete'), icon: 'ri:delete-bin-4-line', color: '#f56c6c' },
                        ],
                        onClick: (item: ButtonMoreItem) => handleAction(item, row),
                    }),
            },
        ],
    },
})

const executionTable = useTable({
    core: {
        apiFn: (params: ScheduledJobExecutionSearchParams) => fetchScheduledJobExecutions(executionJob.value?.id || '0', params),
        apiParams: { current: 1, size: 20 },
        immediate: false,
        columnsFactory: () => [
            {
                prop: 'startedAt',
                queryField: 'StartedAt',
                label: t('scheduledJob.executionFields.startedAt'),
                width: 190,
                sortable: true,
                formatter: (row) => formatTime(row.startedAt),
            },
            {
                prop: 'requestMethod',
                queryField: 'RequestMethod',
                label: t('scheduledJob.executionFields.requestMethod'),
                width: 90,
                sortable: true,
            },
            {
                prop: 'requestUrl',
                queryField: 'RequestUrl',
                label: t('scheduledJob.executionFields.requestUrl'),
                minWidth: 240,
                sortable: true,
                showOverflowTooltip: true,
            },
            {
                prop: 'responseStatusCode',
                queryField: 'ResponseStatusCode',
                queryValueType: 'number',
                label: t('scheduledJob.executionFields.responseStatusCode'),
                width: 130,
                sortable: true,
                align: 'right',
                formatter: (row) => row.responseStatusCode ?? '-',
            },
            {
                prop: 'status',
                queryField: 'Status',
                queryValueType: 'number',
                label: t('scheduledJob.executionFields.status'),
                width: 130,
                sortable: true,
                formatter: (row) => h(ElTag, { size: 'small', type: statusMap[row.status]?.type || 'info' }, () => statusLabel(row.status)),
            },
            {
                prop: 'errorMessage',
                queryField: 'ErrorMessage',
                label: t('scheduledJob.executionFields.errorMessage'),
                minWidth: 180,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row) => row.errorMessage || '-',
            },
            {
                prop: 'operation',
                queryField: false,
                label: t('scheduledJob.detail.operation'),
                width: 70,
                fixed: 'right',
                formatter: (row) =>
                    h(ArtButtonTable, {
                        type: 'view',
                        title: t('scheduledJob.executionDetail.title'),
                        onClick: () => showExecutionDetail(row),
                    }),
            },
        ],
    },
})
const {
    columns: executionColumns,
    columnChecks: executionColumnChecks,
    data: executionData,
    loading: executionLoading,
    pagination: executionPagination,
    getData: executionGetData,
    replaceSearchParams: executionReplaceSearchParams,
    resetSearchParams: executionResetSearchParams,
    handleSizeChange: executionHandleSizeChange,
    handleCurrentChange: executionHandleCurrentChange,
    handleSortChange: executionHandleSortChange,
    refreshData: executionRefreshData,
} = executionTable

const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
const openDialog = (job?: ScheduledJob): void => {
    currentJob.value = job
    dialogVisible.value = true
}
const saveJob = async (form: SaveScheduledJob): Promise<void> => {
    if (saving.value) return
    saving.value = true
    try {
        if (currentJob.value) {
            await fetchUpdateScheduledJob(currentJob.value.id, form)
            await refreshUpdate()
        } else {
            await fetchCreateScheduledJob(form)
            await refreshCreate()
        }
        dialogVisible.value = false
        ElMessage.success('作业已保存')
    } finally {
        saving.value = false
    }
}
const executionFilterGroups = (dynamicFilter?: DynamicFilter) => fetchScheduledJobExecutionFilterGroups(executionJob.value?.id || '0', dynamicFilter)
const executionHandleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    executionReplaceSearchParams({ dynamicFilter })
    await executionGetData()
}
const handleAction = async (item: ButtonMoreItem, job: ScheduledJob): Promise<void> => {
    if (item.key === 'edit') {
        openDialog(job)
        return
    }
    if (item.key === 'run') {
        await fetchRunScheduledJob(job.id)
        ElMessage.success('作业已加入执行队列')
        await refreshUpdate()
        return
    }
    if (item.key === 'executions') {
        executionJob.value = job
        executionFilterFields.value = await fetchScheduledJobExecutionFilterFields(job.id)
        executionReplaceSearchParams({ current: 1, size: 20, dynamicFilter: undefined })
        executionVisible.value = true
        await executionGetData()
        return
    }
    await ElMessageBox.confirm(`确定删除作业“${job.name}”吗？`, '删除确认', { type: 'warning' })
    await fetchDeleteScheduledJob(job.id)
    await refreshRemove()
}
const showExecutionDetail = (execution: ScheduledJobExecution): void => {
    selectedExecution.value = execution
    detailVisible.value = true
}
</script>

<style scoped>
.cron-cell {
    display: flex;
    flex-direction: column;
    gap: 3px;
    line-height: 1.35;
}
.cron-description {
    color: var(--el-text-color-secondary);
    font-size: 12px;
}
.execution-page {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
}
.execution-page :deep(.art-table-card .el-card__body) {
    display: flex;
    flex-direction: column;
    min-height: 0;
}
.execution-table :deep(.el-table th .cell) {
    white-space: nowrap;
}
.execution-pagination {
    display: flex;
    justify-content: flex-end;
    margin-top: 16px;
}
:global(.execution-dialog.el-dialog) {
    display: flex;
    flex-direction: column;
}
:global(.execution-dialog.el-dialog .el-dialog__body) {
    flex: 1;
    min-height: 0;
    overflow: hidden;
}
</style>
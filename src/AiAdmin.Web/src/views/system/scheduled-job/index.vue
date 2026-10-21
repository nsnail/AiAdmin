<template>
    <div class="art-full-height flex flex-col">
        <ElTabs v-model="activeTab" class="scheduled-job-tabs" type="card">
            <ElTabPane :label="t('scheduledJob.tabs.jobs')" name="jobs" />
            <ElTabPane :label="t('scheduledJob.tabs.allExecutions')" lazy name="executions" />
        </ElTabs>
        <ScheduledJobExecutionTable v-if="activeTab === 'executions'" class="min-h-0 flex-1" />
        <ArtTablePage
            v-model:column-checks="columnChecks"
            v-show="activeTab === 'jobs'"
            :columns="columns"
            :data="data"
            :default-filter="defaultFilter"
            :loading="loading"
            :pagination="pagination"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @selection-change="selectedRows = $event"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            ref="scheduledJobTablePage"
            resource="scheduled-job">
            <template #header-left>
                <ElButton v-ripple :disabled="selectedRows.length === 0" @click="openRemarkDialog">{{
                    t('scheduledJob.actions.batchRemark')
                }}</ElButton>
                <ElButton v-ripple @click="openDialog()">{{ t('scheduledJob.actions.create') }}</ElButton>
            </template>
        </ArtTablePage>
        <ElDialog v-model="remarkDialogVisible" :title="t('scheduledJob.actions.batchRemark')" width="420px">
            <ElInput v-model.trim="batchRemark" :placeholder="t('scheduledJob.placeholder.remark')" maxlength="500" show-word-limit type="textarea" />
            <template #footer>
                <ElButton @click="remarkDialogVisible = false">{{ t('scheduledJob.actions.close') }}</ElButton>
                <ElButton :loading="batchRemarkSaving" @click="saveBatchRemark" type="primary">{{ t('scheduledJob.actions.save') }}</ElButton>
            </template>
        </ElDialog>
        <ScheduledJobDialog v-model:visible="dialogVisible" :job-data="currentJob" :saving="saving" @submit="saveJob" />
        <ElDialog
            v-model="executionVisible"
            :title="t('scheduledJob.executionTitle', { name: executionJob?.name || t('scheduledJob.unknown') })"
            class="execution-dialog"
            destroy-on-close
            fullscreen>
            <ScheduledJobExecutionTable :job-id="executionJob?.id" />
            <template #footer
                ><ElButton @click="executionVisible = false">{{ t('scheduledJob.actions.close') }}</ElButton></template
            >
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { h } from 'vue'
import ArtButtonMore, { type ButtonMoreItem } from '@/components/core/forms/art-button-more/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchCreateScheduledJob,
    fetchBatchUpdateScheduledJobRemark,
    fetchDeleteScheduledJob,
    fetchCopyScheduledJob,
    fetchGetScheduledJobs,
    fetchRunScheduledJob,
    fetchUpdateScheduledJob,
    type SaveScheduledJob,
    type ScheduledJob,
    type DynamicFilter,
} from '@/api/system-manage'
import ScheduledJobDialog from './modules/scheduled-job-dialog.vue'
import ScheduledJobExecutionTable from './modules/scheduled-job-execution-table.vue'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'ScheduledJobManagement' })
const { t, locale } = useI18n()
const dialogVisible = ref(false)
const saving = ref(false)
const currentJob = ref<ScheduledJob>()
const executionJob = ref<ScheduledJob>()
const executionVisible = ref(false)
const scheduledJobTablePage = ref<{ refreshFilterGroups: (filter?: DynamicFilter) => Promise<void> }>()
const activeTab = ref('jobs')
const selectedRows = ref<ScheduledJob[]>([])
const remarkDialogVisible = ref(false)
const batchRemark = ref('')
const batchRemarkSaving = ref(false)
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const statusMap: Record<number, { key: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }> = {
    0: { key: 'waiting', type: 'info' },
    1: { key: 'running', type: 'primary' },
    2: { key: 'success', type: 'success' },
    3: { key: 'failed', type: 'danger' },
    4: { key: 'timeout', type: 'warning' },
}
const statusLabel = (status: number): string => t(`scheduledJob.status.${statusMap[status]?.key || 'unknown'}`)
const requestMethodTagType = (method: string): 'info' | 'primary' | 'success' | 'danger' | 'warning' => {
    return (
        {
            GET: 'success',
            POST: 'primary',
            PUT: 'warning',
            PATCH: 'warning',
            DELETE: 'danger',
        }[method.toUpperCase() as 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'] || 'info'
    )
}
const getNextExecution = (cron: string): string => {
    const fields = cron.trim().split(/\s+/)
    if (fields.length !== 5 && fields.length !== 6) return '-'
    const ranges = fields.length === 6 ? [60, 60, 24, 32, 13, 7] : [60, 24, 32, 13, 7]
    const matches = (value: number, expression: string, max: number): boolean =>
        expression.split(',').some((part) => {
            const [base, stepText] = part.split('/')
            const step = stepText ? Number(stepText) : 1
            if (!Number.isInteger(step) || step < 1) return false
            const [startText, endText] = base === '*' ? ['0', String(max - 1)] : base.split('-')
            const start = Number(startText)
            const end = Number(endText || start)
            return Number.isInteger(start) && Number.isInteger(end) && value >= start && value <= end && (value - start) % step === 0
        })
    const start = new Date()
    start.setMilliseconds(0)
    for (let offset = 1; offset <= 366 * 24 * 60 * 60; offset += fields.length === 6 ? 1 : 60) {
        const candidate = new Date(start.getTime() + offset * 1000)
        const values =
            fields.length === 6
                ? [
                      candidate.getSeconds(),
                      candidate.getMinutes(),
                      candidate.getHours(),
                      candidate.getDate(),
                      candidate.getMonth() + 1,
                      candidate.getDay(),
                  ]
                : [candidate.getMinutes(), candidate.getHours(), candidate.getDate(), candidate.getMonth() + 1, candidate.getDay()]
        if (values.every((value, index) => matches(value, fields[index], ranges[index]))) return formatTime(candidate.toISOString())
    }
    return '-'
}
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
    const hourStepMatch = hour.match(/^\*\/(\d+)$/)
    if (second === '0' && minute === '0' && hourStepMatch && day === '*' && month === '*' && week === '*') {
        return t('cronEditor.description.everyHours', { value: hourStepMatch[1] })
    }
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
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter, sortField: 'executionDuration', sortOrder: 'desc' },
        columnsFactory: () => [
            { type: 'selection', width: 48 },
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
                prop: 'jobInfo',
                queryField: 'Name',
                queryValueField: 'name',
                queryValueType: 'string',
                label: t('scheduledJob.fields.name'),
                minWidth: 260,
                sortable: true,
                formatter: (row) =>
                    h(
                        'div',
                        {
                            class: 'job-info',
                            style: { display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: '0', lineHeight: '1.1' },
                        },
                        [
                            h(
                                'span',
                                {
                                    class: 'job-name',
                                    'data-query-field': 'Name',
                                    'data-query-label': t('scheduledJob.fields.name'),
                                    'data-query-value': row.name,
                                    'data-query-value-type': 'string',
                                },
                                row.name,
                            ),
                            h(
                                'span',
                                {
                                    'data-query-field': 'IsEnabled',
                                    'data-query-label': t('listFilter.common.status'),
                                    'data-query-value': row.isEnabled,
                                    'data-query-value-type': 'boolean',
                                },
                                [
                                    h(ArtEnabledSwitch, {
                                        id: String(row.id),
                                        resource: 'scheduled-job',
                                        modelValue: row.isEnabled,
                                        'onUpdate:modelValue': async () => {
                                            await refreshData()
                                            await scheduledJobTablePage.value?.refreshFilterGroups(defaultFilter)
                                        },
                                    }),
                                ],
                            ),
                        ],
                    ),
            },
            {
                prop: 'cronExpression',
                queryField: 'CronExpression',
                label: t('scheduledJob.fields.cronExpression'),
                minWidth: 150,
                sortable: true,
                formatter: (row) =>
                    h('div', { class: 'cron-cell' }, [
                        h('div', row.cronExpression),
                        h(
                            'div',
                            { class: 'cron-description', style: { color: 'var(--el-text-color-secondary)', fontSize: '12px' } },
                            describeCron(row.cronExpression),
                        ),
                    ]),
            },
            {
                prop: 'requestInfo',
                queryField: 'RequestUrl',
                queryValueField: 'requestUrl',
                queryValueType: 'string',
                label: t('scheduledJob.fields.requestUrl'),
                minWidth: 400,
                formatter: (row) =>
                    h('div', { class: 'request-info' }, [
                        h(
                            'div',
                            {
                                'data-query-field': 'RequestMethod',
                                'data-query-label': t('scheduledJob.fields.requestMethod'),
                                'data-query-value': row.requestMethod,
                                'data-query-value-type': 'string',
                            },
                            [h(ElTag, { effect: 'light', size: 'small', type: requestMethodTagType(row.requestMethod) }, () => row.requestMethod)],
                        ),
                        h(
                            'div',
                            {
                                class: 'request-url',
                                'data-query-field': 'RequestUrl',
                                'data-query-label': t('scheduledJob.fields.requestUrl'),
                                'data-query-value': row.requestUrl,
                                'data-query-value-type': 'string',
                            },
                            row.requestUrl,
                        ),
                    ]),
            },
            {
                prop: 'executionInfo',
                queryField: 'Status',
                queryValueType: 'number',
                label: t('scheduledJob.fields.status'),
                minWidth: 150,
                sortable: true,
                align: 'right',
                formatter: (row) => {
                    const status = statusMap[row.status] || { key: 'unknown', type: 'info' as const }
                    return h('div', { class: 'execution-info' }, [
                        h(
                            'div',
                            {
                                'data-query-field': 'Status',
                                'data-query-label': t('scheduledJob.fields.status'),
                                'data-query-value': row.status,
                                'data-query-value-type': 'number',
                            },
                            [h(ElTag, { effect: 'light', size: 'small', type: status.type }, () => statusLabel(row.status))],
                        ),
                        h(
                            'div',
                            {
                                class: 'last-error',
                                'data-query-field': 'LastError',
                                'data-query-label': t('scheduledJob.fields.lastError'),
                                'data-query-value': row.lastError || '-',
                                'data-query-value-type': 'string',
                            },
                            row.lastError || '-',
                        ),
                    ])
                },
            },
            {
                prop: 'executionDuration',
                queryField: 'SuccessRate',
                queryValueType: 'number',
                label: `${t('scheduledJob.fields.executionDuration')} / ${t('scheduledJob.fields.successRate')}`,
                width: 150,
                align: 'right',
                sortable: true,
                formatter: (row) => {
                    const duration =
                        row.lastTriggeredAt && row.lastFinishedAt
                            ? Math.max(0, new Date(row.lastFinishedAt).getTime() - new Date(row.lastTriggeredAt).getTime())
                            : 0
                    const rate = Number(row.successRate ?? 0)
                    return h('div', { style: { display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '3px', lineHeight: '1.35' } }, [
                        h(
                            'span',
                            {
                                style: { color: duration > 1000 ? 'var(--el-color-danger)' : 'var(--el-color-success)' },
                            },
                            `${duration} ms`,
                        ),
                        h(
                            'span',
                            {
                                'data-query-field': 'SuccessRate',
                                'data-query-label': t('scheduledJob.fields.successRate'),
                                'data-query-value': rate,
                                'data-query-value-type': 'number',
                                style: {
                                    color: rate >= 90 ? 'var(--el-color-success)' : rate >= 60 ? 'var(--el-color-warning)' : 'var(--el-color-danger)',
                                },
                            },
                            `${rate.toFixed(2)}%`,
                        ),
                    ])
                },
            },
            {
                prop: 'lastTriggeredAt',
                queryField: 'LastTriggeredAt',
                queryValueType: 'date',
                label: `${t('scheduledJob.fields.previous')} / ${t('scheduledJob.fields.nextExecution')}`,
                width: 160,
                sortable: true,
                formatter: (row) =>
                    h('div', { class: 'job-time-cell' }, [
                        h(
                            'div',
                            {
                                'data-query-field': 'LastFinishedAt',
                                'data-query-label': t('scheduledJob.fields.lastFinishedAt'),
                                'data-query-value': row.lastFinishedAt,
                                'data-query-value-type': 'date',
                            },
                            formatTime(row.lastFinishedAt),
                        ),
                        h(
                            'div',
                            {
                                'data-query-field': 'LastFinishedAt',
                                'data-query-label': t('scheduledJob.fields.lastFinishedAt'),
                                'data-query-value': row.lastFinishedAt,
                                'data-query-value-type': 'date',
                            },
                            getNextExecution(row.cronExpression),
                        ),
                    ]),
            },
            {
                prop: 'remark',
                queryField: 'Remark',
                queryValueField: 'remark',
                queryValueType: 'string',
                label: t('scheduledJob.fields.remark'),
                minWidth: 180,
                sortable: true,
                showOverflowTooltip: true,
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
                            { key: 'copy', label: t('scheduledJob.actions.copy'), icon: 'ri:file-copy-line' },
                            { key: 'delete', label: t('scheduledJob.actions.delete'), icon: 'ri:delete-bin-4-line', color: '#f56c6c' },
                        ],
                        onClick: (item: ButtonMoreItem) => handleAction(item, row),
                    }),
            },
        ],
    },
})

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
    if (item.key === 'copy') {
        await fetchCopyScheduledJob(job.id)
        ElMessage.success(t('scheduledJob.messages.copySuccess'))
        await refreshCreate()
        return
    }
    if (item.key === 'executions') {
        executionJob.value = job
        executionVisible.value = true
        return
    }
    await ElMessageBox.confirm(`确定删除作业“${job.name}”吗？`, '删除确认', { type: 'warning' })
    await fetchDeleteScheduledJob(job.id)
    await refreshRemove()
}
const openRemarkDialog = (): void => {
    batchRemark.value = ''
    remarkDialogVisible.value = true
}
const saveBatchRemark = async (): Promise<void> => {
    batchRemarkSaving.value = true
    try {
        await fetchBatchUpdateScheduledJobRemark(
            selectedRows.value.map((row) => row.id),
            batchRemark.value,
        )
        remarkDialogVisible.value = false
        selectedRows.value = []
        await refreshData()
    } finally {
        batchRemarkSaving.value = false
    }
}
</script>

<style scoped>
.cron-cell {
    display: flex;
    flex-direction: column;
    gap: 3px;
    line-height: 1.35;
}
.cron-cell :deep(.cron-description) {
    display: block;
    color: var(--el-text-color-secondary) !important;
    font-size: 12px;
}
.job-time-cell {
    display: flex;
    flex-direction: column;
    gap: 3px;
    line-height: 1.35;
}
.job-info {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 0;
    line-height: 1.1;
    min-width: 0;
}
.job-name {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.request-info {
    display: flex;
    flex-direction: column;
    gap: 4px;
    line-height: 1.35;
    min-width: 0;
}
.request-url {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.execution-info {
    display: flex;
    flex-direction: column;
    gap: 0;
    line-height: 1.1;
    min-width: 0;
}
.last-error {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
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
.scheduled-job-tabs {
    flex: none;
}
.scheduled-job-tab-body {
    flex: 1;
    min-height: 0;
}
</style>
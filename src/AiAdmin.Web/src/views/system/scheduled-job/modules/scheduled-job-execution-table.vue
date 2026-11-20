<!-- 作业执行记录列表：传入 jobId 时只展示该作业的记录，否则展示全部作业的记录 -->
<template>
    <div class="execution-table-root">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :default-filter="defaultFilter"
            :export-fn="(query) => fetchExportScheduledJobExecutions(props.jobId, query)"
            :filter-fields="filterFields"
            :filter-groups-fn="filterGroups"
            :loading="loading"
            :pagination="pagination"
            :row-actions="rowActions"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            reserve-filter-groups
            resource="scheduled-job" />
        <ScheduledJobExecutionDialog v-model:visible="detailVisible" :execution="selectedExecution" />
    </div>
</template>

<script lang="ts" setup>
import { formatFullNumber } from '@/utils/number'
import { ElTag } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { computed, h } from 'vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchExportScheduledJobExecutions,
    fetchScheduledJobExecutionFilterFields,
    fetchScheduledJobExecutionFilterGroups,
    fetchScheduledJobExecutions,
    type DynamicFilter,
    type ListFilterField,
    type ScheduledJobExecution,
    type ScheduledJobExecutionSearchParams,
} from '@/api/system-manage'
import type { ColumnOption, RowAction } from '@/types/component'
import { formatDateTime, getTodayRange } from '@/utils/date'
import ScheduledJobExecutionDialog from './scheduled-job-execution-dialog.vue'

const props = defineProps<{
    /** 作业主键，为空时查询全部作业的执行记录 */
    jobId?: string
}>()
const { t, locale } = useI18n()

/** 默认筛选：全部执行记录只查今日，数据量大时避免一次扫全表；按作业查看（传入 jobId）时该作业的记录本来就少，不限定日期 */
const defaultFilter = computed<DynamicFilter | undefined>(() =>
    props.jobId ? undefined : { field: 'CreatedAt', operator: 'DateRange', value: getTodayRange() },
)
const filterFields = ref<ListFilterField[]>([])
const selectedExecution = ref<ScheduledJobExecution>()
const detailVisible = ref(false)
const statusMap: Record<number, { key: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }> = {
    0: { key: 'waiting', type: 'info' },
    1: { key: 'running', type: 'primary' },
    2: { key: 'success', type: 'success' },
    3: { key: 'failed', type: 'danger' },
    4: { key: 'timeout', type: 'warning' },
}
const statusLabel = (status: number): string => t(`scheduledJob.status.${statusMap[status]?.key || 'unknown'}`)
const requestMethodTagType = (method: string): 'info' | 'primary' | 'success' | 'danger' | 'warning' =>
    (({ GET: 'success', POST: 'primary', PUT: 'warning', PATCH: 'warning', DELETE: 'danger' })[method.toUpperCase()] as
        'success' | 'primary' | 'warning' | 'danger' | undefined) || 'info'
/** 响应状态码标签类型：2xx 成功、3xx 主色、4xx 警告、5xx 危险，其余为信息 */
const statusCodeTagType = (code: number): 'info' | 'primary' | 'success' | 'danger' | 'warning' => {
    if (code >= 200 && code < 300) return 'success'
    if (code >= 300 && code < 400) return 'primary'
    if (code >= 400 && code < 500) return 'warning'
    if (code >= 500) return 'danger'
    return 'info'
}
const formatTime = (value: string | null): string => (value ? formatDateTime(value, locale.value) : '-')

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
} = useTable({
    core: {
        apiFn: (params: ScheduledJobExecutionSearchParams) => fetchScheduledJobExecutions(props.jobId, params),
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter.value },
        immediate: false,
        columnsFactory: (): ColumnOption<ScheduledJobExecution>[] => [
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number' as const,
                label: 'ID',
                width: 160,
                align: 'left' as const,
                formatter: (row: ScheduledJobExecution) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            ...(props.jobId
                ? []
                : [
                      {
                          prop: 'scheduledJobId',
                          queryField: 'ScheduledJobId',
                          queryValueType: 'number' as const,
                          label: t('scheduledJob.executionDetail.fields.scheduledJobId'),
                          width: 160,
                          align: 'left' as const,
                          sortable: true,
                      },
                  ]),
            {
                prop: 'startedAt',
                queryField: 'StartedAt',
                label: t('scheduledJob.executionFields.startedAt'),
                width: 190,
                sortable: true,
                formatter: (row: ScheduledJobExecution) => formatTime(row.startedAt),
            },
            {
                prop: 'requestInfo',
                queryField: 'RequestUrl',
                queryValueField: 'requestUrl',
                queryValueType: 'string' as const,
                label: t('scheduledJob.executionFields.requestUrl'),
                minWidth: 280,
                formatter: (row: ScheduledJobExecution) =>
                    h('div', { class: 'request-info' }, [
                        h(
                            'div',
                            {
                                'data-query-field': 'RequestMethod',
                                'data-query-label': t('scheduledJob.executionFields.requestMethod'),
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
                                'data-query-label': t('scheduledJob.executionFields.requestUrl'),
                                'data-query-value': row.requestUrl,
                                'data-query-value-type': 'string',
                            },
                            row.requestUrl,
                        ),
                    ]),
            },
            {
                prop: 'responseStatusCode',
                queryField: 'ResponseStatusCode',
                queryValueType: 'number' as const,
                label: t('scheduledJob.executionFields.responseStatusCode'),
                width: 130,
                sortable: true,
                align: 'center' as const,
                formatter: (row: ScheduledJobExecution) =>
                    row.responseStatusCode === null || row.responseStatusCode === undefined
                        ? '-'
                        : h(ElTag, { effect: 'light', size: 'small', type: statusCodeTagType(row.responseStatusCode) }, () => row.responseStatusCode),
            },
            {
                prop: 'durationMs',
                queryField: 'DurationMs',
                queryValueType: 'number' as const,
                label: t('scheduledJob.executionFields.duration'),
                width: 120,
                sortable: true,
                align: 'right' as const,
                formatter: (row: ScheduledJobExecution) =>
                    t('scheduledJob.executionDetail.durationValue', { value: formatFullNumber(row.durationMs ?? 0) }),
            },
            {
                prop: 'status',
                queryField: 'Status',
                queryValueType: 'number' as const,
                label: t('scheduledJob.executionFields.status'),
                width: 130,
                sortable: true,
                align: 'center' as const,
                formatter: (row: ScheduledJobExecution) =>
                    h(ElTag, { effect: 'light', size: 'small', type: statusMap[row.status]?.type || 'info' }, () => statusLabel(row.status)),
            },
            {
                prop: 'result',
                queryField: 'Result',
                label: t('scheduledJob.executionFields.result'),
                minWidth: 220,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row: ScheduledJobExecution) => row.result || '-',
            },
            {
                prop: 'errorMessage',
                queryField: 'ErrorMessage',
                label: t('scheduledJob.executionFields.errorMessage'),
                minWidth: 180,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row: ScheduledJobExecution) => row.errorMessage || '-',
            },
        ],
    },
})

/** 行右键菜单操作 */
const rowActions = (row: ScheduledJobExecution): RowAction[] => [
    {
        key: 'view',
        label: t('scheduledJob.executionDetail.title'),
        icon: 'ri:eye-line',
        onClick: () => {
            selectedExecution.value = row
            detailVisible.value = true
        },
    },
]

const filterGroups = (dynamicFilter?: DynamicFilter) => fetchScheduledJobExecutionFilterGroups(props.jobId, dynamicFilter)
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}

onMounted(async () => {
    filterFields.value = await fetchScheduledJobExecutionFilterFields(props.jobId)
    replaceSearchParams({ current: 1, size: 20, dynamicFilter: defaultFilter.value })
    await getData()
})
</script>

<style scoped>
.execution-table-root {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
}
.execution-table-root :deep(.art-table-page) {
    flex: 1;
    height: auto;
    min-height: 0;
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
</style>
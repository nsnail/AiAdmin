<!-- 作业执行记录列表：传入 jobId 时只展示该作业的记录，否则展示全部作业的记录 -->
<template>
    <div class="execution-table-root">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :filter-fields="filterFields"
            :filter-groups-fn="filterGroups"
            :loading="loading"
            :pagination="pagination"
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
import { ElTag } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { h } from 'vue'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchScheduledJobExecutionFilterFields,
    fetchScheduledJobExecutionFilterGroups,
    fetchScheduledJobExecutions,
    type DynamicFilter,
    type ListFilterField,
    type ScheduledJobExecution,
    type ScheduledJobExecutionSearchParams,
} from '@/api/system-manage'
import type { ColumnOption } from '@/types/component'
import { formatDateTime } from '@/utils/date'
import ScheduledJobExecutionDialog from './scheduled-job-execution-dialog.vue'

const props = defineProps<{
    /** 作业主键，为空时查询全部作业的执行记录 */
    jobId?: string
}>()
const { t, locale } = useI18n()
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
        apiParams: { current: 1, size: 20 },
        immediate: false,
        columnsFactory: (): ColumnOption<ScheduledJobExecution>[] => [
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
                          formatter: (row: ScheduledJobExecution) => h(ArtListIdCell, { id: row.scheduledJobId, createdAt: row.createdAt }),
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
                align: 'right' as const,
                formatter: (row: ScheduledJobExecution) => row.responseStatusCode ?? '-',
            },
            {
                prop: 'status',
                queryField: 'Status',
                queryValueType: 'number' as const,
                label: t('scheduledJob.executionFields.status'),
                width: 130,
                sortable: true,
                formatter: (row: ScheduledJobExecution) =>
                    h(ElTag, { effect: 'light', size: 'small', type: statusMap[row.status]?.type || 'info' }, () => statusLabel(row.status)),
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
            {
                prop: 'operation',
                queryField: false,
                label: t('scheduledJob.detail.operation'),
                width: 70,
                fixed: 'right' as const,
                formatter: (row: ScheduledJobExecution) =>
                    h(ArtButtonTable, {
                        type: 'view',
                        title: t('scheduledJob.executionDetail.title'),
                        onClick: () => {
                            selectedExecution.value = row
                            detailVisible.value = true
                        },
                    }),
            },
        ],
    },
})

const filterGroups = (dynamicFilter?: DynamicFilter) => fetchScheduledJobExecutionFilterGroups(props.jobId, dynamicFilter)
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}

onMounted(async () => {
    filterFields.value = await fetchScheduledJobExecutionFilterFields(props.jobId)
    replaceSearchParams({ current: 1, size: 20, dynamicFilter: undefined })
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
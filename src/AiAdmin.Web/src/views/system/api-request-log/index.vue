<template>
    <div class="api-request-log-page art-full-height">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :default-filter="defaultFilter"
            :loading="loading"
            :pagination="pagination"
            :table-props="{ showPaginationWhenEmpty: true }"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            resource="api-request-log" />
        <ElDrawer v-model="detailVisible" :title="t('apiRequestLog.detail.title')" append-to-body destroy-on-close width="760px">
            <ElTabs v-if="selectedLog" v-model="activeDetailTab" type="card">
                <ElTabPane :label="t('apiRequestLog.detail.tabs.basic')" name="basic">
                    <ElDescriptions :column="2" :label-width="150" border>
                        <ElDescriptionsItem v-for="field in detailFields" :key="field" :label="t(`apiRequestLog.fields.${field}`)">
                            <span class="detail-value">{{ formatValue(field) }}</span>
                        </ElDescriptionsItem>
                    </ElDescriptions>
                </ElTabPane>
                <ElTabPane :label="t('apiRequestLog.detail.tabs.trace')" lazy name="trace">
                    <div v-loading="traceLoading" class="trace-pane">
                        <ElAlert v-if="traceFailed" :closable="false" :title="t('apiRequestLog.trace.loadFailed')" show-icon type="error">
                            <ElButton @click="loadTraceLogs" link type="primary">{{ t('apiRequestLog.trace.retry') }}</ElButton>
                        </ElAlert>
                        <ElEmpty v-else-if="!traceLoading && traceLogs.length === 0" :description="t('apiRequestLog.trace.empty')" />
                        <template v-else>
                            <div class="trace-summary">
                                {{ t('apiRequestLog.trace.count', { count: traceTotal }) }}
                                <span v-if="traceTotal > traceLogs.length">，{{ t('apiRequestLog.trace.truncated') }}</span>
                            </div>
                            <ElCollapse>
                                <ElCollapseItem v-for="(log, index) in traceLogs" :key="index" :name="index">
                                    <template #title>
                                        <ElTag :type="logLevelType(log.level)" class="mr-2" size="small">{{ log.level }}</ElTag>
                                        <ElTag class="mr-2" effect="plain" size="small">{{ log.logType }}</ElTag>
                                        <span class="trace-time">{{ formatTraceTime(log.timestamp) }}</span>
                                        <span v-if="log.elapsedMilliseconds !== null" class="trace-time ml-2">{{ log.elapsedMilliseconds }} ms</span>
                                        <span class="trace-message">{{ log.message }}</span>
                                    </template>
                                    <ElDescriptions :column="1" :label-width="150" border size="small">
                                        <ElDescriptionsItem v-for="field in traceFieldsOf(log)" :key="field" :label="t(`systemLog.fields.${field}`)">
                                            <pre class="trace-value">{{ formatTraceValue(log[field]) }}</pre>
                                        </ElDescriptionsItem>
                                    </ElDescriptions>
                                </ElCollapseItem>
                            </ElCollapse>
                        </template>
                    </div>
                </ElTabPane>
                <ElTabPane :label="t('apiRequestLog.detail.tabs.rawData')" name="rawData">
                    <ArtRawData :data="selectedLog" />
                </ElTabPane>
            </ElTabs>
            <template #footer>
                <ElButton @click="detailVisible = false">{{ t('apiRequestLog.detail.close') }}</ElButton>
            </template>
        </ElDrawer>
    </div>
</template>

<script lang="ts" setup>
import { ElAlert, ElButton, ElCollapse, ElCollapseItem, ElDescriptions, ElDescriptionsItem, ElDrawer, ElEmpty, ElTag } from 'element-plus'
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import { type ApiRequestLogRecord, fetchGetApiRequestLogList, fetchGetApiRequestLogTraceLogs } from '@/api/system-manage'
import { formatDateTime } from '@/utils/date'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useTable } from '@/hooks/core/useTable'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { translateServerMessage } from '@/utils/i18n/server-message'
import { getHashTagStyle } from '@/utils/tagColor'

// 操作日志按天分表保存，列表必须带创建时间范围，默认今天，后端最多合并 7 天的日表
defineOptions({ name: 'ApiRequestLog' })
const { t, locale } = useI18n()
const detailVisible = ref(false)
const selectedLog = ref<ApiRequestLogRecord>()
const activeDetailTab = ref('basic')
const detailFields = [
    'method',
    'path',
    'apiName',
    'statusCode',
    'apiResponseCode',
    'elapsedMilliseconds',
    'userId',
    'departmentId',
    'clientIp',
    'serverIp',
    'traceId',
    'workerId',
    'createdAt',
] as const

/** 生成带毫秒和本地时区偏移的 ISO 时间文本，格式需与日期选择器的 valueFormat（YYYY-MM-DDTHH:mm:ss.SSSZ）一致，否则选择器无法回显默认值 */
const toLocalIsoString = (date: Date): string => {
    const pad = (value: number, length = 2) => String(value).padStart(length, '0')
    const offsetMinutes = -date.getTimezoneOffset()
    const offset = `${offsetMinutes >= 0 ? '+' : '-'}${pad(Math.floor(Math.abs(offsetMinutes) / 60))}:${pad(Math.abs(offsetMinutes) % 60)}`
    return (
        `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}${offset}`
    )
}

/** 默认只查今天：本地今天零点到明天零点，右开区间 */
const getTodayRange = (): string[] => {
    const start = new Date()
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(end.getDate() + 1)
    return [toLocalIsoString(start), toLocalIsoString(end)]
}
const defaultFilter: DynamicFilter = { field: 'CreatedAt', operator: 'DateRange', value: getTodayRange() }

const statusType = (code: number) => (code >= 500 ? 'danger' : code >= 400 ? 'warning' : code >= 300 ? 'info' : 'success')

/** 链路日志：按当前请求的 TraceId 从 Elasticsearch 查询，切换到该页签时才加载 */
type TraceLog = Api.SystemManage.SystemLogItem
const traceLogs = ref<TraceLog[]>([])
const traceTotal = ref(0)
const traceLoading = ref(false)
const traceFailed = ref(false)
let traceRequestId = 0

const loadTraceLogs = async (): Promise<void> => {
    if (!selectedLog.value) return
    const current = ++traceRequestId
    traceLoading.value = true
    traceFailed.value = false
    try {
        const page = await fetchGetApiRequestLogTraceLogs(selectedLog.value.traceId)
        if (current !== traceRequestId) return
        traceLogs.value = page.records
        traceTotal.value = page.total
    } catch {
        if (current === traceRequestId) traceFailed.value = true
    } finally {
        if (current === traceRequestId) traceLoading.value = false
    }
}

/** 级别标签颜色，与系统日志页一致 */
const logLevelType = (level: string): 'danger' | 'warning' | 'success' | 'info' =>
    level === 'Error' || level === 'Critical' ? 'danger' : level === 'Warning' ? 'warning' : level === 'Information' ? 'success' : 'info'

const formatTraceTime = (value: string): string => (value ? formatDateTime(value, locale.value, { fractionalSecondDigits: 3 }) : '-')

/** 展开后展示的字段：只列出有值的字段，避免一大片空行 */
const traceFieldsOf = (log: TraceLog): Array<keyof TraceLog> => {
    const fields: Array<keyof TraceLog> = [
        'timestamp',
        'category',
        'source',
        'requestMethod',
        'requestUrl',
        'requestRelativeUrl',
        'statusCode',
        'elapsedMilliseconds',
        'sql',
        'requestHeaders',
        'requestBody',
        'responseHeaders',
        'responseBody',
        'exception',
    ]
    return fields.filter((field) => log[field] !== null && log[field] !== undefined && log[field] !== '')
}

const formatTraceValue = (value: unknown): string => (typeof value === 'string' ? value : JSON.stringify(value))

/** 切换到链路日志页签时才查询，同一条记录只查一次 */
watch(activeDetailTab, (tab) => {
    if (tab === 'trace' && traceLogs.value.length === 0 && !traceLoading.value) void loadTraceLogs()
})

const openDetail = (row: ApiRequestLogRecord) => {
    selectedLog.value = row
    activeDetailTab.value = 'basic'
    traceLogs.value = []
    traceTotal.value = 0
    traceFailed.value = false
    detailVisible.value = true
}

/** 详情字段取值：空值显示为 - */
const formatValue = (field: (typeof detailFields)[number]): string => {
    const value = selectedLog.value?.[field]
    if (value === undefined || value === null || value === '') return '-'
    if (field === 'apiName') return translateServerMessage(String(value)) ?? String(value)
    return field === 'elapsedMilliseconds' ? `${value} ms` : String(value)
}

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    resetColumns,
    getData,
} = useTable({
    core: {
        apiFn: fetchGetApiRequestLogList,
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter },
        columnsFactory: () => [
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 170,
                align: 'left',
                formatter: (row: ApiRequestLogRecord) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'method',
                queryField: 'Method',
                queryValueField: 'method',
                queryValueType: 'string',
                label: t('apiRequestLog.fields.method'),
                width: 120,
                align: 'center',
                formatter: (row: ApiRequestLogRecord) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(row.method) }, () => row.method),
            },
            {
                prop: 'path',
                queryField: 'PathCrc32',
                queryValueField: 'pathCrc32',
                queryValueType: 'number',
                label: t('apiRequestLog.fields.api'),
                minWidth: 280,
                showOverflowTooltip: true,
                formatter: (row: ApiRequestLogRecord) =>
                    h(
                        'div',
                        {
                            class: 'flex min-w-0 flex-col leading-5',
                            'data-query-field': 'PathCrc32',
                            'data-query-label': t('apiRequestLog.fields.api'),
                            'data-query-value': row.pathCrc32,
                            'data-query-value-type': 'number',
                        },
                        [
                            h('span', { class: 'truncate' }, row.path || String(row.pathCrc32)),
                            h(
                                'span',
                                { class: 'truncate text-xs text-gray-400' },
                                row.apiName ? translateServerMessage(row.apiName) : t('apiRequestLog.unknownApi'),
                            ),
                        ],
                    ),
            },
            {
                prop: 'statusCode',
                queryField: 'StatusCode',
                queryValueField: 'statusCode',
                queryValueType: 'number',
                label: t('apiRequestLog.fields.statusCode'),
                width: 100,
                align: 'center',
                sortable: true,
                formatter: (row: ApiRequestLogRecord) =>
                    h(ElTag, { size: 'small', effect: 'light', type: statusType(row.statusCode) }, () => String(row.statusCode)),
            },
            {
                prop: 'elapsedMilliseconds',
                queryField: 'ElapsedMilliseconds',
                queryValueField: 'elapsedMilliseconds',
                queryValueType: 'number',
                label: t('apiRequestLog.fields.elapsedMilliseconds'),
                width: 110,
                align: 'right',
                sortable: true,
                formatter: (row: ApiRequestLogRecord) => `${row.elapsedMilliseconds} ms`,
            },
            {
                prop: 'userId',
                queryField: 'UserId',
                queryValueField: 'userId',
                queryValueType: 'number',
                label: t('apiRequestLog.fields.user'),
                width: 140,
                showOverflowTooltip: true,
                formatter: (row: ApiRequestLogRecord) => row.userName || row.userId || '-',
            },
            {
                prop: 'clientIp',
                queryField: 'ClientIp',
                queryValueField: 'clientIp',
                queryValueType: 'string',
                label: t('apiRequestLog.fields.clientIp'),
                width: 150,
                formatter: (row: ApiRequestLogRecord) => row.clientIp || '-',
            },
            {
                prop: 'serverIp',
                queryField: 'ServerIp',
                queryValueField: 'serverIp',
                queryValueType: 'string',
                label: t('apiRequestLog.fields.serverIp'),
                width: 150,
                formatter: (row: ApiRequestLogRecord) => row.serverIp || '-',
            },
            {
                prop: 'operation',
                queryField: false,
                label: t('apiRequestLog.fields.actions'),
                width: 90,
                fixed: 'right',
                formatter: (row: ApiRequestLogRecord) =>
                    h(ArtButtonTable, {
                        type: 'view',
                        title: t('apiRequestLog.detail.view'),
                        onClick: () => openDetail(row),
                    }),
            },
        ],
    },
})

// 接口说明由 formatter 在渲染时翻译，切换语言后需要重建列才能刷新
watch(locale, () => resetColumns?.())

/** 公共列表输出完整查询树，替换唯一筛选状态后从第一页查询 */
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
</script>

<style scoped>
.detail-value {
    white-space: pre-wrap;
    word-break: break-word;
}

.trace-pane {
    min-height: 120px;
}

.trace-summary {
    margin-bottom: 8px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.trace-time {
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.trace-message {
    min-width: 0;
    margin-left: 8px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.trace-value {
    margin: 0;
    font-family: inherit;
    white-space: pre-wrap;
    word-break: break-word;
}
</style>
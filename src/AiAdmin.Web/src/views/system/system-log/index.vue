<template>
    <div class="system-log-page art-full-height">
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
            resource="system-log" />
        <ElDrawer v-model="detailVisible" :title="t('systemLog.detail.title')" destroy-on-close width="900px">
            <ElTabs v-if="selectedLog" v-model="activeDetailTab" type="card">
                <ElTabPane
                    v-for="group in visibleLogDetailGroups"
                    :key="group.key"
                    :label="t(`systemLog.detail.tabs.${group.key}`)"
                    :name="group.key">
                    <ElDescriptions :column="1" :label-width="220" border>
                        <ElDescriptionsItem v-for="field in group.fields" :key="field" :label="t(`systemLog.fields.${field}`)">
                            <pre @contextmenu.prevent="copyDetailValue(formatDetailValue(field, selectedLog[field]))" class="log-detail-value">{{
                                formatDetailValue(field, selectedLog[field])
                            }}</pre>
                        </ElDescriptionsItem>
                    </ElDescriptions>
                </ElTabPane>
                <ElTabPane :label="t('systemLog.detail.tabs.rawData')" name="rawData">
                    <ArtRawData :data="selectedLog" />
                </ElTabPane>
            </ElTabs>
            <template #footer>
                <ElButton @click="detailVisible = false">{{ t('systemLog.detail.close') }}</ElButton>
            </template>
        </ElDrawer>
    </div>
</template>

<script lang="ts" setup>
import { ElButton, ElMessage, ElTag } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import { fetchGetSystemLogs } from '@/api/system-manage'
import { useTable } from '@/hooks/core/useTable'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { formatDateTime } from '@/utils/date'
import type { ColumnOption } from '@/types/component'

// 系统日志复用公共列表的动态筛选、保存查询和排序状态
defineOptions({ name: 'SystemLog' })
const { t, locale } = useI18n()

const toLocalIsoString = (date: Date): string => {
    const pad = (value: number, length = 2) => String(value).padStart(length, '0')
    const offsetMinutes = -date.getTimezoneOffset()
    const sign = offsetMinutes >= 0 ? '+' : '-'
    const absoluteOffset = Math.abs(offsetMinutes)
    const offset = `${sign}${pad(Math.floor(absoluteOffset / 60))}:${pad(absoluteOffset % 60)}`
    return (
        `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}` +
        `.${pad(date.getMilliseconds(), 3)}${offset}`
    )
}

const getTodayTimestampRange = (): string[] => {
    const start = new Date()
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(end.getDate() + 1)
    return [toLocalIsoString(start), toLocalIsoString(end)]
}
const defaultFilter: DynamicFilter = {
    field: 'Timestamp',
    operator: 'DateRange',
    value: getTodayTimestampRange(),
}

const levelType = (level: string) => {
    if (level === 'Error' || level === 'Critical') return 'danger'
    if (level === 'Warning') return 'warning'
    if (level === 'Information') return 'success'
    return 'info'
}
const formatTime = (value: string) => (value ? formatDateTime(value, locale.value, { fractionalSecondDigits: 3 }) : '-')
const detailVisible = ref(false)
const selectedLog = ref<Api.SystemManage.SystemLogItem | null>(null)
const activeDetailTab = ref('basic')
const logDetailGroups: Array<{
    key: string
    fields: Array<keyof Api.SystemManage.SystemLogItem>
}> = [
    {
        key: 'basic',
        fields: ['timestamp', 'level', 'category', 'logType', 'message', 'source', 'threadId', 'eventId', 'eventName', 'exception'],
    },
    {
        key: 'api',
        fields: [
            'requestMethod',
            'clientIp',
            'serverIp',
            'userAgent',
            'requestRelativeUrl',
            'elapsedMilliseconds',
            'statusCode',
            'traceId',
            'userName',
            'requestContentType',
            'requestHeaders',
            'requestBody',
            'responseHeaders',
            'responseBody',
            'responseContentType',
        ],
    },
    { key: 'sql', fields: ['sql', 'elapsedMilliseconds'] },
    {
        key: 'http',
        fields: [
            'requestMethod',
            'requestUrl',
            'elapsedMilliseconds',
            'statusCode',
            'requestContentType',
            'requestHeaders',
            'requestBody',
            'responseHeaders',
            'responseBody',
            'responseContentType',
        ],
    },
    {
        key: 'other',
        fields: [
            'clientIp',
            'serverIp',
            'userAgent',
            'requestRelativeUrl',
            'requestUrl',
            'elapsedMilliseconds',
            'statusCode',
            'traceId',
            'userName',
            'requestContentType',
            'requestHeaders',
            'requestBody',
            'responseHeaders',
            'responseBody',
            'responseContentType',
            'sql',
        ],
    },
]
const visibleLogDetailGroups = computed(() => {
    const logType = selectedLog.value?.logType?.toLowerCase() || ''
    const category = logType.includes('api') ? 'api' : logType.includes('sql') ? 'sql' : logType.includes('http') ? 'http' : 'other'
    return logDetailGroups.filter((group) => group.key === 'basic' || group.key === category)
})
const formatDetailValue = (field: keyof Api.SystemManage.SystemLogItem, value: unknown) => {
    if (value === null || value === undefined || value === '') return '-'
    if (field === 'timestamp') return formatTime(String(value))
    return typeof value === 'string' ? value : JSON.stringify(value)
}
const copyDetailValue = async (value: string) => {
    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value)
        } else {
            const textarea = document.createElement('textarea')
            textarea.value = value
            textarea.style.position = 'fixed'
            textarea.style.opacity = '0'
            document.body.appendChild(textarea)
            textarea.select()
            document.execCommand('copy')
            textarea.remove()
        }
        ElMessage.success(t('systemLog.detail.copied'))
    } catch {
        ElMessage.error(t('systemLog.detail.copyFailed'))
    }
}
const openDetail = (row: Api.SystemManage.SystemLogItem) => {
    selectedLog.value = row
    activeDetailTab.value = 'basic'
    detailVisible.value = true
}

const withQueryFields = (items: ColumnOption<Api.SystemManage.SystemLogItem>[]): ColumnOption<Api.SystemManage.SystemLogItem>[] =>
    items.map((item): ColumnOption<Api.SystemManage.SystemLogItem> => {
        const valueType =
            item.queryValueType ||
            (item.prop === 'timestamp'
                ? 'date'
                : ['elapsedMilliseconds', 'eventId', 'statusCode', 'threadId', 'traceId'].includes(String(item.prop))
                  ? 'number'
                  : 'string')
        return {
            ...item,
            queryField: item.queryField ?? String(item.prop).replace(/^./, (letter) => letter.toUpperCase()),
            queryValueField: item.prop,
            sortable: item.queryField === false ? false : 'custom',
            queryValueType: valueType,
        }
    })

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
        apiFn: fetchGetSystemLogs,
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter },
        columnsFactory: () =>
            withQueryFields([
                {
                    prop: 'timestamp',
                    label: t('systemLog.fields.timestamp'),
                    width: 180,
                    sortable: true,
                    formatter: (row: Api.SystemManage.SystemLogItem) => formatTime(row.timestamp),
                },
                {
                    prop: 'level',
                    label: t('systemLog.fields.level'),
                    width: 120,
                    align: 'center',
                    formatter: (row: Api.SystemManage.SystemLogItem) => h(ElTag, { type: levelType(row.level), size: 'small' }, () => row.level),
                },
                { prop: 'logType', label: t('systemLog.fields.logType'), width: 120, align: 'center' },
                {
                    prop: 'message',
                    label: t('systemLog.fields.message'),
                    minWidth: 360,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'eventName',
                    label: t('systemLog.fields.eventName'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'elapsedMilliseconds',
                    label: t('systemLog.fields.elapsedMilliseconds'),
                    width: 130,
                    align: 'right',
                    formatter: (row: Api.SystemManage.SystemLogItem) => (row.elapsedMilliseconds ? `${row.elapsedMilliseconds} ms` : ''),
                },
                { prop: 'threadId', label: t('systemLog.fields.threadId'), width: 100, align: 'right' },
                {
                    prop: 'category',
                    label: t('systemLog.fields.category'),
                    minWidth: 260,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestRelativeUrl',
                    label: t('systemLog.fields.requestRelativeUrl'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestMethod',
                    label: t('systemLog.fields.requestMethod'),
                    width: 140,
                    align: 'center',
                },
                {
                    prop: 'traceId',
                    label: t('systemLog.fields.traceId'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestContentType',
                    label: t('systemLog.fields.requestContentType'),
                    minWidth: 180,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestHeaders',
                    label: t('systemLog.fields.requestHeaders'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestBody',
                    label: t('systemLog.fields.requestBody'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'userAgent',
                    label: t('systemLog.fields.userAgent'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'serverIp',
                    label: t('systemLog.fields.serverIp'),
                    minWidth: 140,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'clientIp',
                    label: t('systemLog.fields.clientIp'),
                    minWidth: 140,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'userName',
                    label: t('systemLog.fields.userName'),
                    minWidth: 140,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'requestUrl',
                    label: t('systemLog.fields.requestUrl'),
                    minWidth: 260,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'responseContentType',
                    label: t('systemLog.fields.responseContentType'),
                    minWidth: 180,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'responseHeaders',
                    label: t('systemLog.fields.responseHeaders'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'responseBody',
                    label: t('systemLog.fields.responseBody'),
                    minWidth: 220,
                    showOverflowTooltip: true,
                },
                { prop: 'statusCode', label: t('systemLog.fields.statusCode'), width: 130, align: 'right' },
                {
                    prop: 'sql',
                    label: t('systemLog.fields.sql'),
                    minWidth: 300,
                    showOverflowTooltip: true,
                },
                {
                    prop: 'exception',
                    label: t('systemLog.fields.exception'),
                    minWidth: 260,
                    showOverflowTooltip: true,
                    formatter: (row: Api.SystemManage.SystemLogItem) => row.exception || '-',
                },
                {
                    prop: 'operation',
                    queryField: false,
                    label: t('systemLog.detail.operation'),
                    width: 110,
                    align: 'center',
                    fixed: 'right',
                    formatter: (row: Api.SystemManage.SystemLogItem) =>
                        h(ArtButtonTable, {
                            type: 'view',
                            title: t('systemLog.detail.view'),
                            onClick: () => openDetail(row),
                        }),
                },
            ]),
    },
})

// 公共列表输出完整查询树，替换唯一筛选状态后从第一页查询
async function handleFilterChange(dynamicFilter: DynamicFilter | undefined) {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
</script>

<style scoped>
.system-log-page :deep(.art-table-card .el-card__body) {
    display: flex;
    flex-direction: column;
    min-height: 0;
}
.system-log-page :deep(.art-table) {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    min-height: 0;
}
.system-log-page :deep(.art-table > .el-table) {
    height: auto !important;
    min-height: 0;
    flex: 1 1 auto;
}
.system-log-page :deep(.art-table .custom-pagination) {
    position: static;
    z-index: auto;
    flex: 0 0 auto;
    box-sizing: border-box;
    min-height: 56px;
    padding: 8px 0 0;
    margin-top: 0;
    background: var(--default-box-color);
}
.system-log-page :deep(.el-table th .cell) {
    white-space: nowrap;
}
.system-log-page :deep(.el-descriptions__label) {
    width: 220px;
    white-space: nowrap;
}
.log-detail-value {
    max-height: 240px;
    margin: 0;
    overflow: auto;
    white-space: pre-wrap;
    word-break: break-word;
    font: inherit;
}

@media (max-width: 640px) {
    .system-log-page :deep(.art-table .custom-pagination) {
        min-height: 108px;
        padding: 8px 0;
    }
    .system-log-page :deep(.el-descriptions__label) {
        width: 140px;
    }
}
</style>
<template>
    <div class="art-full-height">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :loading="loading"
            :pagination="pagination"
            :row-actions="rowActions"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @selection-change="selectedRows = $event"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            ref="monitorPage"
            resource="alert-monitor">
            <template #header-left>
                <ElButton @click="openDialog()" type="primary">{{ t('alertMonitor.actions.create') }}</ElButton>
                <ElButton
                    v-if="hasAuth('delete')"
                    :disabled="!selectedRows.length"
                    :loading="deleting"
                    @click="confirmBatchDelete"
                    plain
                    type="danger"
                    >{{ t('alertMonitor.delete.batch') }}</ElButton
                >
            </template>
        </ArtTablePage>
        <AlertMonitorDialog v-model:visible="dialogVisible" :monitor-data="currentRow" :saving="saving" @submit="saveMonitor" />
    </div>
</template>

<script lang="ts" setup>
import { h } from 'vue'
import type { RowAction } from '@/types/component'
import { useI18n } from 'vue-i18n'
import { ElMessageBox, ElTag } from 'element-plus'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useRouteAuth } from '@/hooks/core/useRouteAuth'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchCreateAlertMonitor,
    fetchDeleteAlertMonitors,
    fetchGetAlertMonitorList,
    fetchUpdateAlertMonitor,
    type AlertMonitorInfo,
    type DynamicFilter,
    type SaveAlertMonitor,
} from '@/api/system-manage'
import { formatDateTime } from '@/utils/date'
import { getHashTagStyle } from '@/utils/tagColor'
import AlertMonitorDialog from './modules/alert-monitor-dialog.vue'

defineOptions({ name: 'AlertMonitorManagement' })
const { t, locale } = useI18n()
const { hasAuth } = useRouteAuth()
const monitorPage = ref<{ refreshFilterGroups: (filter?: DynamicFilter) => Promise<void> }>()
const selectedRows = ref<AlertMonitorInfo[]>([])
const deleting = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const currentRow = ref<AlertMonitorInfo>()

/** 数据来源、指标、比较类型和操作符对应的 i18n 键，与后端枚举编号保持一致 */
const sourceKeys: Record<number, string> = { 1: 'alertMonitor.source.dictionary', 2: 'alertMonitor.source.redis' }
const metricKeys: Record<number, string> = { 1: 'alertMonitor.metric.value', 2: 'alertMonitor.metric.length' }
const valueTypeKeys: Record<number, string> = { 1: 'alertMonitor.valueType.number', 2: 'alertMonitor.valueType.string' }
const operatorKeys: Record<number, string> = {
    1: 'alertMonitor.operator.greaterThan',
    2: 'alertMonitor.operator.greaterThanOrEqual',
    3: 'alertMonitor.operator.lessThan',
    4: 'alertMonitor.operator.lessThanOrEqual',
    5: 'alertMonitor.operator.equal',
    6: 'alertMonitor.operator.notEqual',
    7: 'alertMonitor.operator.contains',
    8: 'alertMonitor.operator.notContains',
    9: 'alertMonitor.operator.startsWith',
    10: 'alertMonitor.operator.endsWith',
}

/** 把条件渲染成“指标 操作符 阈值”，如 长度 大于 1000 */
const conditionText = (row: AlertMonitorInfo): string =>
    `${t(metricKeys[row.metric] ?? 'alertMonitor.metric.value')} ${t(operatorKeys[row.operator] ?? '')} ${row.threshold}`

/** 目标展示：字典来源带目录编码前缀 */
const targetText = (row: AlertMonitorInfo): string => (row.source === 1 && row.categoryCode ? `${row.categoryCode} / ${row.target}` : row.target)

/** 打开新增或编辑抽屉 */
const openDialog = (row?: AlertMonitorInfo): void => {
    currentRow.value = row
    dialogVisible.value = true
}

/** 保存后刷新列表和分组计数 */
const saveMonitor = async (form: SaveAlertMonitor): Promise<void> => {
    saving.value = true
    try {
        await (form.id ? fetchUpdateAlertMonitor(form) : fetchCreateAlertMonitor(form))
        dialogVisible.value = false
        await refreshData()
        await monitorPage.value?.refreshFilterGroups()
    } finally {
        saving.value = false
    }
}

/** 删除后刷新列表和分组计数 */
const deleteRows = async (ids: string[]): Promise<void> => {
    deleting.value = true
    try {
        await fetchDeleteAlertMonitors(ids)
        selectedRows.value = selectedRows.value.filter((x) => !ids.includes(String(x.id)))
        await refreshData()
        await monitorPage.value?.refreshFilterGroups()
    } finally {
        deleting.value = false
    }
}

/** 批量删除前使用弹窗确认 */
const confirmBatchDelete = async (): Promise<void> => {
    const rows = selectedRows.value
    if (!rows.length) return
    try {
        await ElMessageBox.confirm(t('alertMonitor.delete.batchConfirm', { count: rows.length }), t('alertMonitor.delete.batch'), {
            cancelButtonText: t('common.cancel'),
            confirmButtonText: t('common.confirm'),
            type: 'warning',
        })
    } catch {
        // 用户取消确认时保持当前选择和列表状态
        return
    }
    await deleteRows(rows.map((x) => String(x.id)))
}

const time = (value: string | null): string => (value ? formatDateTime(value, locale.value) : '-')

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    replaceSearchParams,
    getData,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    resetColumns,
} = useTable({
    core: {
        apiFn: fetchGetAlertMonitorList,
        apiParams: { current: 1, size: 20, sortField: 'CreatedAt', sortOrder: 'desc' },
        columnsFactory: () => [
            { type: 'selection', width: 48, fixed: 'left' },
            {
                prop: 'name',
                queryField: 'Name',
                queryValueType: 'string',
                label: t('alertMonitor.fields.name'),
                minWidth: 200,
                align: 'left',
                sortable: true,
                formatter: (row: AlertMonitorInfo) => h(ArtListIdCell, { id: row.name, createdAt: row.createdAt }),
            },
            {
                prop: 'source',
                queryField: 'Source',
                queryValueType: 'number',
                label: t('alertMonitor.fields.source'),
                width: 110,
                align: 'center',
                sortable: true,
                formatter: (row: AlertMonitorInfo) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(t(sourceKeys[row.source] ?? '')) }, () =>
                        t(sourceKeys[row.source] ?? ''),
                    ),
            },
            {
                prop: 'target',
                queryField: 'Target',
                queryValueType: 'string',
                label: t('alertMonitor.fields.target'),
                minWidth: 220,
                showOverflowTooltip: true,
                sortable: true,
                formatter: (row: AlertMonitorInfo) => targetText(row),
            },
            {
                prop: 'metric',
                queryField: 'Metric',
                queryValueType: 'number',
                label: t('alertMonitor.fields.metric'),
                width: 100,
                align: 'center',
                sortable: true,
                formatter: (row: AlertMonitorInfo) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(t(metricKeys[row.metric] ?? '')) }, () =>
                        t(metricKeys[row.metric] ?? ''),
                    ),
            },
            {
                prop: 'valueType',
                queryField: 'ValueType',
                queryValueType: 'number',
                label: t('alertMonitor.fields.valueType'),
                width: 110,
                align: 'center',
                sortable: true,
                formatter: (row: AlertMonitorInfo) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(t(valueTypeKeys[row.valueType] ?? '')) }, () =>
                        t(valueTypeKeys[row.valueType] ?? ''),
                    ),
            },
            {
                prop: 'operator',
                queryField: 'Operator',
                queryValueType: 'number',
                label: t('alertMonitor.fields.condition'),
                minWidth: 150,
                showOverflowTooltip: true,
                sortable: true,
                formatter: (row: AlertMonitorInfo) => conditionText(row),
            },
            {
                prop: 'lastValue',
                queryField: false,
                label: t('alertMonitor.fields.lastScan'),
                minWidth: 190,
                // 最近取值在上，最近扫描时间在下
                formatter: (row: AlertMonitorInfo) =>
                    h('div', { class: 'scan-cell' }, [
                        h('div', { class: 'scan-value', title: row.lastValue || undefined }, row.lastValue || '-'),
                        h('div', { class: 'scan-time' }, time(row.lastCheckedAt)),
                    ]),
            },
            {
                prop: 'alertActive',
                queryField: false,
                label: t('alertMonitor.fields.alertState'),
                width: 150,
                align: 'center',
                formatter: (row: AlertMonitorInfo) => {
                    if (!row.isEnabled) return h(ElTag, { size: 'small', type: 'info' }, () => t('alertMonitor.state.disabled'))
                    if (!row.lastCheckedAt) return h(ElTag, { size: 'small', type: 'info' }, () => t('alertMonitor.state.pending'))
                    return row.alertActive
                        ? h(ElTag, { size: 'small', type: 'danger', title: row.lastMessage }, () => t('alertMonitor.state.alerting'))
                        : h(ElTag, { size: 'small', type: 'success' }, () => t('alertMonitor.state.normal'))
                },
            },
            {
                prop: 'remark',
                queryField: 'Remark',
                queryValueType: 'string',
                label: t('alertMonitor.fields.remark'),
                minWidth: 160,
                showOverflowTooltip: true,
                sortable: true,
            },
            {
                prop: 'isEnabled',
                queryField: 'IsEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 110,
                align: 'center',
                sortable: true,
                formatter: (row: AlertMonitorInfo) =>
                    h(ArtEnabledSwitch, {
                        id: String(row.id),
                        resource: 'alert-monitor',
                        modelValue: row.isEnabled,
                        'onUpdate:modelValue': async () => {
                            await refreshData()
                            await monitorPage.value?.refreshFilterGroups()
                        },
                    }),
            },
        ],
    },
})

/** 行右键菜单操作 */
const rowActions = (row: AlertMonitorInfo): RowAction[] => {
    const actions: RowAction[] = [{ key: 'edit', label: t('alertMonitor.actions.edit'), icon: 'ri:pencil-line', onClick: () => openDialog(row) }]
    if (hasAuth('delete')) {
        actions.push({
            key: 'delete',
            label: t('common.rowAction.delete'),
            icon: 'ri:delete-bin-5-line',
            danger: true,
            confirm: t('alertMonitor.delete.confirm'),
            onClick: () => deleteRows([String(row.id)]),
        })
    }
    return actions
}

watch(locale, () => resetColumns?.())

/** 筛选条件变化后写入唯一筛选状态并重新查询 */
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
</script>

<style scoped>
/* 最近取值在上，最近扫描时间在下，取值过长时省略并在悬停时显示完整内容 */
.scan-cell {
    display: flex;
    flex-direction: column;
    gap: 3px;
    min-width: 0;
    line-height: 1.35;
}
.scan-value {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.scan-time {
    font-size: 12px;
    color: var(--el-text-color-secondary);
}
</style>
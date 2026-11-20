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
            ref="rulePage"
            resource="circuit-breaker">
            <template #header-left>
                <ElButton @click="openDialog()" type="primary">{{ t('circuitBreaker.actions.create') }}</ElButton>
                <ElButton
                    v-if="hasAuth('delete')"
                    :disabled="!selectedRows.length"
                    :loading="deleting"
                    @click="confirmBatchDelete"
                    plain
                    type="danger"
                    >{{ t('circuitBreaker.delete.batch') }}</ElButton
                >
            </template>
        </ArtTablePage>
        <CircuitBreakerDialog v-model:visible="dialogVisible" :rule-data="currentRow" :saving="saving" @submit="saveRule" />
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
    fetchCreateCircuitBreakerRule,
    fetchDeleteCircuitBreakerRules,
    fetchGetCircuitBreakerRuleList,
    fetchUpdateCircuitBreakerRule,
    type CircuitBreakerRuleInfo,
    type DynamicFilter,
    type SaveCircuitBreakerRule,
} from '@/api/system-manage'
import CircuitBreakerDialog from './modules/circuit-breaker-dialog.vue'

defineOptions({ name: 'CircuitBreakerManagement' })
const { t, locale } = useI18n()
const { hasAuth } = useRouteAuth()
const rulePage = ref<{ refreshFilterGroups: (filter?: DynamicFilter) => Promise<void> }>()
const selectedRows = ref<CircuitBreakerRuleInfo[]>([])
const deleting = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const currentRow = ref<CircuitBreakerRuleInfo>()

/** 熔断状态对应的 i18n 键和标签类型，与后端枚举编号保持一致：1 关闭 2 打开 3 半开 */
const stateKeys: Record<number, string> = { 1: 'circuitBreaker.state.closed', 2: 'circuitBreaker.state.open', 3: 'circuitBreaker.state.halfOpen' }
const stateTypes: Record<number, 'success' | 'danger' | 'warning'> = { 1: 'success', 2: 'danger', 3: 'warning' }

/** 打开新增或编辑抽屉 */
const openDialog = (row?: CircuitBreakerRuleInfo): void => {
    currentRow.value = row
    dialogVisible.value = true
}

/** 保存后刷新列表和分组计数 */
const saveRule = async (form: SaveCircuitBreakerRule): Promise<void> => {
    saving.value = true
    try {
        await (form.id ? fetchUpdateCircuitBreakerRule(form) : fetchCreateCircuitBreakerRule(form))
        dialogVisible.value = false
        await refreshData()
        await rulePage.value?.refreshFilterGroups()
    } finally {
        saving.value = false
    }
}

/** 删除后刷新列表和分组计数 */
const deleteRows = async (ids: string[]): Promise<void> => {
    deleting.value = true
    try {
        await fetchDeleteCircuitBreakerRules(ids)
        selectedRows.value = selectedRows.value.filter((x) => !ids.includes(String(x.id)))
        await refreshData()
        await rulePage.value?.refreshFilterGroups()
    } finally {
        deleting.value = false
    }
}

/** 批量删除前使用弹窗确认 */
const confirmBatchDelete = async (): Promise<void> => {
    const rows = selectedRows.value
    if (!rows.length) return
    try {
        await ElMessageBox.confirm(t('circuitBreaker.delete.batchConfirm', { count: rows.length }), t('circuitBreaker.delete.batch'), {
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

/** 状态展示：打开状态附带距离进入半开的剩余秒数 */
const stateText = (row: CircuitBreakerRuleInfo): string => {
    const text = t(stateKeys[row.state] ?? stateKeys[1])
    return row.state === 2 && row.retryAfterSeconds > 0 ? `${text} (${row.retryAfterSeconds}s)` : text
}

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
        apiFn: fetchGetCircuitBreakerRuleList,
        apiParams: { current: 1, size: 20, sortField: 'CreatedAt', sortOrder: 'desc' },
        columnsFactory: () => [
            { type: 'selection', width: 48, fixed: 'left' },
            {
                prop: 'path',
                queryField: 'ApiEndpoint.Path',
                queryValueField: 'path',
                queryValueType: 'string',
                label: t('circuitBreaker.fields.api'),
                minWidth: 280,
                align: 'left',
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row: CircuitBreakerRuleInfo) => h(ArtListIdCell, { id: `${row.method} ${row.path}`, createdAt: row.createdAt }),
            },
            {
                prop: 'state',
                queryField: false,
                label: t('circuitBreaker.fields.state'),
                width: 150,
                align: 'center',
                formatter: (row: CircuitBreakerRuleInfo) =>
                    h(ElTag, { size: 'small', type: row.isEnabled ? (stateTypes[row.state] ?? 'success') : 'info' }, () =>
                        row.isEnabled ? stateText(row) : t('circuitBreaker.state.disabled'),
                    ),
            },
            {
                prop: 'failureRatePercent',
                queryField: 'FailureRatePercent',
                queryValueType: 'number',
                label: t('circuitBreaker.fields.condition'),
                minWidth: 300,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row: CircuitBreakerRuleInfo) =>
                    t('circuitBreaker.condition', {
                        seconds: row.windowSeconds,
                        minimum: row.minimumRequests,
                        percent: row.failureRatePercent,
                    }),
            },
            {
                prop: 'slowCallMs',
                queryField: 'SlowCallMs',
                queryValueType: 'number',
                label: t('circuitBreaker.fields.slowCallMs'),
                width: 190,
                align: 'right',
                sortable: true,
                formatter: (row: CircuitBreakerRuleInfo) => (row.slowCallMs > 0 ? `${row.slowCallMs} ms` : '-'),
            },
            {
                prop: 'openSeconds',
                queryField: 'OpenSeconds',
                queryValueType: 'number',
                label: t('circuitBreaker.fields.openSeconds'),
                width: 180,
                align: 'right',
                sortable: true,
                formatter: (row: CircuitBreakerRuleInfo) => `${row.openSeconds} s`,
            },
            {
                prop: 'halfOpenRequests',
                queryField: 'HalfOpenRequests',
                queryValueType: 'number',
                label: t('circuitBreaker.fields.halfOpenRequests'),
                width: 150,
                align: 'right',
                sortable: true,
            },
            {
                prop: 'remark',
                queryField: 'Remark',
                queryValueType: 'string',
                label: t('circuitBreaker.fields.remark'),
                minWidth: 160,
                sortable: true,
                showOverflowTooltip: true,
            },
            {
                prop: 'isEnabled',
                queryField: 'IsEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 110,
                align: 'center',
                sortable: true,
                formatter: (row: CircuitBreakerRuleInfo) =>
                    h(ArtEnabledSwitch, {
                        id: String(row.id),
                        resource: 'circuit-breaker',
                        modelValue: row.isEnabled,
                        'onUpdate:modelValue': async () => {
                            await refreshData()
                            await rulePage.value?.refreshFilterGroups()
                        },
                    }),
            },
        ],
    },
})

/** 行右键菜单操作 */
const rowActions = (row: CircuitBreakerRuleInfo): RowAction[] => {
    const actions: RowAction[] = [
        { key: 'edit', dblclick: true, label: t('circuitBreaker.actions.edit'), icon: 'ri:pencil-line', onClick: () => openDialog(row) },
    ]
    if (hasAuth('delete')) {
        actions.push({
            key: 'delete',
            label: t('common.rowAction.delete'),
            icon: 'ri:delete-bin-5-line',
            danger: true,
            confirm: t('circuitBreaker.delete.confirm'),
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
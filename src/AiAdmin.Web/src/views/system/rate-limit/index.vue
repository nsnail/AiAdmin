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
            resource="rate-limit">
            <template #header-left>
                <ElButton @click="openDialog()" type="primary">{{ t('rateLimit.actions.create') }}</ElButton>
                <ElButton
                    v-if="hasAuth('delete')"
                    :disabled="!selectedRows.length"
                    :loading="deleting"
                    @click="confirmBatchDelete"
                    plain
                    type="danger"
                    >{{ t('rateLimit.delete.batch') }}</ElButton
                >
            </template>
        </ArtTablePage>
        <RateLimitDialog v-model:visible="dialogVisible" :rule-data="currentRow" :saving="saving" @submit="saveRule" />
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
    fetchCreateRateLimitRule,
    fetchDeleteRateLimitRules,
    fetchGetRateLimitRuleList,
    fetchUpdateRateLimitRule,
    type DynamicFilter,
    type RateLimitRuleInfo,
    type SaveRateLimitRule,
} from '@/api/system-manage'
import { getHashTagStyle } from '@/utils/tagColor'
import RateLimitDialog from './modules/rate-limit-dialog.vue'

defineOptions({ name: 'RateLimitManagement' })
const { t, locale } = useI18n()
const { hasAuth } = useRouteAuth()
const rulePage = ref<{ refreshFilterGroups: (filter?: DynamicFilter) => Promise<void> }>()
const selectedRows = ref<RateLimitRuleInfo[]>([])
const deleting = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const currentRow = ref<RateLimitRuleInfo>()

/** 算法和范围对应的 i18n 键，与后端枚举编号保持一致 */
const algorithmKeys: Record<number, string> = {
    1: 'rateLimit.algorithm.slidingWindow',
    2: 'rateLimit.algorithm.leakyBucket',
    3: 'rateLimit.algorithm.tokenBucket',
}
const scopeKeys: Record<number, string> = { 1: 'rateLimit.scope.global', 2: 'rateLimit.scope.user', 3: 'rateLimit.scope.role' }

/** 对象展示：全局、指定角色的名称、指定用户的用户名，或每个用户 */
const subjectText = (row: RateLimitRuleInfo): string => {
    if (row.scope === 1) return t('rateLimit.scope.global')
    if (row.scope === 3) return row.roleName ?? row.roleCode ?? '-'
    return row.userId ? (row.userName ?? row.userId) : t('rateLimit.scopeAll')
}

/** 作用对象是否为固定语义：全局，或没有指定用户的每个用户 */
const isFixedSubject = (row: RateLimitRuleInfo): boolean => row.scope === 1 || (row.scope === 2 && !row.userId)

/**
 * 作用对象单元格对应的右键筛选目标：指定角色按角色编码，指定用户按用户主键，全局和每个用户没有具体对象，按限流范围筛选
 * @param row 限流规则行
 * @returns 筛选字段、显示名称、值和值类型
 */
const subjectQuery = (row: RateLimitRuleInfo): { field: string; label: string; value: string; valueType: 'string' | 'number' } => {
    if (row.scope === 3) return { field: 'RoleCode', label: t('rateLimit.fields.role'), value: row.roleCode ?? '', valueType: 'string' }
    if (row.scope === 2 && row.userId) return { field: 'UserId', label: t('rateLimit.fields.user'), value: row.userId, valueType: 'number' }
    return { field: 'Scope', label: t('rateLimit.fields.scope'), value: String(row.scope), valueType: 'number' }
}

/** 打开新增或编辑抽屉 */
const openDialog = (row?: RateLimitRuleInfo): void => {
    currentRow.value = row
    dialogVisible.value = true
}

/** 保存后刷新列表和分组计数 */
const saveRule = async (form: SaveRateLimitRule): Promise<void> => {
    saving.value = true
    try {
        await (form.id ? fetchUpdateRateLimitRule(form) : fetchCreateRateLimitRule(form))
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
        await fetchDeleteRateLimitRules(ids)
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
        await ElMessageBox.confirm(t('rateLimit.delete.batchConfirm', { count: rows.length }), t('rateLimit.delete.batch'), {
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
        apiFn: fetchGetRateLimitRuleList,
        apiParams: { current: 1, size: 20, sortField: 'CreatedAt', sortOrder: 'desc' },
        columnsFactory: () => [
            { type: 'selection', width: 48, fixed: 'left' },
            {
                prop: 'path',
                queryField: 'ApiEndpoint.Path',
                queryValueField: 'path',
                queryValueType: 'string',
                label: t('rateLimit.fields.api'),
                minWidth: 280,
                align: 'left',
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row: RateLimitRuleInfo) => h(ArtListIdCell, { id: `${row.method} ${row.path}`, createdAt: row.createdAt }),
            },
            {
                prop: 'algorithm',
                queryField: 'Algorithm',
                queryValueType: 'number',
                label: t('rateLimit.fields.algorithm'),
                width: 120,
                align: 'center',
                sortable: true,
                formatter: (row: RateLimitRuleInfo) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(t(algorithmKeys[row.algorithm] ?? '')) }, () =>
                        t(algorithmKeys[row.algorithm] ?? ''),
                    ),
            },
            {
                prop: 'scope',
                queryField: 'Scope',
                queryValueType: 'number',
                label: t('rateLimit.fields.scope'),
                width: 130,
                align: 'center',
                sortable: true,
                formatter: (row: RateLimitRuleInfo) =>
                    h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(t(scopeKeys[row.scope] ?? '')) }, () =>
                        t(scopeKeys[row.scope] ?? ''),
                    ),
            },
            {
                prop: 'userId',

                // 单元格里是用户、角色或固定范围，实际可操作的字段由单元格里的 data-query-* 指定，这里的声明只作兜底
                queryField: 'UserId',
                queryValueType: 'number',
                label: t('rateLimit.fields.subject'),
                minWidth: 140,
                align: 'center',
                formatter: (row: RateLimitRuleInfo) => {
                    const text = subjectText(row)
                    const query = subjectQuery(row)
                    const attrs = {
                        'data-query-field': query.field,
                        'data-query-label': query.label,
                        'data-query-value': query.value,
                        'data-query-value-type': query.valueType,
                    }

                    // 全局和每个用户是固定语义，用默认标签；指定的用户名、角色名是动态内容，按文字 Hash 配色
                    return isFixedSubject(row)
                        ? h(ElTag, { size: 'small', effect: 'light', type: 'info', ...attrs }, () => text)
                        : h(ElTag, { size: 'small', effect: 'light', style: getHashTagStyle(text), ...attrs }, () => text)
                },
            },
            {
                prop: 'limit',
                queryField: 'Limit',
                queryValueType: 'number',
                label: t('rateLimit.fields.rate'),
                width: 140,
                align: 'right',
                sortable: true,
                formatter: (row: RateLimitRuleInfo) => t('rateLimit.rate', { limit: row.limit, seconds: row.windowSeconds }),
            },
            {
                prop: 'remark',
                queryField: 'Remark',
                queryValueType: 'string',
                label: t('rateLimit.fields.remark'),
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
                formatter: (row: RateLimitRuleInfo) =>
                    h(ArtEnabledSwitch, {
                        id: String(row.id),
                        resource: 'rate-limit',
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
const rowActions = (row: RateLimitRuleInfo): RowAction[] => {
    const actions: RowAction[] = [
        { key: 'edit', dblclick: true, label: t('rateLimit.actions.edit'), icon: 'ri:pencil-line', onClick: () => openDialog(row) },
    ]
    if (hasAuth('delete')) {
        actions.push({
            key: 'delete',
            label: t('common.rowAction.delete'),
            icon: 'ri:delete-bin-5-line',
            danger: true,
            confirm: t('rateLimit.delete.confirm'),
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
<template>
    <div class="art-full-height art-table-page">
        <template v-if="pageReady">
            <ArtSearchBar
                v-model="formModel"
                :advanced-query-fields="advancedQueryFields"
                :filter-fields="filterFields"
                :filter-groups="filterGroups"
                :group-selections="groupSelections"
                :items="[]"
                :reserve-filter-groups="reserveFilterGroups || groupedFields.size > 0"
                :reserve-group-count="Math.max(groupedFields.size, 1)"
                @group-select="handleGroupSelect"
                @reset="handleReset"
                @search="handleSearch"
                ref="searchBarRef" />

            <ElCard class="art-table-card">
                <ArtTableHeader
                    v-model:columns="columnChecksModel"
                    :column-storage-key="`resource-${resource}`"
                    :loading="loading"
                    @refresh="emit('refresh')">
                    <template #left>
                        <!-- 页面按钮与统一导出按钮之间保持间距 -->
                        <div class="flex flex-wrap items-center gap-2">
                            <slot name="header-left" />
                            <!-- 统一导出：每个列表页都有；页面可通过 exportable=false 关闭 -->
                            <ElDropdown v-if="exportEnabled" :disabled="exporting" @command="handleExport" class="art-table-page-export">
                                <ElButton :loading="exporting">
                                    <ArtSvgIcon class="mr-1" icon="ri:download-2-line" />{{ t('table.export.title') }}
                                    <ArtSvgIcon class="ml-1" icon="ri:arrow-down-s-line" />
                                </ElButton>
                                <template #dropdown>
                                    <ElDropdownMenu>
                                        <ElDropdownItem command="excel">{{ t('table.export.excel') }}</ElDropdownItem>
                                        <ElDropdownItem command="json">{{ t('table.export.json') }}</ElDropdownItem>
                                    </ElDropdownMenu>
                                </template>
                            </ElDropdown>
                        </div>
                    </template>
                    <template #right><slot name="header-right" /></template>
                </ArtTableHeader>

                <ArtTable
                    v-bind="tableProps"
                    :columns="columns"
                    :data="data"
                    :loading="loading"
                    :pagination="pagination"
                    :row-actions="rowActions"
                    :row-key="rowKey"
                    :tree-props="treeProps"
                    @cell-query="handleCellQuery"
                    @pagination:current-change="emit('page-change', $event)"
                    @pagination:size-change="emit('size-change', $event)"
                    @selection-change="handleSelectionChange"
                    @sort-change="handleSortChange"
                    ref="tableRef" />

                <slot />
            </ElCard>
        </template>
        <div v-else v-loading="true" class="art-table-page-placeholder" />
    </div>
</template>

<script lang="ts" setup>
import {
    fetchGetListFilterFields,
    fetchGetListFilterGroups,
    type ListFilterField,
    type ListFilterGroup,
    type ListFilterResource,
} from '@/api/system-manage'
import type { DynamicFilter, DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import type { ColumnOption, RowAction } from '@/types/component'
import { useI18n } from 'vue-i18n'
import { resolveBasicOperator } from '@/utils/json/dynamic-query-value'
import { ElMessage } from 'element-plus'
import { useRouteAuth } from '@/hooks/core/useRouteAuth'
import request from '@/utils/http'
import { downloadExcel, downloadJson, exportFileName, type ExportColumn, type ExportFormat } from '@/utils/table/export'

/** 导出请求：与列表查询一致的筛选和排序，不带分页 */
interface DynamicExportQuery {
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
}

defineOptions({ name: 'ArtTablePage' })

interface PaginationState {
    current: number
    size: number
    total: number
}

interface Props {
    resource: ListFilterResource
    columns?: ColumnOption[]
    columnChecks?: ColumnOption[]
    data?: any[]
    loading?: boolean
    pagination?: PaginationState
    defaultFilter?: DynamicFilter
    /** 首次加载使用的筛选，缺省为 defaultFilter；重置仍恢复 defaultFilter */
    initialFilter?: DynamicFilter
    filterFields?: ListFilterField[]
    filterGroupsFn?: (dynamicFilter?: DynamicFilter) => Promise<ListFilterGroup[]>
    treeProps?: Record<string, unknown>
    rowKey?: string
    filterGroupsLoader?: (dynamicFilter?: DynamicFilter) => Promise<ListFilterGroup[]>
    filterGroupMaxOptions?: number
    reserveFilterGroups?: boolean
    tableProps?: Record<string, unknown>
    /** 行右键菜单操作，替代表格操作列 */
    rowActions?: (row: any) => RowAction[]
    /** 导出接口：未勾选行时按当前查询条件和排序调用，由后端按系统设置的上限截取；缺省时按 resource 调用 /api/{resource}/export */
    exportFn?: (query: DynamicExportQuery) => Promise<{ records: any[]; limit: number; total: number }>
    /** 是否显示导出按钮，默认显示 */
    exportable?: boolean
    /** 导出的当前排序，由页面传入 useTable 的 searchParams；缺省时取表头排序事件记录的值 */
    exportSort?: { sortField?: string; sortOrder?: 'asc' | 'desc' }
}

const props = withDefaults(defineProps<Props>(), {
    columns: () => [],
    columnChecks: () => [],
    data: () => [],
    loading: false,
    pagination: () => ({ current: 1, size: 20, total: 0 }),
    treeProps: undefined,
    rowKey: undefined,
    reserveFilterGroups: false,
    tableProps: () => ({}),
    exportFn: undefined,
    exportable: true,
    exportSort: undefined,
})

const emit = defineEmits<{
    (e: 'update:columnChecks', value: ColumnOption[]): void
    (e: 'filter-change', value: DynamicFilter | undefined): void
    (e: 'reset'): void
    (e: 'refresh'): void
    (e: 'page-change', value: number): void
    (e: 'size-change', value: number): void
    (e: 'sort-change', value: { prop?: string; order?: 'ascending' | 'descending' | null }): void
    (e: 'selection-change', value: any[]): void
}>()

const { t, locale } = useI18n()
const searchBarRef = ref<{ setDynamicFilter?: (filter: DynamicFilter | undefined) => void }>()
const tableRef = ref()
const cloneFilter = (filter: DynamicFilter | undefined): DynamicFilter | undefined =>
    filter ? (JSON.parse(JSON.stringify(filter)) as DynamicFilter) : undefined
// 将默认动态筛选放入初始模型，确保搜索栏首屏预览即显示完整 dynamicFilter 树。
const formModel = ref<Record<string, unknown>>(props.defaultFilter ? { dynamicFilter: cloneFilter(props.initialFilter ?? props.defaultFilter) } : {})
const allFilterFields = ref<ListFilterField[]>(props.filterFields || [])
const filterFields = ref<ListFilterField[]>(allFilterFields.value.filter((field) => field.isVisible !== false))
const filterGroups = ref<ListFilterGroup[]>([])
const groupSelectionOverrides = ref<Record<string, unknown>>({})
const groupLoading = ref(false)
const pageReady = ref(props.filterFields !== undefined)
const currentFilter = ref<DynamicFilter>()
const residualFilter = ref<DynamicFilter>()
let groupRequestId = 0

const columnChecksModel = computed({
    get: () => props.columnChecks,
    set: (value) => emit('update:columnChecks', value),
})

const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    allFilterFields.value.map((field) => ({
        field: field.field,
        label: typeof field.label === 'string' ? t(field.label) : String(field.label ?? ''),
        type: field.valueType,
    })),
)
const groupedFields = computed(() => new Set(filterFields.value.filter((field) => field.groupCount).map((field) => field.field)))
const groupSelections = computed<Record<string, unknown>>(() => {
    const selections: Record<string, unknown> = {}
    const collect = (filter: DynamicFilter | undefined): void => {
        if (!filter) return
        if (filter.field && groupedFields.value.has(filter.field) && filter.operator?.toLowerCase() === 'equal') {
            selections[filter.field] = filter.value
        }
        filter.filters?.forEach(collect)
    }
    // 以当前动态筛选树为唯一状态来源，确保分组按钮在查询完成后仍保持选中
    collect(currentFilter.value || (formModel.value.dynamicFilter as DynamicFilter | undefined))
    return { ...selections, ...groupSelectionOverrides.value }
})

// 动态查询本身是 JSON 协议；通过序列化克隆可同时移除任意层级的 Vue 响应式代理。
const fieldMetadata = (field: string): ListFilterField | undefined =>
    allFilterFields.value.find((item) => item.field === field || (field === 'UpdatedAt' && item.field === 'CreatedAt'))

const defaultOperator = (field: ListFilterField, value: unknown): string => resolveBasicOperator(field, value)

const combineFilters = (filters: Array<DynamicFilter | undefined>): DynamicFilter | undefined => {
    const values = filters.filter((filter): filter is DynamicFilter => Boolean(filter))
    if (values.length === 0) return undefined
    if (values.length === 1) return values[0]
    return { logic: 'And', filters: values }
}

const removeFieldFilter = (filter: DynamicFilter | undefined, field: string): DynamicFilter | undefined => {
    if (!filter || filter.field?.toLowerCase() === field.toLowerCase()) return undefined
    if (!filter.filters?.length) return cloneFilter(filter)
    const filters = filter.filters.map((child) => removeFieldFilter(child, field)).filter((child): child is DynamicFilter => Boolean(child))
    if (filters.length === 0) return undefined
    if (filters.length === 1) return filters[0]
    return { logic: filter.logic || 'And', filters }
}

const refreshFilterGroups = async (filter: DynamicFilter | undefined): Promise<void> => {
    if (!filterFields.value.some((field) => field.groupCount)) {
        filterGroups.value = []
        return
    }
    const requestId = ++groupRequestId
    groupLoading.value = true
    try {
        const groups = props.filterGroupsLoader
            ? await props.filterGroupsLoader(cloneFilter(filter))
            : props.filterGroupsFn
              ? await props.filterGroupsFn(cloneFilter(filter))
              : await fetchGetListFilterGroups(props.resource, cloneFilter(filter), props.filterGroupMaxOptions)
        if (requestId === groupRequestId) filterGroups.value = groups
    } finally {
        if (requestId === groupRequestId) groupLoading.value = false
    }
}

const decomposeFilter = (filter: DynamicFilter | undefined): { fields: Record<string, unknown>; residual?: DynamicFilter } => {
    if (!filter) return { fields: {} }
    if (filter.field && filter.operator) {
        const metadata = fieldMetadata(filter.field)
        if (metadata && filter.operator === defaultOperator(metadata, filter.value)) {
            return { fields: { [filter.field]: filter.value } }
        }
        return { fields: {}, residual: cloneFilter(filter) }
    }
    if (filter.logic?.toLowerCase() !== 'or' && filter.filters?.length) {
        const fields: Record<string, unknown> = {}
        const residuals: DynamicFilter[] = []
        filter.filters.forEach((child) => {
            const result = decomposeFilter(child)
            Object.assign(fields, result.fields)
            if (result.residual) residuals.push(result.residual)
        })
        return { fields, residual: combineFilters(residuals) }
    }
    return { fields: {}, residual: cloneFilter(filter) }
}

const buildBasicFilter = (values: Record<string, unknown>): DynamicFilter | undefined => {
    const filters = filterFields.value.flatMap<DynamicFilter>((field) => {
        const fieldName = field.field === 'CreatedAt' && values.UpdatedAt !== undefined ? 'UpdatedAt' : field.field
        const value = values[fieldName]
        if (value === undefined || value === null || value === '' || (Array.isArray(value) && value.length === 0)) return []
        return [{ field: fieldName, operator: defaultOperator(field, value), value }]
    })
    return combineFilters(filters)
}

const synchronizeFilter = (filter: DynamicFilter | undefined): void => {
    currentFilter.value = cloneFilter(filter)
    const decomposition = decomposeFilter(filter)
    residualFilter.value = decomposition.residual
    // 保留完整动态筛选树，供搜索栏首屏及后续预览使用；基础字段仍由分解结果驱动。
    formModel.value = { ...decomposition.fields, ...(filter ? { dynamicFilter: cloneFilter(filter) } : {}) }
    searchBarRef.value?.setDynamicFilter?.(cloneFilter(filter))
    // 不等待分组统计，失败只记录日志，避免未处理的 Promise 异常
    refreshFilterGroups(filter).catch((error) => console.error('[ArtTablePage] 筛选分组加载失败:', error))
}

const handleSearch = (values: Record<string, unknown>): void => {
    const filter = values.dynamicFilter ? (values.dynamicFilter as DynamicFilter) : combineFilters([residualFilter.value, buildBasicFilter(values)])
    groupSelectionOverrides.value = {}
    synchronizeFilter(filter)
    emit('filter-change', cloneFilter(filter))
}

const handleCellQuery = (condition: DynamicFilter): void => {
    const filter = combineFilters([currentFilter.value, condition])
    synchronizeFilter(filter)
    emit('filter-change', cloneFilter(filter))
}

const handleGroupSelect = (field: string, value: unknown): void => {
    if (value === undefined) {
        const next = { ...groupSelectionOverrides.value }
        delete next[field]
        groupSelectionOverrides.value = next
    } else {
        groupSelectionOverrides.value = { ...groupSelectionOverrides.value, [field]: value }
    }
    const baseFilter = removeFieldFilter(currentFilter.value, field)
    const filter = value === undefined ? baseFilter : combineFilters([baseFilter, { field, operator: 'Equal', value }])
    synchronizeFilter(filter)
    emit('filter-change', cloneFilter(filter))
}

const handleReset = (): void => {
    groupSelectionOverrides.value = {}
    synchronizeFilter(props.defaultFilter)
    emit('reset')
}

onMounted(async () => {
    try {
        allFilterFields.value = props.filterFields || (await fetchGetListFilterFields(props.resource))
        filterFields.value = allFilterFields.value.filter((field) => field.isVisible !== false)
        await nextTick()
        // 分组统计比列表查询慢，synchronizeFilter 内部已异步触发加载，这里不能等待，否则页面要等全部分组返回才显示；
        // 分组区域由 reserveFilterGroups 预留高度，加载完成后原地填充，不会造成布局跳动
        synchronizeFilter(props.initialFilter ?? props.defaultFilter)
    } catch (error) {
        console.error('[ArtTablePage] 筛选元数据加载失败:', error)
    } finally {
        pageReady.value = true
    }
})

watch(
    () => props.filterFields,
    (fields) => {
        if (fields) {
            allFilterFields.value = fields
            filterFields.value = fields.filter((field) => field.isVisible !== false)
            synchronizeFilter(currentFilter.value || props.defaultFilter)
        }
    },
)

/**
 * 由外部（如路由参数）设置筛选：同步搜索栏和筛选状态，不触发查询，由调用方自行刷新数据
 * @param filter 完整的动态筛选树
 */
const setFilter = (filter: DynamicFilter | undefined): void => {
    groupSelectionOverrides.value = {}
    synchronizeFilter(filter)
}

const exporting = ref(false)
const selectedRows = ref<any[]>([])
const lastSort = ref<{ sortField?: string; sortOrder?: 'asc' | 'desc' }>({})
const { hasAuth } = useRouteAuth()

/** 导出按钮：页面未关闭导出，且当前角色在该菜单下被授予了 export 按钮权限 */
const exportEnabled = computed(() => props.exportable && hasAuth('export'))

/** 记录表头排序，供导出使用，同时把事件继续抛给页面 */
const handleSortChange = (value: { prop?: string; order?: 'ascending' | 'descending' | null }): void => {
    lastSort.value = value.prop && value.order ? { sortField: value.prop, sortOrder: value.order === 'descending' ? 'desc' : 'asc' } : {}
    emit('sort-change', value)
}

/** 记录勾选行，同时把事件继续抛给页面 */
const handleSelectionChange = (rows: any[]): void => {
    selectedRows.value = rows
    emit('selection-change', rows)
}

/** 默认导出接口：POST /api/{resource}/export */
const defaultExportFn = (query: DynamicExportQuery) =>
    request.post<{ records: any[]; limit: number; total: number }>({ url: `/api/${props.resource}/export`, data: query })

/** 导出列：当前可见的数据列，操作列、选择列、展开列、序号列和 exportable=false 的列不导出 */
const exportColumns = computed<ExportColumn[]>(() =>
    props.columns
        .filter((column) => !column.type && column.prop && column.exportable !== false && column.queryField !== false)
        .map((column) => ({
            key: column.exportKey ?? String(column.prop),
            title: String(column.label ?? column.prop),
            value: (row: any) => (column.exportValue ? column.exportValue(row) : row[column.prop as string]),
        })),
)

/** 取导出数据：勾选行则仅导出勾选，未勾选则调用后端 export 接口导出当前查询条件和排序下的结果 */
const loadExportRows = async (): Promise<any[]> => {
    if (selectedRows.value.length > 0) return selectedRows.value
    const sort = props.exportSort ?? lastSort.value
    const result = await (props.exportFn ?? defaultExportFn)({
        dynamicFilter: cloneFilter(currentFilter.value),
        sortField: sort.sortField,
        sortOrder: sort.sortOrder,
    })
    if (result.total > result.records.length) {
        ElMessage.warning(t('table.export.truncated', { total: result.total, limit: result.limit }))
    }
    return result.records
}

/** 导出入口：防重复、空结果提示、失败提示 */
const handleExport = async (format: ExportFormat): Promise<void> => {
    if (exporting.value) return
    exporting.value = true
    try {
        const rows = await loadExportRows()
        if (rows.length === 0 || exportColumns.value.length === 0) {
            ElMessage.info(t('table.export.empty'))
            return
        }
        const labels = { yes: t('listFilter.option.yes'), no: t('listFilter.option.no') }
        const fileName = exportFileName(props.resource, format)
        if (format === 'excel') downloadExcel(rows, exportColumns.value, fileName, props.resource, locale.value, labels)
        else downloadJson(rows, exportColumns.value, fileName, locale.value, labels)
        ElMessage.success(t('table.export.done', { count: rows.length }))
    } catch (error) {
        console.error('[ArtTablePage] 导出失败:', error)
        ElMessage.error(t('table.export.failed'))
    } finally {
        exporting.value = false
    }
}

defineExpose({ tableRef, refreshFilterGroups, setFilter })
</script>

<style scoped>
.art-table-page-placeholder {
    position: relative;
    height: 100%;
    min-height: 420px;
}
</style>
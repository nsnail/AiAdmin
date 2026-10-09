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
                    <template #left><slot name="header-left" /></template>
                    <template #right><slot name="header-right" /></template>
                </ArtTableHeader>

                <ArtTable
                    v-bind="tableProps"
                    :columns="columns"
                    :data="data"
                    :loading="loading"
                    :pagination="pagination"
                    :row-key="rowKey"
                    :tree-props="treeProps"
                    @cell-query="handleCellQuery"
                    @pagination:current-change="emit('page-change', $event)"
                    @pagination:size-change="emit('size-change', $event)"
                    @selection-change="emit('selection-change', $event)"
                    @sort-change="emit('sort-change', $event)"
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
import type { ColumnOption } from '@/types/component'
import { useI18n } from 'vue-i18n'

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
    filterFields?: ListFilterField[]
    filterGroupsFn?: (dynamicFilter?: DynamicFilter) => Promise<ListFilterGroup[]>
    treeProps?: Record<string, unknown>
    rowKey?: string
    filterGroupsLoader?: (dynamicFilter?: DynamicFilter) => Promise<ListFilterGroup[]>
    filterGroupMaxOptions?: number
    reserveFilterGroups?: boolean
    tableProps?: Record<string, unknown>
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

const { t } = useI18n()
const searchBarRef = ref<{ setDynamicFilter?: (filter: DynamicFilter | undefined) => void }>()
const tableRef = ref()
const cloneFilter = (filter: DynamicFilter | undefined): DynamicFilter | undefined =>
    filter ? (JSON.parse(JSON.stringify(filter)) as DynamicFilter) : undefined
// 将默认动态筛选放入初始模型，确保搜索栏首屏预览即显示完整 dynamicFilter 树。
const formModel = ref<Record<string, unknown>>(props.defaultFilter ? { dynamicFilter: cloneFilter(props.defaultFilter) } : {})
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

const defaultOperator = (field: ListFilterField, value: unknown): string => {
    if (Array.isArray(value)) return field.valueType === 'date' ? 'DateRange' : 'Any'
    if (field.control === 'select' || field.valueType === 'boolean' || field.valueType === 'number') return 'Equal'
    return field.valueType === 'date' ? 'DateRange' : 'Contains'
}

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
    void refreshFilterGroups(filter)
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
        synchronizeFilter(props.defaultFilter)
        await refreshFilterGroups(currentFilter.value)
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

defineExpose({ tableRef, refreshFilterGroups })
</script>

<style scoped>
.art-table-page-placeholder {
    position: relative;
    height: 100%;
    min-height: 420px;
}
</style>
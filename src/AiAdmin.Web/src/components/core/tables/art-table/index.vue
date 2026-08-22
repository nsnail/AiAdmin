<!-- 表格组件 -->
<!-- 支持：el-table 全部属性、事件、插槽，同官方文档写法 -->
<!-- 扩展功能：分页组件、渲染自定义列、loading、表格全局边框、斑马纹、表格尺寸、表头背景配置 -->
<!-- 获取 ref：默认暴露了 elTableRef 外部通过 ref.value.elTableRef 可以调用 el-table 方法 -->
<template>
    <div :class="{ 'is-empty': isEmpty }" :style="containerHeight" class="art-table">
        <ElTable
            v-bind="mergedTableProps"
            v-loading="!!loading"
            @cell-contextmenu="handleCellContextMenu"
            @sort-change="handleSortChange"
            ref="elTableRef">
            <template v-for="col in columns" :key="col.prop || col.type">
                <!-- 渲染全局序号列 -->
                <ElTableColumn v-bind="{ ...col }" v-if="col.type === 'globalIndex'">
                    <template #default="{ $index }">
                        <span>{{ getGlobalIndex($index) }}</span>
                    </template>
                </ElTableColumn>

                <!-- 渲染展开行 -->
                <ElTableColumn v-bind="cleanColumnProps(col)" v-else-if="col.type === 'expand'">
                    <template #default="{ row }">
                        <component :is="col.formatter ? col.formatter(row) : null" />
                    </template>
                </ElTableColumn>

                <!-- 渲染普通列 -->
                <ElTableColumn v-bind="cleanColumnProps(col)" v-else>
                    <template v-if="col.useHeaderSlot && col.prop" #header="headerScope">
                        <slot v-bind="{ ...headerScope, prop: col.prop, label: col.label }" :name="col.headerSlotName || `${col.prop}-header`">
                            {{ col.label }}
                        </slot>
                    </template>
                    <template v-if="col.useSlot && col.prop" #default="slotScope">
                        <slot
                            v-bind="{
                                ...slotScope,
                                prop: col.prop,
                                value: col.prop ? slotScope.row[col.prop] : undefined,
                            }"
                            v-if="shouldRenderSlotScope(slotScope)"
                            :name="col.slotName || col.prop" />
                    </template>
                </ElTableColumn>
            </template>

            <template v-if="$slots.default" #default><slot /></template>

            <template #empty>
                <div v-if="loading"></div>
                <ElEmpty v-else :description="emptyText" :image-size="120" />
            </template>
        </ElTable>

        <div v-if="showPagination" :class="mergedPaginationOptions?.align" class="pagination custom-pagination" ref="paginationRef">
            <ElPagination
                v-bind="mergedPaginationOptions"
                :current-page="pagination?.current"
                :disabled="loading"
                :page-size="pagination?.size"
                :total="pagination?.total"
                @current-change="handleCurrentChange"
                @size-change="handleSizeChange" />
        </div>

        <Teleport to="body">
            <div v-if="queryMenu.visible" :style="{ left: `${queryMenu.x}px`, top: `${queryMenu.y}px` }" @contextmenu.prevent class="cell-query-menu">
                <button @click="copyQueryValue" class="cell-query-title" type="button">
                    <span>{{ queryMenu.label }}</span>
                    <span class="cell-query-copy-hint">点击复制</span>
                </button>
                <button
                    v-for="operator in availableOperators"
                    :key="operator.value"
                    @click="openQueryDialog(operator.value)"
                    class="cell-query-operation"
                    type="button">
                    <span>{{ operator.label }}</span>
                    <span class="cell-query-symbol">{{ operator.symbol }}</span>
                </button>
                <div v-if="queryMenu.sortable" :class="{ 'opens-left': queryMenu.submenuLeft }" @click.stop class="cell-query-submenu">
                    <button @click.stop class="cell-query-operation cell-query-submenu-trigger" type="button">
                        <span>排序</span>
                        <span class="cell-query-symbol">›</span>
                    </button>
                    <div class="cell-query-submenu-panel">
                        <button @click="applyContextSort('ascending')" class="cell-query-operation" type="button">
                            <span>顺序</span><span class="cell-query-symbol">↑</span>
                        </button>
                        <button @click="applyContextSort('descending')" class="cell-query-operation" type="button">
                            <span>倒序</span><span class="cell-query-symbol">↓</span>
                        </button>
                    </div>
                </div>
            </div>
        </Teleport>

        <ElDialog v-model="queryDialogVisible" append-to-body title="添加查询条件" width="420px">
            <ElForm label-width="80px">
                <ElFormItem label="字段">{{ queryMenu.label }}</ElFormItem>
                <ElFormItem label="操作符">{{ selectedOperatorLabel }}</ElFormItem>
                <ElFormItem label="值">
                    <ElDatePicker
                        v-if="queryOperator === 'DateRange'"
                        v-model="queryRangeValue"
                        class="w-full"
                        end-placeholder="结束时间"
                        start-placeholder="开始时间"
                        type="datetimerange"
                        value-format="YYYY-MM-DDTHH:mm:ss" />
                    <div v-else-if="queryOperator === 'Range'" class="query-range-inputs">
                        <ElInput v-model="queryRangeValue[0]" inputmode="decimal" placeholder="最小值" />
                        <span>至</span>
                        <ElInput v-model="queryRangeValue[1]" inputmode="decimal" placeholder="最大值" />
                    </div>
                    <ElInput
                        v-else-if="queryOperator === 'Any' || queryOperator === 'NotAny'"
                        v-model="queryValue"
                        :rows="4"
                        clearable
                        placeholder="多个值使用逗号分隔"
                        resize="vertical"
                        type="textarea" />
                    <ElSelect v-else-if="queryMenu.valueType === 'boolean'" v-model="queryValue" class="w-full" filterable>
                        <ElOption :value="true" label="是" />
                        <ElOption :value="false" label="否" />
                    </ElSelect>
                    <ElInput v-else-if="queryMenu.valueType === 'number'" v-model="queryValue" clearable inputmode="decimal" />
                    <ElDatePicker
                        v-else-if="queryMenu.valueType === 'date'"
                        v-model="queryValue"
                        class="w-full"
                        type="datetime"
                        value-format="YYYY-MM-DDTHH:mm:ss" />
                    <ElInput v-else v-model="queryValue" :rows="4" clearable resize="vertical" type="textarea" />
                </ElFormItem>
            </ElForm>
            <template #footer>
                <ElButton @click="queryDialogVisible = false">取消</ElButton>
                <ElButton @click="applyCellQuery" type="primary">确定</ElButton>
            </template>
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { ref, reactive, computed, nextTick, watchEffect, getCurrentInstance, useAttrs, onMounted, onUnmounted } from 'vue'
import type { ElTable, TableProps } from 'element-plus'
import { storeToRefs } from 'pinia'
import { ColumnOption } from '@/types'
import { useTableStore } from '@/store/modules/table'
import { useCommon } from '@/hooks/core/useCommon'
import { useTableHeight } from '@/hooks/core/useTableHeight'
import { useResizeObserver, useWindowSize } from '@vueuse/core'

defineOptions({ name: 'ArtTable' })

const { width } = useWindowSize()
const elTableRef = ref<InstanceType<typeof ElTable> | null>(null)
const paginationRef = ref<HTMLElement>()
const tableHeaderRef = ref<HTMLElement>()
const tableStore = useTableStore()
const { isBorder, isZebra, tableSize, isFullScreen, isHeaderBackground } = storeToRefs(tableStore)
type QueryValueType = 'string' | 'number' | 'boolean' | 'date'
const queryMenu = reactive({
    visible: false,
    x: 0,
    y: 0,
    label: '',
    field: '',
    sortField: '',
    sortable: false,
    submenuLeft: false,
    valueType: 'string' as QueryValueType,
    operators: undefined as string[] | undefined,
    initialValue: undefined as unknown,
})
const queryDialogVisible = ref(false)
const queryOperator = ref('Contains')
const queryValue = ref<any>('')
const queryRangeValue = ref<any[]>([])
const stringOperators = [
    { label: '等于', symbol: '=', value: 'Equal' },
    { label: '不等于', symbol: '!=', value: 'NotEqual' },
    { label: '包含', symbol: '*x*', value: 'Contains' },
    { label: '不包含', symbol: '!*x*', value: 'NotContains' },
    { label: '开头是', symbol: 'x*', value: 'StartsWith' },
    { label: '不是以此开头', symbol: '!x*', value: 'NotStartsWith' },
    { label: '结尾是', symbol: '*x', value: 'EndsWith' },
    { label: '不是以此结尾', symbol: '!*x', value: 'NotEndsWith' },
    { label: '任一匹配', symbol: 'IN', value: 'Any' },
    { label: '均不匹配', symbol: 'NOT IN', value: 'NotAny' },
]
const comparableOperators = [
    { label: '等于', symbol: '=', value: 'Equal' },
    { label: '不等于', symbol: '!=', value: 'NotEqual' },
    { label: '大于', symbol: '>', value: 'GreaterThan' },
    { label: '大于等于', symbol: '>=', value: 'GreaterThanOrEqual' },
    { label: '小于', symbol: '<', value: 'LessThan' },
    { label: '小于等于', symbol: '<=', value: 'LessThanOrEqual' },
    { label: '范围', symbol: 'a <= x <= b', value: 'Range' },
    { label: '任一匹配', symbol: 'IN', value: 'Any' },
    { label: '均不匹配', symbol: 'NOT IN', value: 'NotAny' },
]
const dateOperators = [
    ...comparableOperators.filter((item) => item.value !== 'Range'),
    { label: '日期范围', symbol: 'a <= x < b', value: 'DateRange' },
]
const booleanOperators = [
    { label: '等于', symbol: '=', value: 'Equal' },
    { label: '不等于', symbol: '!=', value: 'NotEqual' },
    { label: '任一匹配', symbol: 'IN', value: 'Any' },
    { label: '均不匹配', symbol: 'NOT IN', value: 'NotAny' },
]
const availableOperators = computed(() =>
    (queryMenu.valueType === 'string'
        ? stringOperators
        : queryMenu.valueType === 'boolean'
          ? booleanOperators
          : queryMenu.valueType === 'date'
            ? dateOperators
            : comparableOperators
    ).filter((operator) => !queryMenu.operators || queryMenu.operators.includes(operator.value)),
)
const selectedOperatorLabel = computed(() => {
    const operator = availableOperators.value.find((item) => item.value === queryOperator.value)
    return operator ? `${operator.label} ${operator.symbol}` : ''
})

/** 分页配置接口 */
interface PaginationConfig {
    /** 当前页码 */
    current: number
    /** 每页显示条目个数 */
    size: number
    /** 总条目数 */
    total: number
}

/** 分页器配置选项接口 */
interface PaginationOptions {
    /** 每页显示个数选择器的选项列表 */
    pageSizes?: number[]
    /** 分页器的对齐方式 */
    align?: 'left' | 'center' | 'right'
    /** 分页器的布局 */
    layout?: string
    /** 是否显示分页器背景 */
    background?: boolean
    /** 只有一页时是否隐藏分页器 */
    hideOnSinglePage?: boolean
    /** 分页器的大小 */
    size?: 'small' | 'default' | 'large'
    /** 分页器的页码数量 */
    pagerCount?: number
}

/** ArtTable 组件的 Props 接口 */
interface ArtTableProps extends /* @vue-ignore */ TableProps<Record<string, any>> {
    /** 加载状态 */
    loading?: boolean
    /** 列渲染配置 */
    columns?: ColumnOption[]
    /** 分页状态 */
    pagination?: PaginationConfig
    /** 分页配置 */
    paginationOptions?: PaginationOptions
    /** 空数据表格高度 */
    emptyHeight?: string
    /** 空数据时显示的文本 */
    emptyText?: string
    /** 是否开启 ArtTableHeader，解决表格高度自适应问题 */
    showTableHeader?: boolean
    /** 空数据时是否仍显示分页器 */
    showPaginationWhenEmpty?: boolean
}

const props = withDefaults(defineProps<ArtTableProps>(), {
    columns: () => [],
    fit: true,
    showHeader: true,
    stripe: undefined,
    border: undefined,
    size: undefined,
    emptyHeight: '100%',
    emptyText: '暂无数据',
    showTableHeader: true,
})
const instance = getCurrentInstance()
const attrs = useAttrs()

const LAYOUT = {
    MOBILE: 'prev, pager, next, sizes, jumper, total',
    IPAD: 'prev, pager, next, jumper, total',
    DESKTOP: 'total, prev, pager, next, sizes, jumper',
}

const layout = computed(() => {
    if (width.value < 768) {
        return LAYOUT.MOBILE
    } else if (width.value < 1024) {
        return LAYOUT.IPAD
    } else {
        return LAYOUT.DESKTOP
    }
})

// 默认分页常量
const DEFAULT_PAGINATION_OPTIONS: PaginationOptions = {
    pageSizes: [10, 20, 30, 50, 100],
    align: 'center',
    background: true,
    layout: layout.value,
    hideOnSinglePage: false,
    size: 'default',
    pagerCount: width.value > 1200 ? 7 : 5,
}

// 合并分页配置
const mergedPaginationOptions = computed(() => ({
    ...DEFAULT_PAGINATION_OPTIONS,
    ...props.paginationOptions,
}))

// 边框 (优先级：props > store)
const border = computed(() => props.border ?? isBorder.value)
// 斑马纹
const stripe = computed(() => props.stripe ?? isZebra.value)
// 表格尺寸
const size = computed(() => props.size ?? tableSize.value)
// 数据是否为空
const isEmpty = computed(() => props.data?.length === 0)

const paginationHeight = ref(0)
const tableHeaderHeight = ref(0)

// 使用 useResizeObserver 监听分页器高度变化
useResizeObserver(paginationRef, (entries) => {
    const entry = entries[0]
    if (entry) {
        // 使用 requestAnimationFrame 避免 ResizeObserver loop 警告
        requestAnimationFrame(() => {
            paginationHeight.value = entry.contentRect.height
        })
    }
})

// 使用 useResizeObserver 监听表格头部高度变化
useResizeObserver(tableHeaderRef, (entries) => {
    const entry = entries[0]
    if (entry) {
        // 使用 requestAnimationFrame 避免 ResizeObserver loop 警告
        requestAnimationFrame(() => {
            tableHeaderHeight.value = entry.contentRect.height
        })
    }
})

// 分页器与表格之间的间距常量（计算属性，响应 showTableHeader 变化）
const PAGINATION_SPACING = computed(() => (props.showTableHeader ? 6 : 15))

// 使用表格高度计算 Hook
const { containerHeight } = useTableHeight({
    showTableHeader: computed(() => props.showTableHeader),
    paginationHeight,
    tableHeaderHeight,
    paginationSpacing: PAGINATION_SPACING,
})

// 表格高度逻辑
const height = computed(() => {
    // 全屏模式下占满全屏
    if (isFullScreen.value) return '100%'
    // 空数据且非加载状态时固定高度
    if (isEmpty.value && !props.loading && !props.showPaginationWhenEmpty) return props.emptyHeight
    // 使用传入的高度
    if (props.height) return props.height
    // 默认占满容器高度
    return '100%'
})

// 表头背景颜色样式
const headerCellStyle = computed(() => ({
    background: isHeaderBackground.value ? 'var(--el-fill-color-lighter)' : 'var(--default-box-color)',
    ...(props.headerCellStyle || {}), // 合并用户传入的样式
}))

// 只有显式传入时才覆盖 ElTable 的原生默认值，避免继承的 Boolean props 把官方默认值冲掉。
const hasExplicitTableProp = (propName: string): boolean => {
    const rawProps = (instance?.vnode.props || {}) as Record<string, unknown>
    const kebabName = propName.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`)
    return propName in rawProps || kebabName in rawProps
}

const mergedTableProps = computed(() => ({
    ...attrs,
    ...props,
    height: height.value,
    stripe: stripe.value,
    border: border.value,
    size: size.value,
    headerCellStyle: headerCellStyle.value,
    // Element Plus 默认值为 true，未显式传入时不应被 ArtTable 覆盖成 false。
    selectOnIndeterminate: hasExplicitTableProp('selectOnIndeterminate') ? props.selectOnIndeterminate : undefined,
}))

// 是否显示分页器
const showPagination = computed(() => !!props.pagination && (!isEmpty.value || props.showPaginationWhenEmpty))

// Element Plus 在部分场景会先用 $index = -1 进行预渲染。
// 这对普通展示无影响，但会让 ElForm 错误注册出 lineList.-1.xxx 这类字段。
const shouldRenderSlotScope = (slotScope: { $index?: number }) => {
    return slotScope.$index === undefined || slotScope.$index >= 0
}

// 清理列属性，移除插槽相关的自定义属性，确保它们不会被 ElTableColumn 错误解释
const cleanColumnProps = (col: ColumnOption) => {
    const columnProps = { ...col }
    // 删除自定义的插槽控制属性
    delete columnProps.useHeaderSlot
    delete columnProps.headerSlotName
    delete columnProps.useSlot
    delete columnProps.slotName
    delete columnProps.queryField
    delete columnProps.queryValueField
    delete columnProps.queryValueType
    delete columnProps.queryOperators
    return columnProps
}

const getRowValue = (row: Record<string, unknown>, path: string): unknown =>
    path.split('.').reduce<unknown>((value, key) => (value && typeof value === 'object' ? (value as Record<string, unknown>)[key] : undefined), row)

const inferValueType = (value: unknown): QueryValueType => {
    if (typeof value === 'boolean') return 'boolean'
    if (typeof value === 'number') return 'number'
    return 'string'
}

const handleCellContextMenu = (
    row: Record<string, unknown>,
    column: {
        property?: string
        prop?: string
        label?: string
        queryField?: string | false
        queryValueField?: string
        queryValueType?: QueryValueType
        queryOperators?: string[]
        sortable?: boolean
    },
    _cell: HTMLElement,
    event: MouseEvent,
) => {
    const definition = props.columns.find((item) => item.prop === column.property) || {
        ...column,
        prop: column.prop || column.property,
    }
    const queryTarget = (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-query-field]')
    const hasCellQueryListener = Boolean(instance?.vnode.props?.onCellQuery)
    const hasSortListener = Boolean(instance?.vnode.props?.onSortChange)
    if ((!hasCellQueryListener && !hasSortListener) || !definition?.prop || definition.prop === 'operation' || definition.queryField === false) return
    event.preventDefault()
    const valuePath = definition.queryValueField || definition.prop
    const rowValue = getRowValue(row, valuePath)
    const targetValue = queryTarget?.dataset.queryValue
    const value = queryTarget ? targetValue : rowValue
    const targetValueType = queryTarget?.dataset.queryValueType as QueryValueType | undefined
    Object.assign(queryMenu, {
        visible: true,
        x: Math.min(event.clientX, window.innerWidth - 180),
        y: Math.min(event.clientY, window.innerHeight - 420),
        label: queryTarget?.dataset.queryLabel || definition.label || column.label || definition.prop,
        field: queryTarget?.dataset.queryField || definition.queryField || definition.prop,
        sortField: queryTarget?.dataset.queryField || definition.prop,
        sortable: hasSortListener && definition.sortable !== false,
        submenuLeft: event.clientX > window.innerWidth - 360,
        valueType: targetValueType || definition.queryValueType || inferValueType(value),
        operators: definition.queryOperators,
        initialValue: value,
    })
}

const openQueryDialog = (operator: string) => {
    queryOperator.value = operator
    queryValue.value = queryMenu.initialValue
    queryRangeValue.value = [queryMenu.initialValue, queryMenu.initialValue]
    queryMenu.visible = false
    queryDialogVisible.value = true
}

const applyCellQuery = () => {
    const value =
        queryOperator.value === 'Range' || queryOperator.value === 'DateRange'
            ? queryRangeValue.value
            : queryOperator.value === 'Any' || queryOperator.value === 'NotAny'
              ? String(queryValue.value)
                    .split(/[,，\r\n]+/)
                    .map((item) => item.trim())
                    .filter(Boolean)
              : queryValue.value
    emit('cell-query', {
        field: queryMenu.field,
        operator: queryOperator.value,
        value,
    })
    queryDialogVisible.value = false
}

const closeQueryMenu = () => {
    queryMenu.visible = false
}

/** 从单元格右键菜单触发服务端字段排序 */
const applyContextSort = (order: 'ascending' | 'descending') => {
    emit('sort-change', {
        column: null,
        prop: queryMenu.sortField,
        order,
    })
    queryMenu.visible = false
}

/** 复制右键菜单对应单元格的原始值 */
const copyQueryValue = async () => {
    const text = queryMenu.initialValue == null ? '' : String(queryMenu.initialValue)
    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text)
        } else {
            const textarea = document.createElement('textarea')
            textarea.value = text
            textarea.style.position = 'fixed'
            textarea.style.opacity = '0'
            document.body.appendChild(textarea)
            textarea.select()
            document.execCommand('copy')
            textarea.remove()
        }
    } finally {
        queryMenu.visible = false
    }
}

onMounted(() => document.addEventListener('click', closeQueryMenu))
onUnmounted(() => document.removeEventListener('click', closeQueryMenu))

// 分页大小变化
const handleSizeChange = (val: number) => {
    emit('pagination:size-change', val)
}

// 分页当前页变化
const handleCurrentChange = (val: number) => {
    emit('pagination:current-change', val)
    scrollToTop() // 页码改变后滚动到表格顶部
}

const handleSortChange = (sort: { column: unknown; prop: string; order: 'ascending' | 'descending' | null }) => {
    emit('sort-change', sort)
}

const { scrollToTop: scrollPageToTop } = useCommon()

// 滚动表格内容到顶部，并可以联动页面滚动到顶部
const scrollToTop = () => {
    nextTick(() => {
        elTableRef.value?.setScrollTop(0) // 滚动 ElTable 内部滚动条到顶部
        scrollPageToTop() // 调用公共 composable 滚动页面到顶部
    })
}

// 全局序号
const getGlobalIndex = (index: number) => {
    if (!props.pagination) return index + 1
    const { current, size } = props.pagination
    return (current - 1) * size + index + 1
}

const emit = defineEmits<{
    (e: 'pagination:size-change', val: number): void
    (e: 'pagination:current-change', val: number): void
    (
        e: 'sort-change',
        val: {
            column: unknown
            prop: string
            order: 'ascending' | 'descending' | null
        },
    ): void
    (e: 'cell-query', val: { field: string; operator: string; value: unknown }): void
}>()

// 查找并绑定表格头部元素 - 使用 VueUse 优化
const findTableHeader = () => {
    if (!props.showTableHeader) {
        tableHeaderRef.value = undefined
        return
    }

    const tableHeader = document.getElementById('art-table-header')
    if (tableHeader) {
        tableHeaderRef.value = tableHeader
    } else {
        // 如果找不到表格头部，设置为 undefined，useElementSize 会返回 0
        tableHeaderRef.value = undefined
    }
}

watchEffect(
    () => {
        // 访问响应式数据以建立依赖追踪
        void props.data?.length // 追踪数据变化
        const shouldShow = props.showTableHeader

        // 只有在需要显示表格头部时才查找
        if (shouldShow) {
            nextTick(() => {
                findTableHeader()
            })
        } else {
            // 不显示时清空引用
            tableHeaderRef.value = undefined
        }
    },
    { flush: 'post' },
)

defineExpose({
    scrollToTop,
    elTableRef,
})
</script>

<style lang="scss" scoped>
@use './style';

.cell-query-menu {
    position: fixed;
    z-index: 4000;
    width: 176px;
    max-height: 400px;
    padding: 6px;
    overflow: visible;
    color: var(--el-text-color-primary);
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: 4px;
    box-shadow: var(--el-box-shadow-light);
}

.cell-query-title {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: 7px 10px;
    overflow: hidden;
    font-size: 12px;
    text-align: left;
    color: var(--el-text-color-secondary);
    cursor: pointer;
    background: transparent;
    border: 0;
    text-overflow: ellipsis;
    white-space: nowrap;
    border-bottom: 1px solid var(--el-border-color-lighter);

    &:hover {
        color: var(--el-color-primary);
        background: var(--el-fill-color-light);
    }
}

.cell-query-copy-hint {
    flex: none;
    margin-left: 8px;
    color: var(--el-text-color-placeholder);
}

.cell-query-operation {
    display: flex;
    gap: 10px;
    align-items: center;
    justify-content: space-between;
    width: 100%;
    padding: 7px 10px;
    margin: 0;
    color: inherit;
    font-size: 12px;
    line-height: 18px;
    text-align: left;
    cursor: pointer;
    background: transparent;
    border: 0;

    &:hover {
        color: var(--el-color-primary);
        background: var(--el-fill-color-light);
    }
}

.cell-query-submenu {
    position: relative;
    margin-top: 4px;
    border-top: 1px solid var(--el-border-color-lighter);

    &:hover .cell-query-submenu-panel,
    &:focus-within .cell-query-submenu-panel {
        display: block;
    }

    &.opens-left .cell-query-submenu-panel {
        right: calc(100% + 4px);
        left: auto;
    }
}

.cell-query-submenu-trigger {
    margin-top: 4px;
}

.cell-query-submenu-panel {
    position: absolute;
    top: -5px;
    left: calc(100% + 4px);
    display: none;
    width: 132px;
    padding: 6px;
    color: var(--el-text-color-primary);
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: 4px;
    box-shadow: var(--el-box-shadow-light);
}

.cell-query-symbol {
    flex: none;
    margin-left: auto;
    font-family: Consolas, Monaco, monospace;
    color: var(--el-text-color-secondary);
}

.query-range-inputs {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
    gap: 8px;
    align-items: center;
    width: 100%;
}
</style>

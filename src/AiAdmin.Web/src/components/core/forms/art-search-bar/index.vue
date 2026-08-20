<!-- 表格搜索组件 -->
<!-- 支持常用表单组件、自定义组件、插槽、校验、隐藏表单项 -->
<!-- 写法同 ElementPlus 官方文档组件，把属性写在 props 里面就可以了 -->
<template>
  <section class="art-search-bar art-card-xs" :class="{ 'is-expanded': isExpanded }">
    <ElForm
      ref="formRef"
      :model="modelValue"
      :label-position="labelPosition"
      v-bind="{ ...$attrs }"
      @submit.prevent="handleSearch"
    >
      <ElRow :gutter="gutter">
        <ElCol
          v-for="item in visibleFormItems"
          :key="item.key"
          :xs="getColSpan(item.span, 'xs')"
          :sm="getColSpan(item.span, 'sm')"
          :md="getColSpan(item.span, 'md')"
          :lg="getColSpan(item.span, 'lg')"
          :xl="getColSpan(item.span, 'xl')"
        >
          <ElFormItem
            :prop="item.key"
            :label-width="item.label ? item.labelWidth || labelWidth : undefined"
          >
            <template #label v-if="item.label">
              <component v-if="typeof item.label !== 'string'" :is="item.label" />
              <span v-else>{{ item.label }}</span>
            </template>
            <div v-if="item.key === 'CreatedAt'" class="date-filter-control">
              <ElSwitch
                v-model="useUpdatedAt"
                class="date-filter-mode-switch"
                inline-prompt
                :active-text="t('table.searchBar.updatedAt')"
                :inactive-text="t('table.searchBar.createdAt')"
              />
              <slot :name="item.key" :item="item" :modelValue="modelValue">
                <component
                  :is="getComponent(item)"
                  :model-value="getFieldValue(item.key)"
                  @update:model-value="setFieldValue(item.key, $event)"
                  v-bind="getProps(item)"
                >
                  <template v-if="item.type === 'select' && getProps(item)?.options">
                    <ElOption
                      v-for="option in getProps(item).options"
                      v-bind="option"
                      :key="option.value"
                    />
                  </template>
                  <template v-if="item.type === 'checkboxgroup' && getProps(item)?.options">
                    <ElCheckbox
                      v-for="option in getProps(item).options"
                      v-bind="option"
                      :key="option.value"
                    />
                  </template>
                  <template v-if="item.type === 'radiogroup' && getProps(item)?.options">
                    <ElRadio
                      v-for="option in getProps(item).options"
                      v-bind="option"
                      :key="option.value"
                    />
                  </template>
                  <template
                    v-for="(slotFn, slotName) in getSlots(item)"
                    :key="slotName"
                    #[slotName]
                  >
                    <component :is="slotFn" />
                  </template>
                </component>
              </slot>
            </div>
            <slot v-else :name="item.key" :item="item" :modelValue="modelValue">
              <component
                :is="getComponent(item)"
                :model-value="getFieldValue(item.key)"
                @update:model-value="setFieldValue(item.key, $event)"
                v-bind="getProps(item)"
              >
                <template v-if="item.type === 'select' && getProps(item)?.options">
                  <ElOption
                    v-for="option in getProps(item).options"
                    v-bind="option"
                    :key="option.value"
                  />
                </template>
                <template v-if="item.type === 'checkboxgroup' && getProps(item)?.options">
                  <ElCheckbox
                    v-for="option in getProps(item).options"
                    v-bind="option"
                    :key="option.value"
                  />
                </template>
                <template v-if="item.type === 'radiogroup' && getProps(item)?.options">
                  <ElRadio
                    v-for="option in getProps(item).options"
                    v-bind="option"
                    :key="option.value"
                  />
                </template>
                <template v-for="(slotFn, slotName) in getSlots(item)" :key="slotName" #[slotName]>
                  <component :is="slotFn" />
                </template>
              </component>
            </slot>
          </ElFormItem>
        </ElCol>
        <ElCol :xs="24" :sm="24" :md="span" :lg="span" :xl="span" class="action-column">
          <div class="action-buttons-wrapper" :style="actionButtonsStyle">
            <div class="form-buttons">
              <div v-if="shouldShowExpandToggle" class="filter-toggle" @click="toggleExpand">
                <span>{{ expandToggleText }}</span>
                <div class="icon-wrapper">
                  <ElIcon>
                    <ArrowUpBold v-if="isExpanded" />
                    <ArrowDownBold v-else />
                  </ElIcon>
                </div>
              </div>
              <div v-if="showSearch && advancedQueryFields?.length" class="query-button-group">
                <ElDropdown
                  trigger="hover"
                  :disabled="disabledSearch"
                  @command="applySavedQuery"
                  @visible-change="handleSavedQueryMenuVisible"
                >
                  <ElButton
                    type="primary"
                    class="search-button"
                    :disabled="disabledSearch"
                    @click="handleSearch"
                    v-ripple
                  >
                    {{ t('table.searchBar.search') }}
                  </ElButton>
                  <template #dropdown>
                    <ElDropdownMenu>
                      <ElDropdownItem
                        v-for="query in savedQueries"
                        :key="query.id"
                        :command="query.id"
                      >
                        {{ query.name }}
                      </ElDropdownItem>
                      <ElDropdownItem v-if="!savedQueries.length" disabled>
                        {{ t('table.searchBar.noSavedQueries') }}
                      </ElDropdownItem>
                    </ElDropdownMenu>
                  </template>
                </ElDropdown>
                <ElButton
                  type="primary"
                  class="advanced-query-button"
                  :disabled="disabledSearch"
                  :aria-label="t('table.searchBar.advancedQuery')"
                  @click="advancedQueryVisible = true"
                >
                  ...
                </ElButton>
              </div>
              <ElButton
                v-else-if="showSearch"
                type="primary"
                class="search-button"
                @click="handleSearch"
                v-ripple
                :disabled="disabledSearch"
              >
                {{ t('table.searchBar.search') }}
              </ElButton>
              <ElBadge
                v-if="showReset"
                :value="activeQueryConditionCount"
                :hidden="activeQueryConditionCount === 0"
                class="query-count-badge"
              >
                <ElTooltip
                  placement="bottom"
                  trigger="hover"
                  :enterable="true"
                  effect="light"
                  popper-class="query-preview-popper"
                >
                  <template #content>
                    <div class="query-preview-editor">
                      <ArtJsonEditor v-model="queryPreviewText" class="query-preview-ace" />
                      <div class="query-preview-actions">
                        <span v-if="queryPreviewError" class="query-preview-error">{{
                          queryPreviewError
                        }}</span>
                        <ElButton size="small" @click="formatQueryPreview">格式化</ElButton>
                        <ElButton size="small" type="primary" @click="applyQueryPreview"
                          >应用</ElButton
                        >
                        <ElButton size="small" @click="saveQueryPreview">保存</ElButton>
                      </div>
                    </div>
                  </template>
                  <ElButton class="reset-button" @click="handleReset" v-ripple>
                    {{ t('table.searchBar.reset') }}
                  </ElButton>
                </ElTooltip>
              </ElBadge>
            </div>
          </div>
        </ElCol>
      </ElRow>
    </ElForm>
    <ArtDynamicQueryDrawer
      v-if="advancedQueryFields?.length"
      v-model:visible="advancedQueryVisible"
      :model-value="activeAdvancedFilter"
      :fields="advancedQueryFields"
      @apply="handleAdvancedQueryApply"
    />
  </section>
</template>

<script setup lang="ts">
  import { ArrowUpBold, ArrowDownBold } from '@element-plus/icons-vue'
  import { useWindowSize } from '@vueuse/core'
  import { useI18n } from 'vue-i18n'
  import { useRoute } from 'vue-router'
  import { toRaw, type Component } from 'vue'
  import {
    ElCascader,
    ElCheckbox,
    ElCheckboxGroup,
    ElDatePicker,
    ElInput,
    ElInputTag,
    ElInputNumber,
    ElMessage,
    ElMessageBox,
    ElRadioGroup,
    ElRate,
    ElSelect,
    ElSlider,
    ElSwitch,
    ElTimePicker,
    ElTimeSelect,
    ElTreeSelect,
    type FormInstance
  } from 'element-plus'
  import { calculateResponsiveSpan, type ResponsiveBreakpoint } from '@/utils/form/responsive'
  import ArtDynamicQueryDrawer from '../art-dynamic-query-drawer/index.vue'
  import ArtJsonEditor from '../art-json-editor/index.vue'
  import type { DynamicFilter, DynamicQueryField } from '../art-dynamic-query-drawer/types'
  import {
    fetchGetSavedQueries,
    fetchSaveQuery,
    type ListFilterField,
    type SavedQuery
  } from '@/api/system-manage'

  defineOptions({ name: 'ArtSearchBar' })

  const componentMap = {
    input: ElInput, // 输入框
    inputTag: ElInputTag, // 标签输入框
    number: ElInputNumber, // 数字输入框
    select: ElSelect, // 选择器
    switch: ElSwitch, // 开关
    checkbox: ElCheckbox, // 复选框
    checkboxgroup: ElCheckboxGroup, // 复选框组
    radiogroup: ElRadioGroup, // 单选框组
    date: ElDatePicker, // 日期选择器
    daterange: ElDatePicker, // 日期范围选择器
    datetime: ElDatePicker, // 日期时间选择器
    datetimerange: ElDatePicker, // 日期时间范围选择器
    rate: ElRate, // 评分
    slider: ElSlider, // 滑块
    cascader: ElCascader, // 级联选择器
    timepicker: ElTimePicker, // 时间选择器
    timeselect: ElTimeSelect, // 时间选择
    treeselect: ElTreeSelect // 树选择器
  }

  const { width } = useWindowSize()
  const { t } = useI18n()
  const route = useRoute()
  const isMobile = computed(() => width.value < 500)

  const formInstance = useTemplateRef<FormInstance>('formRef')

  // 表单项配置
  export interface SearchFormItem {
    /** 表单项的唯一标识 */
    key: string
    /** 表单项的标签文本或自定义渲染函数 */
    label?: string | (() => VNode) | Component
    /** 表单项标签的宽度，会覆盖 Form 的 labelWidth */
    labelWidth?: string | number
    /** 表单项类型，支持预定义的组件类型 */
    type?: keyof typeof componentMap | string
    /** 自定义渲染函数或组件，用于渲染自定义组件（优先级高于 type） */
    render?: (() => VNode) | Component
    /** 是否隐藏该表单项 */
    hidden?: boolean
    /** 表单项占据的列宽，基于24格栅格系统 */
    span?: number
    /** 选项数据，用于 select、checkbox-group、radio-group 等 */
    options?: Record<string, any>
    /** 传递给表单项组件的属性 */
    props?: Record<string, any>
    /** 表单项的插槽配置 */
    slots?: Record<string, (() => any) | undefined>
    /** 表单项的占位符文本 */
    placeholder?: string
    /** 更多属性配置请参考 ElementPlus 官方文档 */
  }

  // 表单配置
  interface SearchBarProps {
    /** 表单数据 */
    items: SearchFormItem[]
    /** 每列的宽度（基于 24 格布局） */
    span?: number
    /** 表单控件间隙 */
    gutter?: number
    /** 展开/收起 */
    isExpand?: boolean
    /** 默认是否展开（仅在 showExpand 为 true 且 isExpand 为 false 时生效） */
    defaultExpanded?: boolean
    /** 表单域标签的位置 */
    labelPosition?: 'left' | 'right' | 'top'
    /** 文字宽度 */
    labelWidth?: string | number
    /** 是否需要展示，收起 */
    showExpand?: boolean
    /** 按钮靠左对齐限制（表单项小于等于该值时） */
    buttonLeftLimit?: number
    /** 是否显示重置按钮 */
    showReset?: boolean
    /** 是否显示搜索按钮 */
    showSearch?: boolean
    /** 是否禁用搜索按钮 */
    disabledSearch?: boolean
    /** 搜索时是否清洗空值 */
    sanitizeOutput?: Partial<SanitizeOutputOptions>
    /** 高级动态查询可选字段 */
    advancedQueryFields?: DynamicQueryField[]
    /** 由后端模型筛选特性反射得到的基础筛选字段 */
    filterFields?: ListFilterField[]
  }

  interface SanitizeOutputOptions {
    /** 移除空字符串 */
    removeEmptyString: boolean
    /** 移除空数组 */
    removeEmptyArray: boolean
    /** 移除清洗后为空的对象 */
    removeEmptyObject: boolean
    /** 移除空富文本占位内容，如 <p><br></p> */
    removeEmptyRichText: boolean
    /** 保留数字 0 这类有效筛选值 */
    keepZero: boolean
    /** 保留 false 这类有效筛选值 */
    keepFalse: boolean
  }

  const props = withDefaults(defineProps<SearchBarProps>(), {
    items: () => [],
    span: 6,
    gutter: 12,
    isExpand: false,
    labelPosition: 'right',
    labelWidth: '70px',
    showExpand: true,
    defaultExpanded: false,
    buttonLeftLimit: 2,
    showReset: true,
    showSearch: true,
    disabledSearch: false,
    sanitizeOutput: () => ({})
  })

  interface SearchBarEmits {
    reset: []
    search: [Record<string, any>]
  }

  const emit = defineEmits<SearchBarEmits>()

  const modelValue = defineModel<Record<string, any>>({ default: {} })
  const advancedQueryVisible = ref(false)
  const savedQueries = ref<SavedQuery[]>([])
  const activeAdvancedFilter = ref<DynamicFilter>()
  const initialModelValue = ref<Record<string, any>>({})
  const useUpdatedAt = ref(false)

  // 在创建时间和更新时间之间切换时，保留用户已经输入的日期范围
  watch(useUpdatedAt, (enabled) => {
    const dateRange = modelValue.value.CreatedAt ?? modelValue.value.UpdatedAt
    if (enabled) {
      delete modelValue.value.CreatedAt
      if (dateRange !== undefined) modelValue.value.UpdatedAt = dateRange
    } else {
      delete modelValue.value.UpdatedAt
      if (dateRange !== undefined) modelValue.value.CreatedAt = dateRange
    }
  })

  // 保存组件初始化时的表单快照，用于 reset 时恢复默认筛选条件。
  const cloneModelValue = (value: Record<string, any> | undefined) => {
    if (!value) return {}

    const deepClone = (source: unknown): unknown => {
      if (Array.isArray(source)) {
        return source.map((item) => deepClone(item))
      }

      if (source && typeof source === 'object') {
        const sourceRecord = source as Record<string, unknown>
        return Object.keys(sourceRecord).reduce<Record<string, unknown>>((accumulator, key) => {
          accumulator[key] = deepClone(sourceRecord[key])
          return accumulator
        }, {})
      }

      return source
    }

    return deepClone(value) as Record<string, any>
  }

  initialModelValue.value = cloneModelValue(modelValue.value)

  /**
   * 是否展开状态
   */
  const isExpanded = ref(props.defaultExpanded)

  const rootProps = ['label', 'labelWidth', 'key', 'type', 'hidden', 'span', 'slots']
  // 搜索参数默认更激进地去掉空值，减少无效 query 参数。
  const sanitizeOutputOptions = computed<SanitizeOutputOptions>(() => ({
    removeEmptyString: true,
    removeEmptyArray: true,
    removeEmptyObject: true,
    removeEmptyRichText: true,
    keepZero: true,
    keepFalse: true,
    ...props.sanitizeOutput
  }))

  const getProps = (item: SearchFormItem) => {
    if (item.props) return item.props
    const props = { ...item }
    rootProps.forEach((key) => delete (props as Record<string, any>)[key])
    return props
  }

  // 获取插槽
  const getSlots = (item: SearchFormItem) => {
    if (!item.slots) return {}
    const validSlots: Record<string, () => any> = {}
    Object.entries(item.slots).forEach(([key, slotFn]) => {
      if (slotFn) {
        validSlots[key] = slotFn
      }
    })
    return validSlots
  }

  /**
   * 获取列宽 span 值
   * 根据屏幕尺寸智能降级，避免小屏幕上表单项被压缩过小
   */
  const getColSpan = (itemSpan: number | undefined, breakpoint: ResponsiveBreakpoint): number => {
    return calculateResponsiveSpan(itemSpan, span.value, breakpoint)
  }

  // 搜索表单清空输入时不保留空字符串，避免后续请求携带空字段。
  const normalizeFieldValue = (value: unknown) => {
    return value === '' ? undefined : value
  }

  const resolveFieldKey = (key: string) =>
    key === 'CreatedAt' && useUpdatedAt.value ? 'UpdatedAt' : key

  const getFieldValue = (key: string) => modelValue.value[resolveFieldKey(key)]

  const setFieldValue = (key: string, value: unknown) => {
    const resolvedKey = resolveFieldKey(key)
    const normalizedValue = normalizeFieldValue(value)

    if (normalizedValue === undefined) {
      delete modelValue.value[resolvedKey]
      return
    }

    modelValue.value[resolvedKey] = normalizedValue
  }

  const isRichTextEmpty = (value: string) => {
    if (/<(img|video|audio|iframe|embed|object)\b/i.test(value)) {
      return false
    }

    // 去掉编辑器常见占位标签后再判断是否还有实际内容。
    return (
      value
        .replace(/&nbsp;/gi, '')
        .replace(/<br\s*\/?>/gi, '')
        .replace(/<[^>]*>/g, '')
        .trim() === ''
    )
  }

  // 搜索时按配置清洗空值，但保留 0 和 false 这类有效筛选条件。
  const sanitizeOutputValue = (value: unknown): unknown => {
    const options = sanitizeOutputOptions.value

    if (Array.isArray(value)) {
      const sanitizedArray = value
        .map((item) => sanitizeOutputValue(item))
        .filter((item) => item !== undefined)
      return sanitizedArray.length === 0 && options.removeEmptyArray ? undefined : sanitizedArray
    }

    if (value && typeof value === 'object') {
      const rawValue = toRaw(value)
      const sanitizedObject = Object.entries(rawValue).reduce<Record<string, unknown>>(
        (accumulator, [key, item]) => {
          const sanitizedItem = sanitizeOutputValue(item)
          if (sanitizedItem !== undefined) {
            accumulator[key] = sanitizedItem
          }
          return accumulator
        },
        {}
      )
      return Object.keys(sanitizedObject).length === 0 && options.removeEmptyObject
        ? undefined
        : sanitizedObject
    }

    if (typeof value === 'string') {
      if (options.removeEmptyString && value.trim() === '') {
        return undefined
      }
      if (options.removeEmptyRichText && isRichTextEmpty(value)) {
        return undefined
      }
      return value
    }

    if (value === 0) {
      return options.keepZero ? value : undefined
    }

    if (value === false) {
      return options.keepFalse ? value : undefined
    }

    return value ?? undefined
  }

  const getSanitizedOutput = () => {
    return (sanitizeOutputValue(cloneModelValue(modelValue.value)) || {}) as Record<string, any>
  }

  const countDynamicFilterConditions = (filter: DynamicFilter | undefined): number => {
    if (!filter) return 0
    if (filter.filters?.length) {
      return filter.filters.reduce((count, child) => count + countDynamicFilterConditions(child), 0)
    }
    return filter.field && filter.operator ? 1 : 0
  }

  const activeQueryConditionCount = computed(() => {
    if (activeAdvancedFilter.value) {
      return countDynamicFilterConditions(activeAdvancedFilter.value)
    }
    return Object.keys(getSanitizedOutput()).length
  })

  // 将当前查询条件转换为已转义的高亮 JSON，避免提示内容被当作 HTML 执行。
  const formattedQueryPreview = computed(() => {
    const query = activeAdvancedFilter.value
      ? { dynamicFilter: activeAdvancedFilter.value }
      : getSanitizedOutput()
    const json = JSON.stringify(query, null, 2)
    const escaped = json
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')

    return escaped.replace(
      /(&quot;(?:\\.|[^&])*?&quot;)(?=\s*:)|(&quot;(?:\\.|[^&])*?&quot;)|\b(true|false|null)\b|-?\b\d+(?:\.\d+)?\b/g,
      (match, key, stringValue, literal) => {
        if (key) return `<span class="json-key">${key}</span>`
        if (stringValue) return `<span class="json-string">${stringValue}</span>`
        if (literal === 'true' || literal === 'false')
          return `<span class="json-boolean">${literal}</span>`
        if (literal === 'null') return `<span class="json-null">${literal}</span>`
        return `<span class="json-number">${match}</span>`
      }
    )
  })
  const queryPreviewText = ref('{}')
  const queryPreviewError = ref('')
  watch(
    formattedQueryPreview,
    () => {
      queryPreviewText.value = JSON.stringify(
        activeAdvancedFilter.value
          ? { dynamicFilter: activeAdvancedFilter.value }
          : getSanitizedOutput(),
        null,
        2
      )
      queryPreviewError.value = ''
    },
    { immediate: true }
  )

  const parseQueryPreview = (): Record<string, any> | undefined => {
    try {
      const parsed = JSON.parse(queryPreviewText.value)
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error()
      queryPreviewError.value = ''
      return parsed
    } catch {
      queryPreviewError.value = 'JSON 格式不正确'
      return undefined
    }
  }
  const formatQueryPreview = () => {
    const parsed = parseQueryPreview()
    if (parsed) queryPreviewText.value = JSON.stringify(parsed, null, 2)
  }
  const queryPreviewToFilter = (query: Record<string, any>): DynamicFilter | undefined => {
    if (query.dynamicFilter && typeof query.dynamicFilter === 'object') return query.dynamicFilter
    const filters = Object.entries(query)
      .filter(
        ([key, value]) =>
          key !== 'dynamicFilter' && value !== undefined && value !== null && value !== ''
      )
      .map(([field, value]) => ({
        field,
        operator: Array.isArray(value)
          ? ['CreatedAt', 'UpdatedAt'].includes(field)
            ? 'DateRange'
            : 'Any'
          : typeof value === 'string'
            ? 'Contains'
            : 'Equal',
        value
      }))
    return filters.length ? { logic: 'And', filters } : undefined
  }
  const applyQueryPreview = () => {
    const parsed = parseQueryPreview()
    if (!parsed) return
    const filter = queryPreviewToFilter(parsed)
    if (parsed.dynamicFilter) applyAdvancedFilter(filter)
    else {
      Object.keys(modelValue.value).forEach((key) => delete modelValue.value[key])
      Object.assign(modelValue.value, parsed)
      emit('search', getSanitizedOutput())
    }
  }
  const saveQueryPreview = async () => {
    const parsed = parseQueryPreview()
    const dynamicFilter = parsed && queryPreviewToFilter(parsed)
    if (!dynamicFilter) return
    const { value } = await ElMessageBox.prompt('请输入查询名称', '保存查询', {
      inputPattern: /\S+/,
      inputErrorMessage: '请输入查询名称'
    })
    await fetchSaveQuery({ name: value.trim(), route: route.path, dynamicFilter })
    ElMessage.success('保存成功')
  }

  // 组件
  const getComponent = (item: SearchFormItem) => {
    // 优先使用 render 函数或组件渲染自定义组件
    if (item.render) {
      return item.render
    }
    // 使用 type 获取预定义组件
    const { type } = item
    return componentMap[type as keyof typeof componentMap] || componentMap['input']
  }

  /**
   * 可见的表单项
   */
  const convertFilterOptionValue = (
    value: string,
    valueType: ListFilterField['valueType']
  ): string | number | boolean => {
    if (valueType === 'boolean') return value === 'true'
    if (valueType === 'number') return Number(value)
    return value
  }

  /** 创建日期范围选择器的常用快捷时间段 */
  const createDateShortcuts = (fieldKey: string) => {
    const now = new Date()
    const startOfDay = (date: Date) => {
      const result = new Date(date)
      result.setHours(0, 0, 0, 0)
      return result
    }
    const startOfWeek = (date: Date) => {
      const result = startOfDay(date)
      const day = result.getDay() || 7
      result.setDate(result.getDate() - day + 1)
      return result
    }
    const startOfMonth = (date: Date) => {
      const result = startOfDay(date)
      result.setDate(1)
      return result
    }
    const addDays = (date: Date, days: number) => {
      const result = new Date(date)
      result.setDate(result.getDate() + days)
      return result
    }
    const addMonths = (date: Date, months: number) => {
      const result = new Date(date)
      result.setMonth(result.getMonth() + months)
      return result
    }

    const today = startOfDay(now)
    const thisWeek = startOfWeek(now)
    const thisMonth = startOfMonth(now)
    const nextWeek = addDays(thisWeek, 7)
    const nextMonth = addMonths(thisMonth, 1)
    const yesterday = addDays(today, -1)
    const previousDay = addDays(today, -2)
    const tomorrow = addDays(today, 1)
    const previousWeek = addDays(thisWeek, -7)
    const previousMonth = addMonths(thisMonth, -1)
    const currentHour = new Date(now)
    currentHour.setMinutes(0, 0, 0)
    const nextHour = new Date(currentHour)
    nextHour.setHours(nextHour.getHours() + 1)

    const getCurrentRange = (fallback: [Date, Date]): [Date, Date] => {
      const value =
        modelValue.value[fieldKey === 'CreatedAt' && useUpdatedAt.value ? 'UpdatedAt' : fieldKey]
      if (!Array.isArray(value) || value.length !== 2) return fallback
      const start = new Date(value[0])
      const end = new Date(value[1])
      return Number.isNaN(start.getTime()) || Number.isNaN(end.getTime()) ? fallback : [start, end]
    }
    const shiftRange = (fallback: [Date, Date], shift: (date: Date) => Date): [Date, Date] => {
      const [start, end] = getCurrentRange(fallback)
      return [shift(start), shift(end)]
    }

    return [
      {
        text: t('table.searchBar.lastHour'),
        value: () => [new Date(now.getTime() - 3600000), new Date()]
      },
      {
        text: t('table.searchBar.currentHour'),
        value: () => [new Date(currentHour), new Date(nextHour)]
      },
      {
        text: t('table.searchBar.previousHour'),
        value: () =>
          shiftRange([new Date(now.getTime() - 3600000), new Date(now)], (date) => {
            const result = new Date(date)
            result.setHours(result.getHours() - 1)
            return result
          })
      },
      {
        text: t('table.searchBar.yesterdayAtThisTime'),
        value: () => [new Date(yesterday), new Date()]
      },
      { text: t('table.searchBar.today'), value: () => [new Date(today), new Date(tomorrow)] },
      { text: t('table.searchBar.yesterday'), value: () => [new Date(yesterday), new Date(today)] },
      {
        text: t('table.searchBar.previousDay'),
        value: () => shiftRange([new Date(yesterday), new Date(today)], (date) => addDays(date, -1))
      },
      {
        text: t('table.searchBar.thisWeek'),
        value: () => [new Date(thisWeek), new Date(nextWeek)]
      },
      {
        text: t('table.searchBar.previousWeek'),
        value: () =>
          shiftRange([new Date(thisWeek), new Date(nextWeek)], (date) => addDays(date, -7))
      },
      {
        text: t('table.searchBar.thisMonth'),
        value: () => [new Date(thisMonth), new Date(nextMonth)]
      },
      {
        text: t('table.searchBar.previousMonth'),
        value: () =>
          shiftRange([new Date(thisMonth), new Date(nextMonth)], (date) => addMonths(date, -1))
      }
    ]
  }

  const backendFormItems = computed<SearchFormItem[]>(() =>
    (props.filterFields || []).map((field) => {
      const fieldPlaceholder = t(field.label)
      const controlProps =
        field.control === 'date'
          ? {
              type: 'datetimerange',
              valueFormat: 'YYYY-MM-DDTHH:mm:ss',
              rangeSeparator: t('table.searchBar.to'),
              startPlaceholder: t('table.searchBar.startDate'),
              endPlaceholder: t('table.searchBar.endDate'),
              shortcuts: createDateShortcuts(field.field),
              clearable: true
            }
          : field.control === 'select'
            ? {
                placeholder: fieldPlaceholder,
                options: field.options.map((option) => ({
                  ...option,
                  label: t(option.label),
                  value: convertFilterOptionValue(option.value, field.valueType)
                })),
                clearable: true
              }
            : { placeholder: fieldPlaceholder, clearable: true }

      return {
        key: field.field,
        label: undefined,
        type:
          field.control === 'select'
            ? 'select'
            : field.control === 'date'
              ? 'date'
              : field.control === 'number'
                ? 'number'
                : 'input',
        span: field.span,
        placeholder: fieldPlaceholder,
        props: controlProps
      }
    })
  )
  const activeItems = computed(() =>
    props.filterFields?.length ? backendFormItems.value : props.items
  )
  const currentBreakpoint = computed<ResponsiveBreakpoint>(() => {
    if (width.value < 768) return 'xs'
    if (width.value < 992) return 'sm'
    if (width.value < 1200) return 'md'
    if (width.value < 1920) return 'lg'
    return 'xl'
  })
  const actionReservedSpan = computed(() => {
    const searchSpan = props.showSearch ? 2 : 0
    const advancedQuerySpan = props.showSearch && props.advancedQueryFields?.length ? 1 : 0
    const resetSpan = props.showReset ? 1 : 0
    const expandSpan = !props.isExpand && props.showExpand ? 1 : 0
    return Math.max(
      1,
      Math.min(props.span, searchSpan + advancedQuerySpan + resetSpan + expandSpan)
    )
  })
  const collapsedItems = computed(() => {
    const actionUsesSeparateRow =
      currentBreakpoint.value === 'xs' || currentBreakpoint.value === 'sm'
    const availableSpan = actionUsesSeparateRow ? 24 : 24 - actionReservedSpan.value
    let occupiedSpan = 0

    return activeItems.value
      .filter((item) => !item.hidden)
      .filter((item) => {
        const itemSpan = getColSpan(item.span, currentBreakpoint.value)
        if (occupiedSpan + itemSpan > availableSpan) return false

        occupiedSpan += itemSpan
        return true
      })
  })
  const visibleFormItems = computed(() => {
    const filteredItems = activeItems.value.filter((item) => !item.hidden)
    const shouldShowLess = !props.isExpand && !isExpanded.value
    if (shouldShowLess) {
      return collapsedItems.value
    }
    return filteredItems
  })

  /**
   * 是否应该显示展开/收起按钮
   */
  const shouldShowExpandToggle = computed(() => {
    const filteredItems = activeItems.value.filter((item) => !item.hidden)
    return !props.isExpand && props.showExpand && filteredItems.length > collapsedItems.value.length
  })

  /**
   * 展开/收起按钮文本
   */
  const expandToggleText = computed(() => {
    return isExpanded.value ? t('table.searchBar.collapse') : t('table.searchBar.expand')
  })

  /**
   * 操作按钮样式
   */
  const actionButtonsStyle = computed(() => ({
    'justify-content': isMobile.value
      ? 'flex-end'
      : activeItems.value.filter((item) => !item.hidden).length <= props.buttonLeftLimit
        ? 'flex-start'
        : 'flex-end'
  }))

  /**
   * 切换展开/收起状态
   */
  const toggleExpand = () => {
    isExpanded.value = !isExpanded.value
  }

  /**
   * 处理重置事件
   */
  const handleReset = () => {
    // 重置表单字段（UI 层）
    formInstance.value?.resetFields()

    // 恢复初始表单值，保留默认搜索条件而不是简单清空。
    Object.keys(modelValue.value).forEach((key) => {
      delete modelValue.value[key]
    })
    Object.assign(modelValue.value, cloneModelValue(initialModelValue.value))
    useUpdatedAt.value = false
    activeAdvancedFilter.value = undefined

    // 触发 reset 事件
    emit('reset')
  }

  /**
   * 处理搜索事件
   */
  const handleSearch = () => {
    activeAdvancedFilter.value = undefined
    delete modelValue.value.dynamicFilter
    // 对外只抛出清洗后的查询参数，避免接口收到空数组/空字符串。
    emit('search', getSanitizedOutput())
  }

  // 每次展开菜单时刷新数据，使高级查询层中新保存的条件立即可用。
  const handleSavedQueryMenuVisible = async (visible: boolean) => {
    if (visible) {
      savedQueries.value = await fetchGetSavedQueries(route.path)
    }
  }

  // 选择已保存条件后直接交给页面现有的高级查询处理流程。
  const applySavedQuery = (id: string | number) => {
    const query = savedQueries.value.find((item) => String(item.id) === String(id))
    if (query) {
      applyAdvancedFilter(query.dynamicFilter)
    }
  }

  const applyAdvancedFilter = (filter: DynamicFilter | undefined) => {
    activeAdvancedFilter.value = filter
    Object.keys(modelValue.value).forEach((key) => {
      delete modelValue.value[key]
    })
    if (filter) {
      modelValue.value.dynamicFilter = filter
    }
    emit('search', getSanitizedOutput())
  }

  const handleAdvancedQueryApply = (filter: DynamicFilter | undefined) =>
    applyAdvancedFilter(filter)

  defineExpose({
    ref: formInstance,
    validate: (...args: any[]) => formInstance.value?.validate(...args),
    reset: handleReset,
    // 允许外部在手动组装请求前直接读取清洗后的参数。
    getOutput: getSanitizedOutput
  })

  // 解构 props 以便在模板中直接使用
  const { span, gutter, labelPosition, labelWidth } = toRefs(props)
</script>

<style lang="scss" scoped>
  .art-search-bar {
    padding: 15px 20px 0;

    .action-column {
      flex: 1;
      max-width: 100%;

      .action-buttons-wrapper {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        justify-content: flex-end;
        margin-bottom: 12px;
      }

      .form-buttons {
        display: flex;
        gap: 8px;

        .query-button-group {
          display: flex;

          .search-button {
            border-top-right-radius: 0;
            border-bottom-right-radius: 0;
          }

          .advanced-query-button {
            width: 34px;
            min-width: 34px;
            padding: 0;
            margin-left: 1px;
            border-top-left-radius: 0;
            border-bottom-left-radius: 0;
            letter-spacing: 0;
          }
        }
      }

      .filter-toggle {
        display: flex;
        align-items: center;
        margin-left: 10px;
        line-height: 32px;
        color: var(--theme-color);
        cursor: pointer;
        transition: color 0.2s ease;

        &:hover {
          color: var(--ElColor-primary);
        }

        span {
          font-size: 14px;
          user-select: none;
        }

        .icon-wrapper {
          display: flex;
          align-items: center;
          margin-left: 4px;
          font-size: 14px;
          transition: transform 0.2s ease;
        }
      }
    }
  }

  .query-preview {
    max-width: min(560px, 70vw);
    max-height: 360px;
    margin: 0;
    overflow: auto;
    color: var(--el-text-color-primary);
    font:
      12px/1.5 Consolas,
      Monaco,
      monospace;
    white-space: pre;

    :deep(.json-key) {
      color: var(--el-color-primary);
    }

    :deep(.json-string) {
      color: var(--el-color-success);
    }

    :deep(.json-number) {
      color: var(--el-color-warning);
    }

    :deep(.json-boolean),
    :deep(.json-null) {
      color: var(--el-color-danger);
    }
  }

  .query-preview-editor {
    width: min(520px, calc(100vw - 40px));
  }

  .query-preview-actions {
    display: flex;
    gap: 6px;
    align-items: center;
    justify-content: flex-end;
    margin-top: 8px;
  }

  .query-preview-error {
    flex: 1;
    color: var(--el-color-danger);
    font-size: 12px;
  }

  .date-filter-control {
    display: flex;
    gap: 8px;
    align-items: center;
    width: 100%;
    min-width: 0;

    :deep(.el-date-editor) {
      flex: 1;
      width: 0;
      min-width: 0;
    }

    :deep(.el-range-input) {
      min-width: 0;
    }
  }

  .date-filter-mode-switch {
    flex: none;

    &:not(.is-checked) :deep(.el-switch__core) {
      color: var(--el-color-white);
      background: var(--el-color-primary);
      border-color: var(--el-color-primary);
    }
  }

  @media (max-width: 767px) {
    .date-filter-control {
      gap: 4px;

      .date-filter-mode-switch {
        width: 76px;
        font-size: 11px;
      }

      :deep(.el-date-editor .el-range-separator) {
        padding: 0 2px;
      }
    }
  }

  :global(.query-preview-popper.el-popper) {
    background: var(--el-bg-color-overlay);
    border-color: var(--el-border-color-light);
    color: var(--el-text-color-primary);
    box-shadow: var(--el-box-shadow-light);
  }

  :global(.query-preview-popper.el-popper .el-popper__arrow::before) {
    background: var(--el-bg-color-overlay);
    border-color: var(--el-border-color-light);
  }

  // 响应式优化
  @media (width <= 768px) {
    .art-search-bar {
      padding: 16px 16px 0;

      .action-column {
        .action-buttons-wrapper {
          flex-direction: column;
          gap: 8px;
          align-items: stretch;

          .form-buttons {
            justify-content: center;
          }

          .filter-toggle {
            justify-content: center;
            margin-left: 0;
          }
        }
      }
    }
  }
</style>

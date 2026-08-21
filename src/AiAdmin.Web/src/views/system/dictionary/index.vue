<template>
  <div class="art-full-height">
    <ArtSearchBar
      v-model="searchForm"
      :filter-fields="filterFields"
      :advanced-query-fields="advancedQueryFields"
      @search="handleSearch"
      @reset="resetSearch"
    />
    <div class="dictionary-layout">
      <ElCard class="directory-panel art-card-xs">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-medium">字典目录</span>
            <div class="flex items-center gap-2">
              <ElButton v-auth="'add'" text circle title="新增根目录" @click="openCategoryDialog()">
                <ArtSvgIcon icon="ri:add-line" />
              </ElButton>
            </div>
          </div>
        </template>

        <ElScrollbar>
          <ElTree
            ref="treeRef"
            :data="categories"
            :props="{ label: 'name', children: 'children' }"
            node-key="id"
            default-expand-all
            highlight-current
            :expand-on-click-node="false"
            @node-click="selectCategory"
          >
            <template #default="{ data }">
              <div class="tree-node">
                <div class="tree-node-main">
                  <span class="truncate">{{ getCategoryName(data) }}</span>
                </div>
                <div class="node-actions">
                  <ElButton
                    text
                    circle
                    title="新增子目录"
                    @click.stop="openCategoryDialog(undefined, data.id)"
                  >
                    <ArtSvgIcon icon="ri:add-line" />
                  </ElButton>
                  <ElButton text circle title="编辑目录" @click.stop="openCategoryDialog(data)">
                    <ArtSvgIcon icon="ri:edit-2-line" />
                  </ElButton>
                  <ElButton text circle title="删除目录" @click.stop="deleteCategory(data)">
                    <ArtSvgIcon icon="ri:delete-bin-4-line" />
                  </ElButton>
                </div>
              </div>
            </template>
          </ElTree>
        </ElScrollbar>
      </ElCard>

      <ElCard class="content-panel art-table-card">
        <ArtTableHeader :loading="loading" @refresh="loadItems">
          <template #left>
            <div class="flex items-center gap-3">
              <ElButton v-auth="'add'" :disabled="!selectedCategory" @click="openItemDialog()">
                <ArtSvgIcon icon="ri:add-line" class="mr-1" />新增字典内容
              </ElButton>
              <span class="text-g-500">{{
                selectedCategory ? getCategoryName(selectedCategory) : '请选择字典目录'
              }}</span>
            </div>
          </template>
        </ArtTableHeader>

        <ArtTable
          :loading="loading"
          :data="filteredItems"
          height="calc(100vh - 220px)"
          @cell-query="handleCellQuery"
        >
          <ElTableColumn prop="label" query-field="Label" label="标签" min-width="160" sortable />
          <ElTableColumn prop="value" query-field="Value" label="键值" min-width="160" sortable />
          <ElTableColumn prop="sort" query-field="Sort" query-value-type="number" label="排序" width="90" sortable />
          <ElTableColumn prop="isEnabled" query-field="IsEnabled" query-value-type="boolean" label="是否启用" width="100" sortable>
            <template #default="{ row }"
              ><ArtEnabledSwitch :id="row.id" v-model="row.isEnabled" resource="dictionary-item"
            /></template>
          </ElTableColumn>
          <ElTableColumn
            prop="remark"
            query-field="Remark"
            label="备注"
            min-width="180"
            show-overflow-tooltip
            sortable
          />
          <ElTableColumn label="操作" width="110" fixed="right">
            <template #default="{ row }">
              <ElButton text circle title="编辑" @click="openItemDialog(row)"
                ><ArtSvgIcon icon="ri:edit-2-line"
              /></ElButton>
              <ElButton text circle title="删除" @click="deleteItem(row)"
                ><ArtSvgIcon icon="ri:delete-bin-4-line" class="text-danger"
              /></ElButton>
            </template>
          </ElTableColumn>
        </ArtTable>
      </ElCard>
    </div>

    <ElDialog
      v-model="categoryDialogVisible"
      :title="categoryForm.id ? '编辑字典目录' : '新增字典目录'"
      width="520px"
      destroy-on-close
    >
      <ElTabs v-model="categoryDialogTab">
        <ElTabPane label="基本信息" name="form">
          <ElForm label-width="90px">
            <ElFormItem label="上级目录"
              ><ElSelect
                v-model="categoryForm.parentId"
                clearable
                filterable
                class="w-full"
                placeholder="根目录"
                ><ElOption
                  v-for="option in categoryOptions"
                  :key="option.id"
                  :label="option.label"
                  :value="option.id"
                  :disabled="categoryForm.id === option.id" /></ElSelect
            ></ElFormItem>
            <ElFormItem label="目录名称" required
              ><ElInput v-model="categoryForm.name" maxlength="100"
            /></ElFormItem>
            <ElFormItem label="目录编码" required
              ><ElInput v-model="categoryForm.code" maxlength="100"
            /></ElFormItem>
            <ElFormItem label="排序"
              ><ElInputNumber v-model="categoryForm.sort" :min="0" :max="9999"
            /></ElFormItem>
          </ElForm>
        </ElTabPane>
        <ElTabPane v-if="categoryForm.id" label="原始数据" name="raw-data"
          ><ArtRawData :data="categoryRawData"
        /></ElTabPane>
      </ElTabs>
      <template #footer
        ><ElButton @click="categoryDialogVisible = false">取消</ElButton
        ><ElButton type="primary" :loading="saving" @click="saveCategory">保存</ElButton></template
      >
    </ElDialog>

    <ElDialog
      v-model="itemDialogVisible"
      :title="itemForm.id ? '编辑字典内容' : '新增字典内容'"
      width="520px"
      destroy-on-close
    >
      <ElTabs v-model="itemDialogTab">
        <ElTabPane label="基本信息" name="form">
          <ElForm label-width="80px">
            <ElFormItem label="标签" required
              ><ElInput v-model="itemForm.label" maxlength="100"
            /></ElFormItem>
            <ElFormItem label="键值" required
              ><ElInput v-model="itemForm.value" maxlength="100"
            /></ElFormItem>
            <ElFormItem label="排序"
              ><ElInputNumber v-model="itemForm.sort" :min="0" :max="9999"
            /></ElFormItem>
            <ElFormItem label="是否启用"><ElSwitch v-model="itemForm.isEnabled" /></ElFormItem>
            <ElFormItem label="备注"
              ><ElInput
                v-model="itemForm.remark"
                type="textarea"
                :rows="3"
                maxlength="500"
                show-word-limit
            /></ElFormItem>
          </ElForm>
        </ElTabPane>
        <ElTabPane v-if="itemForm.id" label="原始数据" name="raw-data"><ArtRawData :data="itemRawData" /></ElTabPane>
      </ElTabs>
      <template #footer
        ><ElButton @click="itemDialogVisible = false">取消</ElButton
        ><ElButton type="primary" :loading="saving" @click="saveItem">保存</ElButton></template
      >
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
  import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
  import type { DynamicFilter, DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
  import {
    fetchCreateDictionaryCategory,
    fetchCreateDictionaryItem,
    fetchDeleteDictionaryCategory,
    fetchDeleteDictionaryItem,
    fetchGetDictionaryCategories,
    fetchGetDictionaryFilterFields,
    fetchGetDictionaryItems,
    fetchUpdateDictionaryCategory,
    fetchUpdateDictionaryItem
  } from '@/api/system-manage'

  defineOptions({ name: 'DictionaryManagement' })
  type Category = Api.SystemManage.DictionaryCategory
  type Item = Api.SystemManage.DictionaryItem
  const categories = ref<Category[]>([])
  const items = ref<Item[]>([])
  const selectedCategory = ref<Category>()
  const treeRef = ref()
  const loading = ref(false)
  const saving = ref(false)
  const filterFields = ref<import('@/api/system-manage').ListFilterField[]>([])
  const searchForm = reactive<Record<string, unknown> & { dynamicFilter?: DynamicFilter }>({})
  const appliedSearchForm = reactive<Record<string, unknown> & { dynamicFilter?: DynamicFilter }>({})
  const categoryDialogVisible = ref(false)
  const itemDialogVisible = ref(false)
  const categoryDialogTab = ref('form')
  const itemDialogTab = ref('form')
  const categoryRawData = ref<Partial<Category> | Record<string, unknown>>({})
  const itemRawData = ref<Partial<Item> | Record<string, unknown>>({})
  const categoryForm = reactive({
    id: '',
    code: '',
    name: '',
    parentId: null as string | null,
    sort: 0
  })
  const itemForm = reactive({ id: '', value: '', label: '', sort: 0, isEnabled: true, remark: '' })
  const { t } = useI18n()
  const getCategoryName = (category: Category) =>
    category.code === 'system_settings'
      ? t('menus.dictionaryCategories.systemSettings')
      : category.code === 'scheduled_job_placeholders'
        ? t('menus.dictionaryCategories.scheduledJobPlaceholders')
      : category.name

  const flattenCategories = (nodes: Category[], depth = 0): Array<Category & { label: string }> =>
    nodes.flatMap((node) => [
      { ...node, label: `${'　'.repeat(depth)}${getCategoryName(node)}` },
      ...flattenCategories(node.children, depth + 1)
    ])
  const categoryOptions = computed(() => flattenCategories(categories.value))
  const translate = (key: string) => {
    const value = t(key)
    return value === key ? key : value
  }
  const advancedQueryFields = computed<DynamicQueryField[]>(() => filterFields.value.map((field) => ({
    field: field.field,
    label: translate(field.label),
    type: field.valueType
  })))
  const getItemFieldValue = (item: Item, field: string) => {
    const property = field.charAt(0).toLowerCase() + field.slice(1)
    return item[property as keyof Item]
  }
  const matchesFilter = (item: Item, filter?: DynamicFilter): boolean => {
    if (!filter) return true
    if (filter.filters?.length) {
      const results = filter.filters.map((child) => matchesFilter(item, child))
      return filter.logic === 'Or' ? results.some(Boolean) : results.every(Boolean)
    }
    if (!filter.field || !filter.operator) return true
    const actual = getItemFieldValue(item, filter.field)
    const expected = filter.value
    if (filter.operator === 'Equal') return actual === expected
    if (filter.operator === 'NotEqual') return actual !== expected
    if (filter.operator === 'Contains') return String(actual ?? '').toLowerCase().includes(String(expected ?? '').toLowerCase())
    if (filter.operator === 'StartsWith') return String(actual ?? '').toLowerCase().startsWith(String(expected ?? '').toLowerCase())
    if (filter.operator === 'EndsWith') return String(actual ?? '').toLowerCase().endsWith(String(expected ?? '').toLowerCase())
    if (filter.operator === 'GreaterThan') return Number(actual) > Number(expected)
    if (filter.operator === 'GreaterThanOrEqual') return Number(actual) >= Number(expected)
    if (filter.operator === 'LessThan') return Number(actual) < Number(expected)
    if (filter.operator === 'LessThanOrEqual') return Number(actual) <= Number(expected)
    return true
  }
  const filteredItems = computed(() => {
    return items.value.filter((item) => {
      const matchesFields = filterFields.value.every((field) => {
        const expected = appliedSearchForm[field.field]
        if (expected === undefined || expected === null || expected === '') return true
        const actual = getItemFieldValue(item, field.field)
        return field.control === 'select' ? String(actual) === String(expected) : String(actual ?? '').toLowerCase().includes(String(expected).toLowerCase())
      })
      return matchesFields && matchesFilter(item, appliedSearchForm.dynamicFilter)
    })
  })

  const loadCategories = async (preferredId?: string) => {
    categories.value = await fetchGetDictionaryCategories()
    const id = preferredId ?? selectedCategory.value?.id ?? categories.value[0]?.id
    const selected = flattenCategories(categories.value).find((item) => item.id === id)
    selectedCategory.value = selected
    if (selected) {
      await nextTick()
      treeRef.value?.setCurrentKey(selected.id)
      await loadItems()
    } else items.value = []
  }
  const loadItems = async () => {
    if (!selectedCategory.value) {
      items.value = []
      return
    }
    loading.value = true
    try {
      items.value = await fetchGetDictionaryItems(selectedCategory.value.id)
    } finally {
      loading.value = false
    }
  }
  const handleSearch = (params: typeof searchForm) => {
    Object.keys(appliedSearchForm).forEach((key) => delete appliedSearchForm[key])
    Object.assign(appliedSearchForm, {
      ...params,
      dynamicFilter: params.dynamicFilter ? structuredClone(params.dynamicFilter) : undefined
    })
  }
  const resetSearch = () => {
    Object.keys(searchForm).forEach((key) => delete searchForm[key])
    Object.keys(appliedSearchForm).forEach((key) => delete appliedSearchForm[key])
  }
  const handleCellQuery = (condition: DynamicFilter) => {
    const currentFilter = appliedSearchForm.dynamicFilter
    appliedSearchForm.dynamicFilter = currentFilter
      ? { logic: 'And', filters: [currentFilter, condition] }
      : condition
  }
  const selectCategory = async (category: Category) => {
    selectedCategory.value = category
    await loadItems()
  }
  const openCategoryDialog = (category?: Category, parentId: string | null = null) => {
    Object.assign(
      categoryForm,
      category
        ? { ...category }
        : { id: '', code: '', name: '', parentId, sort: 0 }
    )
    categoryRawData.value = category ? { ...category } : { ...categoryForm }
    categoryDialogTab.value = 'form'
    categoryDialogVisible.value = true
  }
  const saveCategory = async () => {
    if (saving.value) return
    if (!categoryForm.name.trim() || !categoryForm.code.trim()) {
      ElMessage.warning('请填写目录名称和编码')
      return
    }
    saving.value = true
    try {
      const data = {
        code: categoryForm.code,
        name: categoryForm.name,
        parentId: categoryForm.parentId,
        sort: categoryForm.sort
      }
      const saved = categoryForm.id
        ? await fetchUpdateDictionaryCategory(categoryForm.id, data)
        : await fetchCreateDictionaryCategory(data)
      categoryDialogVisible.value = false
      ElMessage.success('字典目录已保存')
      await loadCategories(saved.id)
    } finally {
      saving.value = false
    }
  }
  const deleteCategory = async (category: Category) => {
    await ElMessageBox.confirm(`确定删除目录“${category.name}”吗？`, '删除确认', {
      type: 'warning'
    })
    await fetchDeleteDictionaryCategory(category.id)
    if (selectedCategory.value?.id === category.id) selectedCategory.value = undefined
    await loadCategories()
  }
  const openItemDialog = (item?: Partial<Item>) => {
    Object.assign(
      itemForm,
      item || { id: '', value: '', label: '', sort: 0, isEnabled: true, remark: '' }
    )
    itemRawData.value = item ? { ...item } : { ...itemForm }
    itemDialogTab.value = 'form'
    itemDialogVisible.value = true
  }
  const saveItem = async () => {
    if (saving.value) return
    if (!selectedCategory.value || !itemForm.label.trim() || !itemForm.value.trim()) {
      ElMessage.warning('请填写标签和键值')
      return
    }
    saving.value = true
    try {
      const data = {
        value: itemForm.value,
        label: itemForm.label,
        sort: itemForm.sort,
        isEnabled: itemForm.isEnabled,
        remark: itemForm.remark
      }
      if (itemForm.id) await fetchUpdateDictionaryItem(itemForm.id, data)
      else await fetchCreateDictionaryItem(selectedCategory.value.id, data)
      itemDialogVisible.value = false
      ElMessage.success('字典内容已保存')
      await loadItems()
    } finally {
      saving.value = false
    }
  }
  const deleteItem = async (item: Pick<Item, 'id' | 'label'>) => {
    await ElMessageBox.confirm(`确定删除字典内容“${item.label}”吗？`, '删除确认', {
      type: 'warning'
    })
    await fetchDeleteDictionaryItem(item.id)
    await loadItems()
  }
  onMounted(async () => {
    filterFields.value = await fetchGetDictionaryFilterFields()
    await loadCategories()
  })
</script>

<style scoped>
  .dictionary-layout {
    display: grid;
    grid-template-columns: minmax(240px, 300px) minmax(0, 1fr);
    gap: 12px;
    height: 100%;
    margin-top: 12px;
  }
  .directory-panel,
  .content-panel {
    min-height: 0;
    margin-top: 0;
  }
  .directory-panel :deep(.el-card__body) {
    height: calc(100% - 57px);
  }
  .tree-node {
    display: flex;
    align-items: center;
    justify-content: space-between;
    width: calc(100% - 8px);
    min-width: 0;
  }
  .tree-node-main {
    display: flex;
    min-width: 0;
    flex: 1;
    align-items: center;
    gap: 8px;
  }
  .tree-node-main :deep(.list-id-cell) {
    width: 130px;
    flex-shrink: 0;
  }
  .node-actions {
    display: none;
    flex-shrink: 0;
  }
  .enabled-filter {
    width: 92px;
  }
  .tree-node:hover .node-actions {
    display: flex;
  }
  @media (max-width: 768px) {
    .dictionary-layout {
      grid-template-columns: 1fr;
      height: auto;
    }
    .directory-panel {
      height: 320px;
    }
    .content-panel {
      min-height: 520px;
    }
  }
</style>

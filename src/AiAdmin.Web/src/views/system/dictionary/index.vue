<template>
    <div class="art-full-height">
        <div class="dictionary-layout">
            <ElCard class="directory-panel art-card-xs">
                <template #header>
                    <div class="flex items-center justify-between">
                        <span class="font-medium">字典目录</span>
                        <div class="flex items-center gap-2">
                            <ElButton v-auth="'add'" @click="openCategoryDialog()" circle text title="新增根目录">
                                <ArtSvgIcon icon="ri:add-line" />
                            </ElButton>
                        </div>
                    </div>
                </template>

                <ElScrollbar>
                    <ElTree
                        :data="categories"
                        :expand-on-click-node="false"
                        :props="{ label: 'name', children: 'children' }"
                        @node-click="selectCategory"
                        default-expand-all
                        highlight-current
                        node-key="id"
                        ref="treeRef">
                        <template #default="{ data }">
                            <div class="tree-node">
                                <div class="tree-node-main">
                                    <span class="truncate">{{ getCategoryName(data) }}</span>
                                </div>
                                <div class="node-actions">
                                    <ElButton @click.stop="openCategoryDialog(undefined, data.id)" circle text title="新增子目录">
                                        <ArtSvgIcon icon="ri:add-line" />
                                    </ElButton>
                                    <ElButton @click.stop="openCategoryDialog(data)" circle text title="编辑目录">
                                        <ArtSvgIcon icon="ri:edit-2-line" />
                                    </ElButton>
                                    <ElButton @click.stop="deleteCategory(data)" circle text title="删除目录">
                                        <ArtSvgIcon icon="ri:delete-bin-4-line" />
                                    </ElButton>
                                </div>
                            </div>
                        </template>
                    </ElTree>
                </ElScrollbar>
            </ElCard>

            <ArtTablePage
                v-model:column-checks="columnChecks"
                :columns="columns"
                :data="items"
                :default-filter="defaultFilter"
                :filter-groups-loader="loadDictionaryFilterGroups"
                :key="selectedCategory?.id || 'empty'"
                :loading="loading"
                :pagination="pagination"
                @filter-change="handleFilterChange"
                @page-change="handleCurrentChange"
                @refresh="refreshData"
                @reset="resetSearchParams"
                @size-change="handleSizeChange"
                @sort-change="handleSortChange"
                class="content-panel"
                reserve-filter-groups
                resource="dictionary">
                <template #header-left>
                    <div class="flex items-center gap-3">
                        <ElButton v-auth="'add'" :disabled="!selectedCategory" @click="openItemDialog()">
                            <ArtSvgIcon class="mr-1" icon="ri:add-line" />新增字典内容
                        </ElButton>
                        <span class="text-g-500">{{ selectedCategory ? getCategoryName(selectedCategory) : '请选择字典目录' }}</span>
                    </div>
                </template>
            </ArtTablePage>
        </div>

        <ElDialog v-model="categoryDialogVisible" :title="categoryForm.id ? '编辑字典目录' : '新增字典目录'" destroy-on-close width="520px">
            <ElTabs v-model="categoryDialogTab">
                <ElTabPane label="基本信息" name="form">
                    <ElForm label-width="90px">
                        <ElFormItem label="上级目录"
                            ><ElSelect v-model="categoryForm.parentId" class="w-full" clearable filterable placeholder="根目录"
                                ><ElOption
                                    v-for="option in categoryOptions"
                                    :disabled="categoryForm.id === option.id"
                                    :key="option.id"
                                    :label="option.label"
                                    :value="option.id" /></ElSelect
                        ></ElFormItem>
                        <ElFormItem label="目录名称" required><ElInput v-model="categoryForm.name" maxlength="100" /></ElFormItem>
                        <ElFormItem label="目录编码" required><ElInput v-model="categoryForm.code" maxlength="100" /></ElFormItem>
                        <ElFormItem label="排序"><ElInputNumber v-model="categoryForm.sort" :max="9999" :min="0" /></ElFormItem>
                    </ElForm>
                </ElTabPane>
                <ElTabPane v-if="categoryForm.id" label="原始数据" name="raw-data"><ArtRawData :data="categoryRawData" /></ElTabPane>
            </ElTabs>
            <template #footer
                ><ElButton @click="categoryDialogVisible = false">取消</ElButton
                ><ElButton :loading="saving" @click="saveCategory" type="primary">保存</ElButton></template
            >
        </ElDialog>

        <ElDialog v-model="itemDialogVisible" :title="itemForm.id ? '编辑字典内容' : '新增字典内容'" destroy-on-close width="520px">
            <ElTabs v-model="itemDialogTab">
                <ElTabPane label="基本信息" name="form">
                    <ElForm label-width="80px">
                        <ElFormItem label="标签" required><ElInput v-model="itemForm.label" maxlength="100" /></ElFormItem>
                        <ElFormItem label="键值" required><ElInput v-model="itemForm.value" maxlength="100" /></ElFormItem>
                        <ElFormItem label="排序"><ElInputNumber v-model="itemForm.sort" :max="9999" :min="0" /></ElFormItem>
                        <ElFormItem label="是否启用"><ElSwitch v-model="itemForm.isEnabled" /></ElFormItem>
                        <ElFormItem label="备注"
                            ><ElInput v-model="itemForm.remark" :rows="3" maxlength="500" show-word-limit type="textarea"
                        /></ElFormItem>
                    </ElForm>
                </ElTabPane>
                <ElTabPane v-if="itemForm.id" label="原始数据" name="raw-data"><ArtRawData :data="itemRawData" /></ElTabPane>
            </ElTabs>
            <template #footer
                ><ElButton @click="itemDialogVisible = false">取消</ElButton
                ><ElButton :loading="saving" @click="saveItem" type="primary">保存</ElButton></template
            >
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { ElMessage, ElMessageBox } from 'element-plus'
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import { useTable } from '@/hooks/core/useTable'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import {
    fetchCreateDictionaryCategory,
    fetchCreateDictionaryItem,
    fetchDeleteDictionaryCategory,
    fetchDeleteDictionaryItem,
    fetchGetDictionaryCategories,
    fetchGetDictionaryFilterGroups,
    fetchGetDictionaryItemList,
    fetchUpdateDictionaryCategory,
    fetchUpdateDictionaryItem,
} from '@/api/system-manage'

defineOptions({ name: 'DictionaryManagement' })
type Category = Api.SystemManage.DictionaryCategory
type Item = Api.SystemManage.DictionaryItem
const categories = ref<Category[]>([])
const selectedCategory = ref<Category>()
const treeRef = ref()
const saving = ref(false)
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
    sort: 0,
})
const itemForm = reactive({ id: '', value: '', label: '', sort: 0, isEnabled: true, remark: '' })
const { t } = useI18n()
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const getCategoryName = (category: Category) =>
    category.code === 'system_settings'
        ? t('menus.dictionaryCategories.systemSettings')
        : category.code === 'scheduled_job_placeholders'
          ? t('menus.dictionaryCategories.scheduledJobPlaceholders')
          : category.name

const flattenCategories = (nodes: Category[], depth = 0): Array<Category & { label: string }> =>
    nodes.flatMap((node) => [{ ...node, label: `${'　'.repeat(depth)}${getCategoryName(node)}` }, ...flattenCategories(node.children, depth + 1)])
const categoryOptions = computed(() => flattenCategories(categories.value))
const columnsFactory = () => [
    {
        prop: 'id',
        label: 'ID',
        queryField: 'Id',
        queryValueType: 'number',
        width: 150,
        align: 'left' as const,
        formatter: (row: Item) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
    },
    { prop: 'label', label: '标签', queryField: 'Label', minWidth: 160, sortable: 'custom' as const },
    { prop: 'value', label: '键值', queryField: 'Value', minWidth: 160, sortable: 'custom' as const },
    { prop: 'sort', label: '排序', queryField: 'Sort', queryValueType: 'number', width: 90, align: 'right' as const, sortable: 'custom' as const },
    {
        prop: 'isEnabled',
        label: '是否启用',
        queryField: 'IsEnabled',
        queryValueType: 'boolean',
        width: 120,
        align: 'center' as const,
        formatter: (row: Item) =>
            h(ArtEnabledSwitch, { id: row.id, resource: 'dictionary-item', modelValue: row.isEnabled, 'onUpdate:modelValue': () => refreshData() }),
    },
    { prop: 'remark', label: '备注', queryField: 'Remark', minWidth: 180, showOverflowTooltip: true, sortable: 'custom' as const },
    {
        prop: 'operation',
        label: '操作',
        width: 110,
        align: 'center' as const,
        queryField: false,
        formatter: (row: Item) =>
            h('div', [
                h(ArtButtonTable, { type: 'edit', onClick: () => openItemDialog(row) }),
                h(ArtButtonTable, { type: 'delete', onClick: () => deleteItem(row) }),
            ]),
    },
]
const tableState = useTable({
    core: {
        apiFn: (params: { current: number; size: number; dynamicFilter?: DynamicFilter; sortField?: string; sortOrder?: 'asc' | 'desc' }) =>
            selectedCategory.value
                ? fetchGetDictionaryItemList(selectedCategory.value.id, params)
                : Promise.resolve({ data: { records: [], current: 1, size: 20, total: 0 } } as any),
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter },
        columnsFactory,
    },
})
const {
    data: items,
    loading,
    pagination,
    columnChecks,
    columns,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    replaceSearchParams,
    resetSearchParams,
} = tableState
const handleFilterChange = (filter?: DynamicFilter) => replaceSearchParams({ dynamicFilter: filter })
const loadDictionaryFilterGroups = (filter?: DynamicFilter) =>
    selectedCategory.value ? fetchGetDictionaryFilterGroups(selectedCategory.value.id, filter) : Promise.resolve([])

const loadCategories = async (preferredId?: string) => {
    categories.value = await fetchGetDictionaryCategories()
    const id = preferredId ?? selectedCategory.value?.id ?? categories.value[0]?.id
    const selected = flattenCategories(categories.value).find((item) => item.id === id)
    selectedCategory.value = selected
    if (selected) {
        await nextTick()
        treeRef.value?.setCurrentKey(selected.id)
        await refreshData()
    }
}
const selectCategory = async (category: Category) => {
    selectedCategory.value = category
    resetSearchParams()
    await nextTick()
    await refreshData()
}
const openCategoryDialog = (category?: Category, parentId: string | null = null) => {
    Object.assign(categoryForm, category ? { ...category } : { id: '', code: '', name: '', parentId, sort: 0 })
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
            sort: categoryForm.sort,
        }
        const saved = categoryForm.id ? await fetchUpdateDictionaryCategory(categoryForm.id, data) : await fetchCreateDictionaryCategory(data)
        categoryDialogVisible.value = false
        ElMessage.success('字典目录已保存')
        await loadCategories(saved.id)
    } finally {
        saving.value = false
    }
}
const deleteCategory = async (category: Category) => {
    await ElMessageBox.confirm(`确定删除目录“${category.name}”吗？`, '删除确认', {
        type: 'warning',
    })
    await fetchDeleteDictionaryCategory(category.id)
    if (selectedCategory.value?.id === category.id) selectedCategory.value = undefined
    await loadCategories()
}
const openItemDialog = (item?: Partial<Item>) => {
    Object.assign(itemForm, item || { id: '', value: '', label: '', sort: 0, isEnabled: true, remark: '' })
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
            remark: itemForm.remark,
        }
        if (itemForm.id) await fetchUpdateDictionaryItem(itemForm.id, data)
        else await fetchCreateDictionaryItem(selectedCategory.value.id, data)
        itemDialogVisible.value = false
        ElMessage.success('字典内容已保存')
        await refreshData()
    } finally {
        saving.value = false
    }
}
const deleteItem = async (item: Pick<Item, 'id' | 'label'>) => {
    await ElMessageBox.confirm(`确定删除字典内容“${item.label}”吗？`, '删除确认', {
        type: 'warning',
    })
    await fetchDeleteDictionaryItem(item.id)
    await refreshData()
}
onMounted(async () => {
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
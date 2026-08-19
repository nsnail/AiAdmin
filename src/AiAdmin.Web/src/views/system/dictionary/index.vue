<template>
  <div class="art-full-height">
    <div class="dictionary-layout">
      <ElCard class="directory-panel art-card-xs">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-medium">字典目录</span>
            <ElButton v-auth="'add'" text circle title="新增根目录" @click="openCategoryDialog()">
              <ArtSvgIcon icon="ri:add-line" />
            </ElButton>
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
                <span class="truncate">{{ getCategoryName(data) }}</span>
                <div class="node-actions">
                  <ElButton text circle title="新增子目录" @click.stop="openCategoryDialog(undefined, data.id)">
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
              <span class="text-g-500">{{ selectedCategory ? getCategoryName(selectedCategory) : '请选择字典目录' }}</span>
            </div>
          </template>
        </ArtTableHeader>

        <ElTable v-loading="loading" :data="items" height="calc(100vh - 220px)">
          <ElTableColumn prop="label" label="标签" min-width="160" />
          <ElTableColumn prop="value" label="键值" min-width="160" />
          <ElTableColumn prop="sort" label="排序" width="90" />
          <ElTableColumn label="状态" width="100">
            <template #default="{ row }"><ElTag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</ElTag></template>
          </ElTableColumn>
          <ElTableColumn prop="remark" label="备注" min-width="180" show-overflow-tooltip />
          <ElTableColumn label="操作" width="110" fixed="right">
            <template #default="{ row }">
              <ElButton text circle title="编辑" @click="openItemDialog(row)"><ArtSvgIcon icon="ri:edit-2-line" /></ElButton>
              <ElButton text circle title="删除" @click="deleteItem(row)"><ArtSvgIcon icon="ri:delete-bin-4-line" class="text-danger" /></ElButton>
            </template>
          </ElTableColumn>
        </ElTable>
      </ElCard>
    </div>

    <ElDialog v-model="categoryDialogVisible" :title="categoryForm.id ? '编辑字典目录' : '新增字典目录'" width="520px" destroy-on-close>
      <ElForm label-width="90px">
        <ElFormItem label="上级目录"><ElSelect v-model="categoryForm.parentId" clearable class="w-full" placeholder="根目录"><ElOption v-for="option in categoryOptions" :key="option.id" :label="option.label" :value="option.id" :disabled="categoryForm.id === option.id" /></ElSelect></ElFormItem>
        <ElFormItem label="目录名称" required><ElInput v-model="categoryForm.name" maxlength="100" /></ElFormItem>
        <ElFormItem label="目录编码" required><ElInput v-model="categoryForm.code" maxlength="100" /></ElFormItem>
        <ElFormItem label="排序"><ElInputNumber v-model="categoryForm.sort" :min="0" :max="9999" /></ElFormItem>
        <ElFormItem label="状态"><ElSwitch v-model="categoryForm.isEnabled" /></ElFormItem>
      </ElForm>
      <template #footer><ElButton @click="categoryDialogVisible = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="saveCategory">保存</ElButton></template>
    </ElDialog>

    <ElDialog v-model="itemDialogVisible" :title="itemForm.id ? '编辑字典内容' : '新增字典内容'" width="520px" destroy-on-close>
      <ElForm label-width="80px">
        <ElFormItem label="标签" required><ElInput v-model="itemForm.label" maxlength="100" /></ElFormItem>
        <ElFormItem label="键值" required><ElInput v-model="itemForm.value" maxlength="100" /></ElFormItem>
        <ElFormItem label="排序"><ElInputNumber v-model="itemForm.sort" :min="0" :max="9999" /></ElFormItem>
        <ElFormItem label="状态"><ElSwitch v-model="itemForm.isEnabled" /></ElFormItem>
        <ElFormItem label="备注"><ElInput v-model="itemForm.remark" type="textarea" :rows="3" maxlength="500" show-word-limit /></ElFormItem>
      </ElForm>
      <template #footer><ElButton @click="itemDialogVisible = false">取消</ElButton><ElButton type="primary" :loading="saving" @click="saveItem">保存</ElButton></template>
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import {
    fetchCreateDictionaryCategory, fetchCreateDictionaryItem, fetchDeleteDictionaryCategory,
    fetchDeleteDictionaryItem, fetchGetDictionaryCategories, fetchGetDictionaryItems,
    fetchUpdateDictionaryCategory, fetchUpdateDictionaryItem
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
  const categoryDialogVisible = ref(false)
  const itemDialogVisible = ref(false)
  const categoryForm = reactive({ id: 0, code: '', name: '', parentId: null as number | null, sort: 0, isEnabled: true })
  const itemForm = reactive({ id: 0, value: '', label: '', sort: 0, isEnabled: true, remark: '' })
  const { t } = useI18n()
  const getCategoryName = (category: Category) => category.code === 'system_settings' ? t('menus.dictionaryCategories.systemSettings') : category.name

  const flattenCategories = (nodes: Category[], depth = 0): Array<Category & { label: string }> =>
    nodes.flatMap((node) => [{ ...node, label: `${'　'.repeat(depth)}${getCategoryName(node)}` }, ...flattenCategories(node.children, depth + 1)])
  const categoryOptions = computed(() => flattenCategories(categories.value))

  const loadCategories = async (preferredId?: number) => {
    categories.value = await fetchGetDictionaryCategories()
    const id = preferredId ?? selectedCategory.value?.id ?? categories.value[0]?.id
    const selected = flattenCategories(categories.value).find((item) => item.id === id)
    selectedCategory.value = selected
    if (selected) { await nextTick(); treeRef.value?.setCurrentKey(selected.id); await loadItems() } else items.value = []
  }
  const loadItems = async () => {
    if (!selectedCategory.value) { items.value = []; return }
    loading.value = true
    try { items.value = await fetchGetDictionaryItems(selectedCategory.value.id) } finally { loading.value = false }
  }
  const selectCategory = async (category: Category) => { selectedCategory.value = category; await loadItems() }
  const openCategoryDialog = (category?: Category, parentId: number | null = null) => {
    Object.assign(categoryForm, category ? { ...category } : { id: 0, code: '', name: '', parentId, sort: 0, isEnabled: true })
    categoryDialogVisible.value = true
  }
  const saveCategory = async () => {
    if (!categoryForm.name.trim() || !categoryForm.code.trim()) { ElMessage.warning('请填写目录名称和编码'); return }
    saving.value = true
    try {
      const data = { code: categoryForm.code, name: categoryForm.name, parentId: categoryForm.parentId, sort: categoryForm.sort, isEnabled: categoryForm.isEnabled }
      const saved = categoryForm.id ? await fetchUpdateDictionaryCategory(categoryForm.id, data) : await fetchCreateDictionaryCategory(data)
      categoryDialogVisible.value = false; ElMessage.success('字典目录已保存'); await loadCategories(saved.id)
    } finally { saving.value = false }
  }
  const deleteCategory = async (category: Category) => {
    await ElMessageBox.confirm(`确定删除目录“${category.name}”吗？`, '删除确认', { type: 'warning' })
    await fetchDeleteDictionaryCategory(category.id); if (selectedCategory.value?.id === category.id) selectedCategory.value = undefined; await loadCategories()
  }
  const openItemDialog = (item?: Partial<Item>) => { Object.assign(itemForm, item || { id: 0, value: '', label: '', sort: 0, isEnabled: true, remark: '' }); itemDialogVisible.value = true }
  const saveItem = async () => {
    if (!selectedCategory.value || !itemForm.label.trim() || !itemForm.value.trim()) { ElMessage.warning('请填写标签和键值'); return }
    saving.value = true
    try {
      const data = { value: itemForm.value, label: itemForm.label, sort: itemForm.sort, isEnabled: itemForm.isEnabled, remark: itemForm.remark }
      if (itemForm.id) await fetchUpdateDictionaryItem(itemForm.id, data); else await fetchCreateDictionaryItem(selectedCategory.value.id, data)
      itemDialogVisible.value = false; ElMessage.success('字典内容已保存'); await loadItems()
    } finally { saving.value = false }
  }
  const deleteItem = async (item: Pick<Item, 'id' | 'label'>) => { await ElMessageBox.confirm(`确定删除字典内容“${item.label}”吗？`, '删除确认', { type: 'warning' }); await fetchDeleteDictionaryItem(item.id); await loadItems() }
  onMounted(() => loadCategories())
</script>

<style scoped>
  .dictionary-layout { display: grid; grid-template-columns: minmax(240px, 300px) minmax(0, 1fr); gap: 12px; height: 100%; }
  .directory-panel, .content-panel { min-height: 0; }
  .directory-panel :deep(.el-card__body) { height: calc(100% - 57px); }
  .tree-node { display: flex; align-items: center; justify-content: space-between; width: calc(100% - 8px); min-width: 0; }
  .node-actions { display: none; flex-shrink: 0; }
  .tree-node:hover .node-actions { display: flex; }
  @media (max-width: 768px) { .dictionary-layout { grid-template-columns: 1fr; height: auto; } .directory-panel { height: 320px; } .content-panel { min-height: 520px; } }
</style>

<template>
  <div class="department-page art-full-height">
    <ArtSearchBar
      v-model="searchForm"
      :items="searchItems"
      :show-expand="false"
      @search="applySearch"
      @reset="resetSearch"
    />

    <ElCard class="art-table-card">
      <ArtTableHeader
        v-model:columns="columnChecks"
        :show-zebra="false"
        :loading="loading"
        @refresh="loadDepartments"
      >
        <template #left>
          <ElButton v-auth="'add'" @click="showDialog('add')" v-ripple>新增部门</ElButton>
          <ElButton @click="toggleExpand" v-ripple>{{ expanded ? '收起' : '展开' }}</ElButton>
        </template>
      </ArtTableHeader>

      <ArtTable
        ref="tableRef"
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="filteredDepartments"
        :stripe="false"
        :tree-props="{ children: 'children' }"
      />
    </ElCard>

    <DepartmentDialog
      v-model:visible="dialogVisible"
      :type="dialogType"
      :department-data="currentDepartment"
      :departments="departments"
      @submit="saveDepartment"
    />
  </div>
</template>

<script setup lang="ts">
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import { useTableColumns } from '@/hooks/core/useTableColumns'
  import {
    fetchCreateDepartment,
    fetchDeleteDepartment,
    fetchGetDepartmentTree,
    fetchUpdateDepartment
  } from '@/api/system-manage'
  import DepartmentDialog from './modules/department-dialog.vue'
  import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'

  defineOptions({ name: 'Department' })

  type Department = Api.SystemManage.DepartmentTreeItem
  type SaveDepartment = Api.SystemManage.SaveDepartmentParams
  const { t } = useI18n()
  const defaultDepartmentCode = 'DEFAULT'
  const getDepartmentName = (department: Department): string =>
    department.code === defaultDepartmentCode ? t('userManagement.defaultDepartment') : department.name

  const loading = ref(false)
  const expanded = ref(false)
  const tableRef = ref()
  const departments = ref<Department[]>([])
  const dialogVisible = ref(false)
  const dialogType = ref<'add' | 'edit'>('add')
  const currentDepartment = ref<Partial<Department>>({})
  const searchForm = reactive({ keyword: '' })
  const appliedKeyword = ref('')
  const searchItems = [{ label: '部门名称/编码', key: 'keyword', type: 'input', props: { clearable: true } }]

  const { columnChecks, columns } = useTableColumns(() => [
    { prop: 'name', label: '部门名称', minWidth: 200, formatter: (row: Department) => getDepartmentName(row) },
    { prop: 'code', label: '部门编码', minWidth: 140 },
    { prop: 'sort', label: '排序', width: 80 },
    { prop: 'leader', label: '负责人', minWidth: 110 },
    { prop: 'phone', label: '联系电话', minWidth: 140 },
    { prop: 'email', label: '邮箱', minWidth: 180 },
    {
      prop: 'isEnabled',
      label: '状态',
      width: 90,
      formatter: (row: Department) =>
        h(ElTag, { type: row.isEnabled ? 'success' : 'info' }, () =>
          row.isEnabled ? '启用' : '停用'
        )
    },
    {
      prop: 'operation',
      label: '操作',
      width: 150,
      fixed: 'right',
      align: 'right',
      formatter: (row: Department) =>
        h('div', { class: 'flex justify-end' }, [
          h(ArtButtonTable, { type: 'add', onClick: () => showDialog('add', row) }),
          h(ArtButtonTable, { type: 'edit', onClick: () => showDialog('edit', row) }),
          h(ArtButtonTable, { type: 'delete', onClick: () => deleteDepartment(row) })
        ])
    }
  ])

  const filterTree = (items: Department[], keyword: string): Department[] => {
    if (!keyword) return items
    return items.flatMap((item) => {
      const children = filterTree(item.children, keyword)
      const matches = getDepartmentName(item).toLowerCase().includes(keyword) || item.name.toLowerCase().includes(keyword) || item.code.toLowerCase().includes(keyword)
      return matches || children.length ? [{ ...item, children }] : []
    })
  }

  const filteredDepartments = computed(() =>
    filterTree(departments.value, appliedKeyword.value.trim().toLowerCase())
  )

  const loadDepartments = async (): Promise<void> => {
    loading.value = true
    try {
      departments.value = await fetchGetDepartmentTree()
    } finally {
      loading.value = false
    }
  }

  const applySearch = (): void => {
    appliedKeyword.value = searchForm.keyword
  }

  const resetSearch = (): void => {
    searchForm.keyword = ''
    appliedKeyword.value = ''
  }

  const showDialog = (type: 'add' | 'edit', row?: Department): void => {
    dialogType.value = type
    currentDepartment.value =
      type === 'edit' ? row ?? {} : { parentId: row?.id ?? null }
    dialogVisible.value = true
  }

  const saveDepartment = async (form: SaveDepartment): Promise<void> => {
    if (dialogType.value === 'add') await fetchCreateDepartment(form)
    else await fetchUpdateDepartment(currentDepartment.value.id!, form)
    ElMessage.success(dialogType.value === 'add' ? '部门创建成功' : '部门更新成功')
    dialogVisible.value = false
    await loadDepartments()
  }

  const deleteDepartment = async (row: Department): Promise<void> => {
    await ElMessageBox.confirm(`确定要删除部门“${row.name}”吗？`, '删除部门', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await fetchDeleteDepartment(row.id)
    await loadDepartments()
  }

  const toggleExpand = (): void => {
    expanded.value = !expanded.value
    nextTick(() => {
      const visit = (items: Department[]) =>
        items.forEach((item) => {
          tableRef.value?.elTableRef?.toggleRowExpansion(item, expanded.value)
          visit(item.children)
        })
      visit(filteredDepartments.value)
    })
  }

  onMounted(loadDepartments)
</script>

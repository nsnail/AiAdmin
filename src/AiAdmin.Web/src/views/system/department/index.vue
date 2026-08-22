<template>
    <div class="department-page art-full-height">
        <ArtSearchBar v-model="searchForm" :filter-fields="filterFields" :show-expand="false" @reset="resetSearch" @search="applySearch" />

        <ElCard class="art-table-card">
            <ArtTableHeader v-model:columns="columnChecks" :loading="loading" :show-zebra="false" @refresh="loadDepartments">
                <template #left>
                    <ElButton v-auth="'add'" v-ripple @click="showDialog('add')">新增部门</ElButton>
                    <ElButton v-ripple @click="toggleExpand">{{ expanded ? '收起' : '展开' }}</ElButton>
                </template>
            </ArtTableHeader>

            <ArtTable
                :columns="columns"
                :data="filteredDepartments"
                :loading="loading"
                :stripe="false"
                :tree-props="{ children: 'children' }"
                ref="tableRef"
                row-key="id" />
        </ElCard>

        <DepartmentDialog
            v-model:visible="dialogVisible"
            :department-data="currentDepartment"
            :departments="departments"
            :saving="dialogSaving"
            :type="dialogType"
            @submit="saveDepartment" />
    </div>
</template>

<script lang="ts" setup>
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import { useTableColumns } from '@/hooks/core/useTableColumns'
import {
    fetchCreateDepartment,
    fetchDeleteDepartment,
    fetchGetDepartmentTree,
    fetchGetListFilterFields,
    fetchUpdateDepartment,
} from '@/api/system-manage'
import DepartmentDialog from './modules/department-dialog.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
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
const dialogSaving = ref(false)
const searchForm = reactive<Record<string, unknown>>({ IsEnabled: true })
const filterFields = ref<import('@/api/system-manage').ListFilterField[]>([])
const appliedFilters = ref<Record<string, unknown>>({ IsEnabled: true })

const { columnChecks, columns } = useTableColumns(() => [
    {
        prop: 'name',
        label: '部门名称',
        minWidth: 200,
        formatter: (row: Department) => getDepartmentName(row),
    },
    { prop: 'code', label: '部门编码', minWidth: 140 },
    { prop: 'sort', label: '排序', width: 80 },
    { prop: 'leader', label: '负责人', minWidth: 110 },
    { prop: 'phone', label: '联系电话', minWidth: 140 },
    { prop: 'email', label: '邮箱', minWidth: 180 },
    {
        prop: 'isEnabled',
        label: '是否启用',
        width: 90,
        formatter: (row: Department) =>
            h(ArtEnabledSwitch, {
                id: row.id,
                resource: 'department',
                modelValue: row.isEnabled,
                'onUpdate:modelValue': () => {
                    void loadDepartments()
                },
            }),
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
                h(ArtButtonTable, { type: 'delete', onClick: () => deleteDepartment(row) }),
            ]),
    },
])

const filterTree = (items: Department[]): Department[] => {
    return items.flatMap((item) => {
        const children = filterTree(item.children)
        const name = appliedFilters.value.Name
        const code = appliedFilters.value.Code
        const enabled = appliedFilters.value.IsEnabled
        const matches =
            (typeof name !== 'string' || item.name.toLowerCase().includes(name.toLowerCase())) &&
            (typeof code !== 'string' || item.code.toLowerCase().includes(code.toLowerCase())) &&
            (typeof enabled !== 'boolean' || item.isEnabled === enabled)
        return matches || children.length ? [{ ...item, children }] : []
    })
}

const filteredDepartments = computed(() => filterTree(departments.value))

const loadDepartments = async (): Promise<void> => {
    loading.value = true
    try {
        departments.value = await fetchGetDepartmentTree()
    } finally {
        loading.value = false
    }
}

const applySearch = (params: Record<string, unknown>): void => {
    appliedFilters.value = { ...params }
}

const resetSearch = (): void => {
    Object.keys(searchForm).forEach((key) => delete searchForm[key])
    searchForm.IsEnabled = true
    appliedFilters.value = { IsEnabled: true }
}

const showDialog = (type: 'add' | 'edit', row?: Department): void => {
    dialogType.value = type
    currentDepartment.value = type === 'edit' ? (row ?? {}) : { parentId: row?.id ?? null }
    dialogVisible.value = true
}

const saveDepartment = async (form: SaveDepartment): Promise<void> => {
    if (dialogSaving.value) return
    dialogSaving.value = true
    try {
        if (dialogType.value === 'add') await fetchCreateDepartment(form)
        else await fetchUpdateDepartment(currentDepartment.value.id!, form)
        ElMessage.success(dialogType.value === 'add' ? '部门创建成功' : '部门更新成功')
        dialogVisible.value = false
        await loadDepartments()
    } finally {
        dialogSaving.value = false
    }
}

const deleteDepartment = async (row: Department): Promise<void> => {
    await ElMessageBox.confirm(`确定要删除部门“${row.name}”吗？`, '删除部门', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
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

onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('department')
    await loadDepartments()
})
</script>

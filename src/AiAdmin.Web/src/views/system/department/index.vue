<template>
    <ArtTablePage
        v-model:column-checks="columnChecks"
        :columns="columns"
        :data="data"
        :default-filter="defaultFilter"
        :loading="loading"
        :pagination="pagination"
        :row-key="'id'"
        :tree-props="{ children: 'children' }"
        @filter-change="handleFilterChange"
        @page-change="handleCurrentChange"
        @refresh="refreshData"
        @reset="resetSearchParams"
        @size-change="handleSizeChange"
        @sort-change="handleSortChange"
        ref="tablePageRef"
        resource="department">
        <template #header-left>
            <ElSpace>
                <ElButton v-auth="'add'" v-ripple @click="showDialog('add')">{{ t('departmentManagement.actions.add') }}</ElButton>
                <ElButton v-ripple @click="toggleExpand">{{ expanded ? '全部收起' : '全部展开' }}</ElButton>
            </ElSpace>
        </template>
        <DepartmentDialog
            v-model:visible="dialogVisible"
            :department-data="currentDepartment"
            :departments="departments"
            :saving="dialogSaving"
            :type="dialogType"
            @submit="saveDepartment" />
    </ArtTablePage>
</template>

<script lang="ts" setup>
import {
    fetchCreateDepartment,
    fetchDeleteDepartment,
    fetchGetDepartmentList,
    fetchGetDepartmentTree,
    fetchUpdateDepartment,
} from '@/api/system-manage'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import { useTable } from '@/hooks/core/useTable'
import DepartmentDialog from './modules/department-dialog.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'Department' })
type Department = Api.SystemManage.DepartmentTreeItem
type SaveDepartment = Api.SystemManage.SaveDepartmentParams
const { t, locale } = useI18n()
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const departments = ref<Department[]>([])
const dialogVisible = ref(false)
const dialogType = ref<'add' | 'edit'>('add')
const currentDepartment = ref<Partial<Department>>({})
const dialogSaving = ref(false)
const expanded = ref(false)
const tablePageRef = ref<{ tableRef?: { elTableRef?: { toggleRowExpansion: (row: Department, expanded: boolean) => void } } }>()

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    refreshRemove,
    resetColumns,
} = useTable({
    core: {
        apiFn: fetchGetDepartmentList,
        apiParams: { current: 1, size: 20, dynamicFilter: defaultFilter },
        columnsFactory: () => [
            {
                prop: 'name',
                queryField: 'Name',
                queryValueField: 'name',
                queryValueType: 'string',
                label: t('listFilter.department.name'),
                minWidth: 180,
            },
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 160,
                align: 'left',
                formatter: (row: Department) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            { prop: 'code', queryField: 'Code', queryValueType: 'string', label: t('listFilter.department.code'), minWidth: 140 },
            { prop: 'sort', queryField: 'Sort', queryValueType: 'number', label: t('departmentManagement.fields.sort'), width: 90, align: 'right' },
            {
                prop: 'description',
                queryField: 'Description',
                queryValueType: 'string',
                label: t('departmentManagement.fields.description'),
                minWidth: 220,
            },
            {
                prop: 'isEnabled',
                queryField: 'IsEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 110,
                align: 'center',
                formatter: (row: Department) =>
                    h(ArtEnabledSwitch, {
                        id: row.id,
                        resource: 'department',
                        modelValue: row.isEnabled,
                        'onUpdate:modelValue': () => void getData(),
                    }),
            },
            {
                prop: 'operation',
                queryField: false,
                label: t('departmentManagement.fields.operation'),
                width: 150,
                fixed: 'right',
                formatter: (row: Department) =>
                    h('div', { class: 'flex justify-end' }, [
                        h(ArtButtonTable, { type: 'add', onClick: () => showDialog('add', row) }),
                        h(ArtButtonTable, { type: 'edit', onClick: () => showDialog('edit', row) }),
                        h(ArtButtonTable, { type: 'delete', onClick: () => deleteDepartment(row) }),
                    ]),
            },
        ],
    },
})

watch(locale, () => resetColumns?.())
const loadDepartments = async (): Promise<void> => {
    departments.value = await fetchGetDepartmentTree()
}
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
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
        ElMessage.success(t(dialogType.value === 'add' ? 'departmentManagement.message.created' : 'departmentManagement.message.updated'))
        dialogVisible.value = false
        await Promise.all([loadDepartments(), getData()])
    } finally {
        dialogSaving.value = false
    }
}
const deleteDepartment = (row: Department): void => {
    ElMessageBox.confirm(t('departmentManagement.confirm.deleteMessage', { name: row.name }), t('departmentManagement.confirm.deleteTitle'), {
        type: 'warning',
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
    })
        .then(async () => {
            await fetchDeleteDepartment(row.id)
            await refreshRemove()
        })
        .catch(() => undefined)
}
const toggleExpand = (): void => {
    expanded.value = !expanded.value
    const table = tablePageRef.value?.tableRef?.elTableRef
    if (!table) return
    const visit = (items: Department[]): void => {
        items.forEach((item) => {
            if (item.children?.length) {
                table.toggleRowExpansion(item, expanded.value)
                visit(item.children)
            }
        })
    }
    visit(data.value as Department[])
}
onMounted(async () => {
    await Promise.all([loadDepartments(), getData()])
})
</script>
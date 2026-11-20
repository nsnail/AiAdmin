<!-- 角色管理页面 -->
<template>
    <ArtTablePage
        v-model:column-checks="columnChecks"
        :columns="columns"
        :data="data"
        :default-filter="defaultFilter"
        :loading="loading"
        :pagination="pagination"
        :row-actions="rowActions"
        @filter-change="handleFilterChange"
        @page-change="handleCurrentChange"
        @refresh="refreshData"
        @reset="resetSearchParams"
        @selection-change="selectedRows = $event"
        @size-change="handleSizeChange"
        @sort-change="handleSortChange"
        resource="role">
        <template #header-left>
            <ElSpace wrap>
                <ElButton v-ripple @click="showDialog('add')">{{ t('roleManagement.actions.add') }}</ElButton>
            </ElSpace>
        </template>

        <!-- 角色编辑弹窗 -->
        <RoleEditDialog v-model="dialogVisible" :dialog-type="dialogType" :role-data="currentRoleData" @success="refreshData" />

        <!-- 菜单权限弹窗 -->
        <RolePermissionDialog v-model="permissionDialog" :role-data="currentRoleData" @success="refreshData" />

        <RoleApiDialog v-model="apiPermissionDialog" :role-data="currentRoleData" />
    </ArtTablePage>
</template>

<script lang="ts" setup>
import type { RowAction } from '@/types/component'
import { useTable } from '@/hooks/core/useTable'
import { fetchCopyRole, fetchDeleteRole, fetchGetRoleList } from '@/api/system-manage'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import RoleEditDialog from './modules/role-edit-dialog.vue'
import RolePermissionDialog from './modules/role-permission-dialog.vue'
import RoleApiDialog from './modules/role-api-dialog.vue'
import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'Role' })
type RoleListItem = Api.SystemManage.RoleListItem
const { t, locale } = useI18n()
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }

const dialogVisible = ref(false)
const permissionDialog = ref(false)
const apiPermissionDialog = ref(false)
const currentRoleData = ref<RoleListItem | undefined>(undefined)
const selectedRows = ref<RoleListItem[]>([])

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    searchParams,
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
    // 核心配置
    core: {
        apiFn: fetchGetRoleList,
        apiParams: {
            current: 1,
            size: 20,
            dynamicFilter: defaultFilter,
        },
        // 排除 apiParams 中的属性
        columnsFactory: () => [
            {
                type: 'selection',
                width: 48,
                fixed: 'left',
            },
            {
                prop: 'roleId',
                exportKey: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 160,
                align: 'left',
                formatter: (row) => h(ArtListIdCell, { id: row.roleId, createdAt: row.createTime }),
            },
            {
                prop: 'roleName',
                queryField: 'Name',
                queryValueType: 'number',
                label: t('listFilter.role.name'),
                minWidth: 120,
            },
            {
                prop: 'roleCode',
                queryField: 'Code',
                queryValueType: 'string',
                label: t('listFilter.role.code'),
                minWidth: 120,
            },
            {
                prop: 'description',
                queryField: 'Description',
                queryValueType: 'string',
                label: t('listFilter.role.description'),
                minWidth: 150,
                showOverflowTooltip: true,
            },
            {
                prop: 'dataScope',
                exportValue: (row) =>
                    ({
                        0: t('listFilter.option.allData'),
                        1: t('listFilter.option.departmentData'),
                        2: t('listFilter.option.departmentAndChildren'),
                        3: t('listFilter.option.ownData'),
                    })[row.dataScope as 0 | 1 | 2 | 3] ?? row.dataScope,
                queryField: 'DataScope',
                queryValueType: 'string',
                align: 'center',
                label: t('listFilter.role.dataScope'),
                minWidth: 150,
                formatter: (row) => {
                    const label = {
                        0: t('listFilter.option.allData'),
                        1: t('listFilter.option.departmentData'),
                        2: t('listFilter.option.departmentAndChildren'),
                        3: t('listFilter.option.ownData'),
                    }[row.dataScope]
                    /** 数据权限范围从大到小依次使用红、橙、绿、蓝，色相差异明显 */
                    const type = ({ 0: 'danger', 1: 'warning', 2: 'success', 3: 'primary' } as const)[row.dataScope as 0 | 1 | 2 | 3]
                    return h(
                        ElTag,
                        {
                            type,
                            effect: 'light',
                            size: 'small',
                            'data-query-field': 'DataScope',
                            'data-query-label': t('listFilter.role.dataScope'),
                            'data-query-value': row.dataScope,
                            'data-query-value-type': 'number',
                        },
                        () => label || row.dataScope,
                    )
                },
            },
            {
                prop: 'enabled',
                exportKey: 'isEnabled',
                queryField: 'IsEnabled',
                queryValueField: 'enabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 120,
                align: 'center',
                formatter: (row) =>
                    h(ArtEnabledSwitch, {
                        id: row.roleId,
                        resource: 'role',
                        modelValue: row.enabled,
                        'onUpdate:modelValue': () => {
                            void getData()
                        },
                    }),
            },
        ],
    },
})

watch(locale, () => resetColumns?.())

const dialogType = ref<'add' | 'edit'>('add')

const showDialog = (type: 'add' | 'edit', row?: RoleListItem) => {
    dialogVisible.value = true
    dialogType.value = type
    currentRoleData.value = row
}

/**
 * 搜索处理
 * @param params 搜索参数
 */
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}

/** 行右键菜单操作 */
const rowActions = (row: RoleListItem): RowAction[] => [
    {
        key: 'permission',
        shortcut: 'M',
        label: t('roleManagement.actions.menuPermission'),
        icon: 'ri:user-3-line',
        onClick: () => showPermissionDialog(row),
    },
    {
        key: 'apiPermission',
        shortcut: 'P',
        label: t('roleManagement.actions.apiPermission'),
        icon: 'ri:route-line',
        onClick: () => {
            apiPermissionDialog.value = true
            currentRoleData.value = row
        },
    },
    { key: 'edit', label: t('roleManagement.actions.edit'), icon: 'ri:edit-2-line', onClick: () => showDialog('edit', row) },
    { key: 'copy', shortcut: 'Y', label: t('roleManagement.actions.copy'), icon: 'ri:file-copy-line', onClick: () => copyRole(row) },
    { key: 'delete', label: t('roleManagement.actions.delete'), icon: 'ri:delete-bin-4-line', danger: true, onClick: () => deleteRole(row) },
]

const showPermissionDialog = (row?: RoleListItem) => {
    permissionDialog.value = true
    currentRoleData.value = row
}

const copyRole = async (row: RoleListItem): Promise<void> => {
    await fetchCopyRole(row.roleId)
    ElMessage.success(t('roleManagement.message.copied'))
    await refreshData()
}

const deleteRole = (row: RoleListItem): void => {
    ElMessageBox.confirm(t('roleManagement.confirm.deleteMessage', { name: row.roleName }), t('roleManagement.confirm.deleteTitle'), {
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
        type: 'warning',
    })
        .then(async () => {
            await fetchDeleteRole(row.roleId)
            await refreshRemove()
        })
        .catch(() => {
            ElMessage.info(t('roleManagement.message.deleteCancelled'))
        })
}
</script>
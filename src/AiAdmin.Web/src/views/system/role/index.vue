<!-- 角色管理页面 -->
<template>
    <ArtTablePage
        v-model:column-checks="columnChecks"
        :columns="columns"
        :data="data"
        :default-filter="defaultFilter"
        :loading="loading"
        :pagination="pagination"
        @filter-change="handleFilterChange"
        @page-change="handleCurrentChange"
        @refresh="refreshData"
        @reset="resetSearchParams"
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
import { ButtonMoreItem } from '@/components/core/forms/art-button-more/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchDeleteRole, fetchGetRoleList } from '@/api/system-manage'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtButtonMore from '@/components/core/forms/art-button-more/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import RoleEditDialog from './modules/role-edit-dialog.vue'
import RolePermissionDialog from './modules/role-permission-dialog.vue'
import RoleApiDialog from './modules/role-api-dialog.vue'
import { ElMessageBox } from 'element-plus'
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
                prop: 'roleName',
                queryField: 'Name',
                label: t('listFilter.role.name'),
                minWidth: 120,
            },
            {
                prop: 'roleCode',
                queryField: 'Code',
                label: t('listFilter.role.code'),
                minWidth: 120,
            },
            {
                prop: 'description',
                queryField: 'Description',
                label: t('listFilter.role.description'),
                minWidth: 150,
                showOverflowTooltip: true,
            },
            {
                prop: 'dataScope',
                queryField: 'DataScope',
                label: t('listFilter.role.dataScope'),
                minWidth: 150,
                formatter: (row) =>
                    ({
                        all: t('listFilter.option.allData'),
                        department: t('listFilter.option.departmentData'),
                        department_and_children: t('listFilter.option.departmentAndChildren'),
                        self: t('listFilter.option.ownData'),
                    })[row.dataScope] || row.dataScope,
            },
            {
                prop: 'enabled',
                queryField: 'IsEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 120,
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
            {
                prop: 'operation',
                label: t('roleManagement.fields.operation'),
                width: 100,
                fixed: 'right',
                formatter: (row) =>
                    h('div', [
                        h(ArtButtonMore, {
                            list: [
                                {
                                    key: 'permission',
                                    label: t('roleManagement.actions.menuPermission'),
                                    icon: 'ri:user-3-line',
                                },
                                {
                                    key: 'apiPermission',
                                    label: t('roleManagement.actions.apiPermission'),
                                    icon: 'ri:route-line',
                                },
                                {
                                    key: 'edit',
                                    label: t('roleManagement.actions.edit'),
                                    icon: 'ri:edit-2-line',
                                },
                                {
                                    key: 'delete',
                                    label: t('roleManagement.actions.delete'),
                                    icon: 'ri:delete-bin-4-line',
                                    color: '#f56c6c',
                                },
                            ],
                            onClick: (item: ButtonMoreItem) => buttonMoreClick(item, row),
                        }),
                    ]),
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

const buttonMoreClick = (item: ButtonMoreItem, row: RoleListItem) => {
    switch (item.key) {
        case 'permission':
            showPermissionDialog(row)
            break
        case 'apiPermission':
            apiPermissionDialog.value = true
            currentRoleData.value = row
            break
        case 'edit':
            showDialog('edit', row)
            break
        case 'delete':
            deleteRole(row)
            break
    }
}

const showPermissionDialog = (row?: RoleListItem) => {
    permissionDialog.value = true
    currentRoleData.value = row
}

const deleteRole = (row: RoleListItem) => {
    ElMessageBox.confirm(t('roleManagement.confirm.deleteMessage', { name: row.roleName }), t('roleManagement.confirm.deleteTitle'), {
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
        type: 'warning',
    })
        .then(async () => {
            await fetchDeleteRole(row.roleId)
            refreshData()
        })
        .catch(() => {
            ElMessage.info(t('roleManagement.message.deleteCancelled'))
        })
}
</script>
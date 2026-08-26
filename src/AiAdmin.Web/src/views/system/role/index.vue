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
        @selection-change="selectedRows = $event"
        @size-change="handleSizeChange"
        @sort-change="handleSortChange"
        resource="role">
        <template #header-left>
            <ElSpace wrap>
                <ElButton v-ripple @click="showDialog('add')">{{ t('roleManagement.actions.add') }}</ElButton>
                <ElDropdown :disabled="exporting" @command="handleExport">
                    <ElButton :loading="exporting">
                        <ArtSvgIcon class="mr-1" icon="ri:download-2-line" />{{ t('roleManagement.actions.exportData') }}
                        <ArtSvgIcon class="ml-1" icon="ri:arrow-down-s-line" />
                    </ElButton>
                    <template #dropdown>
                        <ElDropdownMenu>
                            <ElDropdownItem command="excel">{{ t('roleManagement.actions.exportExcel') }}</ElDropdownItem>
                            <ElDropdownItem command="json">{{ t('roleManagement.actions.exportJson') }}</ElDropdownItem>
                        </ElDropdownMenu>
                    </template>
                </ElDropdown>
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
import { fetchCopyRole, fetchDeleteRole, fetchExportRoles, fetchGetRoleList } from '@/api/system-manage'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtButtonMore from '@/components/core/forms/art-button-more/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import RoleEditDialog from './modules/role-edit-dialog.vue'
import RolePermissionDialog from './modules/role-permission-dialog.vue'
import RoleApiDialog from './modules/role-api-dialog.vue'
import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'
import { formatDateTime } from '@/utils/date'
import * as XLSX from 'xlsx'
import FileSaver from 'file-saver'

defineOptions({ name: 'Role' })
type RoleListItem = Api.SystemManage.RoleListItem
const { t, locale } = useI18n()
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }

const dialogVisible = ref(false)
const permissionDialog = ref(false)
const apiPermissionDialog = ref(false)
const currentRoleData = ref<RoleListItem | undefined>(undefined)
const selectedRows = ref<RoleListItem[]>([])
const exporting = ref(false)

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
                queryValueType: 'string',
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
                queryField: 'DataScope',
                queryValueType: 'string',
                label: t('listFilter.role.dataScope'),
                minWidth: 150,
                formatter: (row) => {
                    const label = {
                        all: t('listFilter.option.allData'),
                        department: t('listFilter.option.departmentData'),
                        department_and_children: t('listFilter.option.departmentAndChildren'),
                        self: t('listFilter.option.ownData'),
                    }[row.dataScope]
                    return h(
                        ElTag,
                        {
                            size: 'small',
                            'data-query-field': 'DataScope',
                            'data-query-label': t('listFilter.role.dataScope'),
                            'data-query-value': row.dataScope,
                            'data-query-value-type': 'string',
                        },
                        () => label || row.dataScope,
                    )
                },
            },
            {
                prop: 'enabled',
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
            {
                prop: 'operation',
                queryField: false,
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
                                    key: 'copy',
                                    label: t('roleManagement.actions.copy'),
                                    icon: 'ri:file-copy-line',
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

type ExportFormat = 'excel' | 'json'

const exportFileName = (extension: ExportFormat): string => {
    const timestamp = new Date().toISOString().replace('T', '_').replace(/:/g, '-').replace(/\..+$/, '')
    return `roles_${timestamp}.${extension === 'excel' ? 'xlsx' : 'json'}`
}

const dataScopeLabel = (scope: RoleListItem['dataScope']): string =>
    ({
        all: t('listFilter.option.allData'),
        department: t('listFilter.option.departmentData'),
        department_and_children: t('listFilter.option.departmentAndChildren'),
        self: t('listFilter.option.ownData'),
    })[scope]

const downloadExcel = (rows: RoleListItem[]): void => {
    const exportRows = rows.map((row) => ({
        ID: row.roleId,
        [t('listFilter.role.name')]: row.roleName,
        [t('listFilter.role.code')]: row.roleCode,
        [t('listFilter.role.description')]: row.description,
        [t('listFilter.role.dataScope')]: dataScopeLabel(row.dataScope),
        [t('listFilter.common.status')]: t(row.enabled ? 'listFilter.option.enabled' : 'listFilter.option.disabled'),
        [t('roleManagement.fields.createdAt')]: formatDateTime(row.createTime, locale.value),
        [t('roleManagement.fields.updatedAt')]: formatDateTime(row.updateTime, locale.value),
    }))
    const worksheet = XLSX.utils.json_to_sheet(exportRows)
    worksheet['!cols'] = Object.keys(exportRows[0]).map((key) => ({
        wch: Math.min(Math.max(key.length + 2, ...exportRows.map((row) => String(row[key as keyof typeof row] ?? '').length + 2)), 50),
    }))
    const workbook = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(workbook, worksheet, t('roleManagement.export.sheetName'))
    const buffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array', compression: true })
    FileSaver.saveAs(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), exportFileName('excel'))
}

const downloadJson = (rows: RoleListItem[]): void => {
    const exportRows = rows.map((row) => ({
        id: row.roleId,
        name: row.roleName,
        code: row.roleCode,
        description: row.description,
        dataScope: row.dataScope,
        isEnabled: row.enabled,
        createdAt: formatDateTime(row.createTime, locale.value),
        updatedAt: formatDateTime(row.updateTime, locale.value),
    }))
    FileSaver.saveAs(new Blob([JSON.stringify(exportRows, null, 2)], { type: 'application/json;charset=utf-8' }), exportFileName('json'))
}

const handleExport = async (format: ExportFormat): Promise<void> => {
    if (exporting.value) return
    exporting.value = true
    try {
        let rows = selectedRows.value
        if (rows.length === 0) {
            const result = await fetchExportRoles({
                dynamicFilter: searchParams.dynamicFilter as DynamicFilter | undefined,
                sortField: searchParams.sortField as string | undefined,
                sortOrder: searchParams.sortOrder as 'asc' | 'desc' | undefined,
            })
            rows = result.records
            if (result.total > result.records.length) {
                ElMessage.warning(t('roleManagement.message.exportTruncated', { total: result.total, limit: result.limit }))
            }
        }
        if (rows.length === 0) {
            ElMessage.info(t('roleManagement.message.exportEmpty'))
            return
        }
        if (format === 'excel') downloadExcel(rows)
        else downloadJson(rows)
        ElMessage.success(t('roleManagement.message.exported', { count: rows.length }))
    } catch (error) {
        console.error(error)
        ElMessage.error(t('roleManagement.message.exportFailed'))
    } finally {
        exporting.value = false
    }
}

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
        case 'copy':
            void copyRole(row)
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
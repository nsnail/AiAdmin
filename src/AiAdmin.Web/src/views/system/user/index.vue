<!-- 用户管理页面 -->
<!-- art-full-height 自动计算出页面剩余高度 -->
<!-- art-table-card 一个符合系统样式的 class，同时自动撑满剩余高度 -->
<!-- 更多 useTable 使用示例请移步至 功能示例 下面的高级表格示例或者查看官方文档 -->
<!-- useTable 文档：https://www.artd.pro/docs/zh/guide/hooks/use-table.html -->
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
        resource="user">
        <template #header-left>
            <ElSpace wrap>
                <ElButton v-ripple @click="showDialog('add')">{{ t('userManagement.actions.add') }}</ElButton>
                <ElDropdown :disabled="exporting" @command="handleExport">
                    <ElButton :loading="exporting">
                        <ArtSvgIcon class="mr-1" icon="ri:download-2-line" />{{ t('userManagement.actions.exportData') }}
                        <ArtSvgIcon class="ml-1" icon="ri:arrow-down-s-line" />
                    </ElButton>
                    <template #dropdown>
                        <ElDropdownMenu>
                            <ElDropdownItem command="excel">{{ t('userManagement.actions.exportExcel') }}</ElDropdownItem>
                            <ElDropdownItem command="json">{{ t('userManagement.actions.exportJson') }}</ElDropdownItem>
                        </ElDropdownMenu>
                    </template>
                </ElDropdown>
            </ElSpace>
        </template>

        <UserDialog
            v-model:visible="dialogVisible"
            :saving="dialogSaving"
            :type="dialogType"
            :user-data="currentUserData"
            @submit="handleDialogSubmit" />
    </ArtTablePage>
</template>

<script lang="ts" setup>
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchCreateUser, fetchExportUsers, fetchGetUserList, fetchUpdateUser, fetchUploadUserAvatar } from '@/api/system-manage'
import UserDialog from './modules/user-dialog.vue'
import { ElMessage, ElTag } from 'element-plus'
import { DialogType } from '@/types'
import { useI18n } from 'vue-i18n'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import * as XLSX from 'xlsx'
import FileSaver from 'file-saver'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'User' })
const { t, locale } = useI18n()

type UserListItem = Api.SystemManage.UserListItem

// 弹窗相关
const dialogType = ref<DialogType>('add')
const dialogVisible = ref(false)
const currentUserData = ref<Partial<UserListItem>>({})
const dialogSaving = ref(false)
const selectedRows = ref<UserListItem[]>([])
const exporting = ref(false)

const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const roleName = (role: string): string => {
    const key = `userCenter.roleNames.${role}`
    return t(key) === key ? role : t(key)
}

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
    resetColumns,
} = useTable({
    // 核心配置
    core: {
        apiFn: fetchGetUserList,
        apiParams: {
            current: 1,
            size: 20,
            dynamicFilter: defaultFilter,
        },
        // 自定义分页字段映射，未设置时将使用全局配置 tableConfig.ts 中的 paginationKey
        // paginationKey: {
        //   current: 'pageNum',
        //   size: 'pageSize'
        // },
        columnsFactory: () => [
            {
                type: 'selection',
                width: 48,
                fixed: 'left',
            },
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 150,
                align: 'left',
                formatter: (row) => h(ArtListIdCell, { id: row.id, createdAt: row.createTime }),
            },
            {
                prop: 'userInfo',
                queryField: 'UserName',
                queryValueField: 'userName',
                label: t('userManagement.fields.userName'),
                width: 280,
                // visible: false, // 默认是否显示列
                formatter: (row) => {
                    return h('div', { class: 'user flex-c' }, [
                        h(ArtUserAvatar, { class: 'size-9.5 rounded-full', src: row.avatar, name: row.userName }),
                        h('div', { class: 'ml-2' }, [
                            h(
                                'p',
                                {
                                    class: 'user-name',
                                    'data-query-field': 'UserName',
                                    'data-query-label': t('userManagement.fields.userName'),
                                    'data-query-value': row.userName,
                                    'data-query-value-type': 'string',
                                },
                                row.userName,
                            ),
                            h(
                                'p',
                                {
                                    class: 'email text-gray-400',
                                    'data-query-field': 'Email',
                                    'data-query-label': t('userManagement.fields.email'),
                                    'data-query-value': row.userEmail,
                                    'data-query-value-type': 'string',
                                },
                                row.userEmail,
                            ),
                        ]),
                    ])
                },
            },
            {
                prop: 'userGender',
                queryField: 'Gender',
                queryValueType: 'number',
                align: 'right',
                label: t('userManagement.fields.gender'),
                width: 100,
                sortable: true,
                align: 'center',
                formatter: (row) =>
                    h(ElTag, { size: 'small', type: row.userGender === 2 ? 'danger' : 'primary' }, () =>
                        t(row.userGender === 2 ? 'userManagement.gender.female' : 'userManagement.gender.male'),
                    ),
            },
            {
                width: 150,
                prop: 'userPhone',
                queryField: 'Phone',
                label: t('userManagement.fields.phone'),
            },
            {
                prop: 'userRoles',
                queryField: 'RoleName',
                queryValueField: 'roleNames.0',
                queryValueType: 'string',
                label: t('userManagement.fields.roles'),
                minWidth: 160,
                formatter: (row) =>
                    h(
                        'div',
                        { class: 'flex flex-wrap gap-1' },
                        row.userRoles.map((role, index) =>
                            h(
                                ElTag,
                                {
                                    size: 'small',
                                    'data-query-field': 'RoleName',
                                    'data-query-label': t('userManagement.fields.roles'),
                                    'data-query-value': row.roleNames[index],
                                    'data-query-value-type': 'string',
                                },
                                () => roleName(role),
                            ),
                        ),
                    ),
            },
            {
                prop: 'departmentNames',
                queryField: 'DepartmentName',
                queryValueField: 'departmentNames.0',
                queryValueType: 'string',
                label: t('userManagement.fields.departments'),
                minWidth: 160,
                formatter: (row) =>
                    row.departmentNames.length
                        ? h(
                              'div',
                              { class: 'flex flex-wrap gap-1' },
                              row.departmentNames.map((department) =>
                                  h(
                                      ElTag,
                                      {
                                          size: 'small',
                                          type: 'info',
                                          'data-query-field': 'DepartmentName',
                                          'data-query-label': t('userManagement.fields.departments'),
                                          'data-query-value': department,
                                          'data-query-value-type': 'string',
                                      },
                                      () => department,
                                  ),
                              ),
                          )
                        : '-',
            },
            {
                prop: 'status',
                queryField: 'IsEnabled',
                queryValueField: 'isEnabled',
                queryValueType: 'boolean',
                label: t('listFilter.common.status'),
                width: 120,
                align: 'center',
                formatter: (row) =>
                    h(ArtEnabledSwitch, {
                        id: row.id,
                        resource: 'user',
                        modelValue: row.isEnabled,
                        'onUpdate:modelValue': () => {
                            void getData()
                        },
                    }),
            },
            {
                prop: 'operation',
                label: t('userManagement.fields.operation'),
                width: 70,
                fixed: 'right', // 固定列
                formatter: (row) =>
                    h('div', [
                        h(ArtButtonTable, {
                            type: 'edit',
                            onClick: () => showDialog('edit', row),
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
    return `users_${timestamp}.${extension === 'excel' ? 'xlsx' : 'json'}`
}

const downloadExcel = (rows: UserListItem[]): void => {
    const exportRows = rows.map((row) => ({
        ID: row.id,
        [t('userManagement.fields.userName')]: row.userName,
        [t('userManagement.fields.email')]: row.userEmail,
        [t('userManagement.fields.phone')]: row.userPhone,
        [t('userManagement.fields.gender')]: t(row.userGender === 2 ? 'userManagement.gender.female' : 'userManagement.gender.male'),
        [t('userManagement.fields.roles')]: row.userRoles.map(roleName).join(', '),
        [t('userManagement.fields.departments')]: row.departmentNames.join(', '),
        [t('userManagement.fields.status')]: t(row.isEnabled ? 'userManagement.status.enabled' : 'userManagement.status.disabled'),
        [t('userManagement.fields.createdAt')]: formatDateTime(row.createTime, locale.value),
        [t('userManagement.fields.updatedAt')]: formatDateTime(row.updateTime, locale.value),
    }))
    const worksheet = XLSX.utils.json_to_sheet(exportRows)
    worksheet['!cols'] = Object.keys(exportRows[0]).map((key) => ({
        wch: Math.min(Math.max(key.length + 2, ...exportRows.map((row) => String(row[key as keyof typeof row] ?? '').length + 2)), 50),
    }))
    const workbook = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(workbook, worksheet, t('userManagement.export.sheetName'))
    const buffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array', compression: true })
    FileSaver.saveAs(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), exportFileName('excel'))
}

const downloadJson = (rows: UserListItem[]): void => {
    const exportRows = rows.map((row) => ({
        id: row.id,
        userName: row.userName,
        email: row.userEmail,
        phone: row.userPhone,
        gender: row.userGender,
        roles: row.roleNames,
        departments: row.departmentNames,
        isEnabled: row.isEnabled,
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
            const result = await fetchExportUsers({
                dynamicFilter: searchParams.dynamicFilter as DynamicFilter | undefined,
                sortField: searchParams.sortField as string | undefined,
                sortOrder: searchParams.sortOrder as 'asc' | 'desc' | undefined,
            })
            rows = result.records
            if (result.total > result.records.length) {
                ElMessage.warning(t('userManagement.message.exportTruncated', { total: result.total, limit: result.limit }))
            }
        }
        if (rows.length === 0) {
            ElMessage.info(t('userManagement.message.exportEmpty'))
            return
        }
        if (format === 'excel') downloadExcel(rows)
        else downloadJson(rows)
        ElMessage.success(t('userManagement.message.exported', { count: rows.length }))
    } catch (error) {
        console.error(error)
        ElMessage.error(t('userManagement.message.exportFailed'))
    } finally {
        exporting.value = false
    }
}

/**
 * 搜索处理
 * @param params 参数
 */
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}

/**
 * 显示用户弹窗
 */
const showDialog = (type: DialogType, row?: UserListItem): void => {
    dialogType.value = type
    currentUserData.value = row || {}
    nextTick(() => {
        dialogVisible.value = true
    })
}

/**
 * 处理弹窗提交事件
 */
const handleDialogSubmit = async (form: Api.SystemManage.SaveUserParams) => {
    if (dialogSaving.value) return
    dialogSaving.value = true
    try {
        const { avatarFile, removeAvatar, ...userForm } = form
        let userId = currentUserData.value.id
        if (dialogType.value === 'add') {
            const createdUser = await fetchCreateUser(userForm)
            userId = createdUser.id
        } else {
            const { userName: _userName, password, ...editableFields } = userForm
            await fetchUpdateUser(userId!, {
                ...editableFields,
                ...(password ? { password } : {}),
                ...(removeAvatar ? { avatar: '' } : {}),
                removeAvatar: Boolean(removeAvatar),
            })
        }
        if (avatarFile && userId) await fetchUploadUserAvatar(userId, avatarFile)
        ElMessage.success(t(dialogType.value === 'add' ? 'userManagement.message.created' : 'userManagement.message.updated'))
        dialogVisible.value = false
        currentUserData.value = {}
        await getData()
    } catch (error) {
        console.error(error)
    } finally {
        dialogSaving.value = false
    }
}
</script>
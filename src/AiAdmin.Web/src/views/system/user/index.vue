<!-- 用户管理页面 -->
<!-- art-full-height 自动计算出页面剩余高度 -->
<!-- art-table-card 一个符合系统样式的 class，同时自动撑满剩余高度 -->
<!-- 更多 useTable 使用示例请移步至 功能示例 下面的高级表格示例或者查看官方文档 -->
<!-- useTable 文档：https://www.artd.pro/docs/zh/guide/hooks/use-table.html -->
<template>
    <div class="user-page art-full-height">
        <!-- 搜索栏 -->
        <UserSearch v-model="searchForm" @reset="resetSearchParams" @search="handleSearch"></UserSearch>

        <ElCard class="art-table-card">
            <!-- 表格头部 -->
            <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData">
                <template #left>
                    <ElSpace wrap>
                        <ElButton v-ripple @click="showDialog('add')">{{ t('userManagement.actions.add') }}</ElButton>
                    </ElSpace>
                </template>
            </ArtTableHeader>

            <!-- 表格 -->
            <ArtTable
                :columns="columns"
                :data="data"
                :loading="loading"
                :pagination="pagination"
                @cell-query="applyCellQuery"
                @pagination:current-change="handleCurrentChange"
                @pagination:size-change="handleSizeChange"
                @sort-change="handleSortChange">
            </ArtTable>

            <!-- 用户弹窗 -->
            <UserDialog
                v-model:visible="dialogVisible"
                :saving="dialogSaving"
                :type="dialogType"
                :user-data="currentUserData"
                @submit="handleDialogSubmit" />
        </ElCard>
    </div>
</template>

<script lang="ts" setup>
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchCreateUser, fetchGetUserList, fetchUpdateUser, fetchUploadUserAvatar } from '@/api/system-manage'
import UserSearch from './modules/user-search.vue'
import UserDialog from './modules/user-dialog.vue'
import { ElImage, ElMessage, ElTag } from 'element-plus'
import { DialogType } from '@/types'
import { useI18n } from 'vue-i18n'
import defaultAvatar from '@/assets/images/user/avatar.png'

defineOptions({ name: 'User' })
const { t, locale } = useI18n()

type UserListItem = Api.SystemManage.UserListItem

// 弹窗相关
const dialogType = ref<DialogType>('add')
const dialogVisible = ref(false)
const currentUserData = ref<Partial<UserListItem>>({})
const dialogSaving = ref(false)

// 搜索表单
const searchForm = ref<Api.SystemManage.UserSearchParams>({
    IsEnabled: true,
} as Api.SystemManage.UserSearchParams)
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
            ...searchForm.value,
        },
        // 自定义分页字段映射，未设置时将使用全局配置 tableConfig.ts 中的 paginationKey
        // paginationKey: {
        //   current: 'pageNum',
        //   size: 'pageSize'
        // },
        columnsFactory: () => [
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 150,
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
                        h(ElImage, {
                            class: 'size-9.5 rounded-full',
                            src: row.avatar || defaultAvatar,
                            fallback: defaultAvatar,
                            previewSrcList: row.avatar ? [row.avatar] : [],
                            onError: (event: Event) => {
                                const image = event.target as HTMLImageElement
                                image.src = defaultAvatar
                            },
                            // 图片预览是否插入至 body 元素上，用于解决表格内部图片预览样式异常
                            previewTeleported: true,
                        }),
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
                queryField: false,
                label: t('userManagement.fields.roles'),
                minWidth: 160,
                formatter: (row) =>
                    h(
                        'div',
                        { class: 'flex flex-wrap gap-1' },
                        row.userRoles.map((role) => h(ElTag, { size: 'small' }, () => roleName(role))),
                    ),
            },
            {
                prop: 'departmentNames',
                queryField: false,
                label: t('userManagement.fields.departments'),
                minWidth: 160,
                formatter: (row) =>
                    row.departmentNames.length
                        ? h(
                              'div',
                              { class: 'flex flex-wrap gap-1' },
                              row.departmentNames.map((department) => h(ElTag, { size: 'small', type: 'info' }, () => department)),
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

/**
 * 搜索处理
 * @param params 参数
 */
const handleSearch = (params: Api.SystemManage.UserSearchParams) => {
    replaceSearchParams(params)
    getData()
}

const applyCellQuery = async (condition: { field: string; operator: string; value: unknown }): Promise<void> => {
    const currentFilter = searchForm.value.dynamicFilter
    searchForm.value = {
        ...searchForm.value,
        dynamicFilter: currentFilter ? { logic: 'And', filters: [currentFilter, condition] } : condition,
    }
    replaceSearchParams(searchForm.value)
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

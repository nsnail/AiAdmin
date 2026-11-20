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
        :initial-filter="initialFilter"
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
        ref="userPage"
        resource="user">
        <template #header-left>
            <ElSpace wrap>
                <ElButton v-ripple @click="showDialog('add')">{{ t('userManagement.actions.add') }}</ElButton>
            </ElSpace>
        </template>

        <UserDialog
            v-model:visible="dialogVisible"
            :saving="dialogSaving"
            :type="dialogType"
            :user-data="currentUserData"
            @submit="handleDialogSubmit" />
        <UserDetailDialog v-model:visible="detailVisible" :user="detailUser" />
    </ArtTablePage>
</template>

<script lang="ts" setup>
import type { RowAction } from '@/types/component'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useRouteFilter } from '@/hooks/core/useRouteFilter'
import { useTable } from '@/hooks/core/useTable'
import { fetchCreateUser, fetchGetUser, fetchGetUserList, fetchImpersonateUser, fetchUpdateUser, fetchUploadUserAvatar } from '@/api/system-manage'
import UserDialog from './modules/user-dialog.vue'
import UserDetailDialog from './modules/user-detail-dialog.vue'
import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
import { useRouteAuth } from '@/hooks/core/useRouteAuth'
import { useUserStore } from '@/store/modules/user'
import { StorageConfig } from '@/utils/storage/storage-config'
import { DialogType } from '@/types'
import { useI18n } from 'vue-i18n'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { formatDateTime } from '@/utils/date'
import { getHashTagStyle } from '@/utils/tagColor'

defineOptions({ name: 'User' })
const { t, locale } = useI18n()

type UserListItem = Api.SystemManage.UserListItem

// 弹窗相关
const dialogType = ref<DialogType>('add')
const dialogVisible = ref(false)
const currentUserData = ref<Partial<UserListItem>>({})
const dialogSaving = ref(false)
const selectedRows = ref<UserListItem[]>([])
const detailVisible = ref(false)
const detailUser = ref<UserListItem>()

const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }
const userPage = ref<{ setFilter?: (filter: DynamicFilter | undefined) => void }>()

/**
 * 路由参数 userId 对应的筛选：从其他页面（如账号列表的“分配商家”）点击跳转而来时只按该用户查询。
 * 不叠加默认的“已启用”条件，否则已停用的用户会被筛掉而查不到
 */
const { initialFilter: routeFilter } = useRouteFilter({
    queryKey: 'userId',
    toFilter: (value) => (/^\d+$/.test(value) ? { field: 'Id', operator: 'Equal', value } : undefined),
    setFilter: (filter) => userPage.value?.setFilter?.(filter),
    apply: async (filter) => {
        replaceSearchParams({ dynamicFilter: filter })
        await getData()
    },
})
const initialFilter = routeFilter ?? defaultFilter
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
            dynamicFilter: initialFilter,
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
                width: 160,
                align: 'left',
                formatter: (row) => h(ArtListIdCell, { id: row.id, createdAt: row.createTime }),
            },
            {
                prop: 'userInfo',
                exportKey: 'userName',
                exportValue: (row) => row.userName,
                queryField: 'UserName',
                queryValueField: 'userName',
                label: t('userManagement.fields.userName'),
                width: 250,
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
                exportValue: (row) => t(row.userGender === 2 ? 'userManagement.gender.female' : 'userManagement.gender.male'),
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
                exportKey: 'roles',
                exportValue: (row) => row.userRoles.map(roleName).join(', '),
                queryField: 'UserRoles.Role.Name',
                queryValueField: 'roleNames.0',
                queryValueType: 'string',
                label: t('userManagement.fields.roles'),
                minWidth: 160,
                align: 'center',
                formatter: (row) =>
                    h(
                        'div',
                        { class: 'flex flex-wrap justify-center gap-1' },
                        row.userRoles.map((role, index) =>
                            h(
                                ElTag,
                                {
                                    size: 'small',
                                    style: getHashTagStyle(roleName(role)),
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
                exportKey: 'departments',
                queryField: 'UserDepartments.Department.Name',
                queryValueField: 'departmentNames.0',
                queryValueType: 'string',
                label: t('userManagement.fields.departments'),
                minWidth: 160,
                align: 'center',
                formatter: (row) =>
                    row.departmentNames.length
                        ? h(
                              'div',
                              { class: 'flex flex-wrap justify-center gap-1' },
                              row.departmentNames.map((department) =>
                                  h(
                                      ElTag,
                                      {
                                          size: 'small',
                                          style: getHashTagStyle(department),
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
                prop: 'isOnline',
                exportValue: (row) => t(row.isOnline ? 'userManagement.status.online' : 'userManagement.status.offline'),
                queryField: 'IsOnline',
                queryValueField: 'isOnline',
                queryValueType: 'boolean',
                label: t('userManagement.fields.online'),
                minWidth: 190,
                sortable: true,
                formatter: (row) =>
                    h('div', { class: 'leading-5' }, [
                        // 第一行：圆点加状态文字，作为在线状态字段的右键目标
                        h(
                            'div',
                            {
                                class: 'flex items-center gap-2',
                                'data-query-field': 'IsOnline',
                                'data-query-label': t('userManagement.fields.online'),
                                'data-query-value': String(row.isOnline),
                                'data-query-value-type': 'boolean',
                            },
                            [
                                h('span', {
                                    class: ['inline-block size-2 shrink-0 rounded-full', row.isOnline ? 'user-online-dot bg-success' : 'bg-g-300'],
                                }),
                                h('span', t(row.isOnline ? 'userManagement.status.online' : 'userManagement.status.offline')),
                            ],
                        ),
                        // 第二行：最后在线时间，作为日期字段的右键目标；从未在线过时没有时间，不挂筛选属性
                        h(
                            'div',
                            {
                                class: 'text-xs text-gray-400',
                                ...(row.lastOnlineAt
                                    ? {
                                          'data-query-field': 'LastOnlineAt',
                                          'data-query-label': t('userManagement.fields.lastOnline'),
                                          'data-query-value': row.lastOnlineAt,
                                          'data-query-value-type': 'date',
                                      }
                                    : {}),
                            },
                            row.lastOnlineAt ? formatDateTime(row.lastOnlineAt, locale.value) : t('userManagement.fields.neverOnline'),
                        ),
                    ]),
            },
            {
                prop: 'lastLoginAt',
                exportValue: (row) => (row.lastLoginAt ? formatDateTime(row.lastLoginAt, locale.value) : ''),
                queryField: 'LastLoginAt',
                queryValueField: 'lastLoginAt',
                queryValueType: 'date',
                label: t('userManagement.fields.lastLogin'),
                minWidth: 220,
                sortable: true,
                showOverflowTooltip: true,
                formatter: (row) =>
                    h('div', { class: 'leading-5' }, [
                        h(
                            'div',
                            {
                                'data-query-field': 'LastLoginAt',
                                'data-query-label': t('userManagement.fields.lastLogin'),
                                'data-query-value': row.lastLoginAt || '',
                                'data-query-value-type': 'date',
                            },
                            row.lastLoginAt ? formatDateTime(row.lastLoginAt, locale.value) : '-',
                        ),
                        h('div', { class: 'text-xs text-gray-400' }, [
                            h(
                                'span',
                                {
                                    'data-query-field': 'LastLoginIp',
                                    'data-query-label': t('userManagement.fields.lastLoginIp'),
                                    'data-query-value': row.lastLoginIp || '',
                                    'data-query-value-type': 'string',
                                },
                                row.lastLoginIp || '-',
                            ),
                            h('span', { class: 'mx-1' }, '|'),
                            h(
                                'span',
                                {
                                    'data-query-field': 'LastLoginRegion',
                                    'data-query-label': t('userManagement.fields.lastLoginRegion'),
                                    'data-query-value': row.lastLoginRegion || '',
                                    'data-query-value-type': 'string',
                                },
                                row.lastLoginRegion || t('userManagement.fields.unknownRegion'),
                            ),
                        ]),
                    ]),
            },
            {
                prop: 'status',
                exportKey: 'isEnabled',
                exportValue: (row) => t(row.isEnabled ? 'userManagement.status.enabled' : 'userManagement.status.disabled'),
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
        ],
    },
})

watch(locale, () => resetColumns?.())

const userStore = useUserStore()
const currentUserId = computed(() => String(userStore.info.userId ?? ''))

/**
 * 模拟登录：由后端签发目标用户的令牌，替换本地令牌后整页刷新
 * 整页刷新让路由、菜单和用户信息按新身份重新初始化，该流程不经过登录接口，因此不会产生登录日志
 * @param row 被模拟的用户
 */
const { hasAuth } = useRouteAuth()

/** 行右键菜单操作，模拟登录需要 impersonate 按钮权限，且不能模拟自己和已禁用的用户 */
const rowActions = (row: UserListItem): RowAction[] => {
    const actions: RowAction[] = [
        { key: 'view', label: t('userManagement.detail.view'), icon: 'ri:eye-line', onClick: () => openDetail(row) },
        { key: 'edit', label: t('common.rowAction.edit'), icon: 'ri:pencil-line', onClick: () => showDialog('edit', row) },
    ]
    if (row.id !== currentUserId.value && row.isEnabled && hasAuth('impersonate')) {
        actions.push({
            key: 'impersonate',
            shortcut: 'I',
            label: t('userManagement.impersonate.title'),
            icon: 'ri:user-shared-line',
            onClick: () => impersonate(row),
        })
    }
    return actions
}

const impersonate = async (row: UserListItem): Promise<void> => {
    try {
        await ElMessageBox.confirm(t('userManagement.impersonate.message', { name: row.userName }), t('userManagement.impersonate.title'), {
            confirmButtonText: t('userManagement.impersonate.confirm'),
            cancelButtonText: t('common.cancel'),
            type: 'warning',
        })
    } catch {
        return
    }
    const { token, refreshToken, signKey } = await fetchImpersonateUser(row.id)
    // 记录当前身份，刷新后检测到用户变化会清理上一个身份的工作台标签页
    localStorage.setItem(StorageConfig.LAST_USER_ID_KEY, currentUserId.value)
    userStore.setToken(token, refreshToken, signKey)
    userStore.setLoginStatus(true)
    // 清空用户信息，刷新后按新身份重新拉取并重建动态路由
    userStore.info = {}
    window.location.href = import.meta.env.BASE_URL
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
 * 打开用户详情抽屉
 * @param row 列表行
 */
const openDetail = (row: UserListItem): void => {
    detailUser.value = row
    detailVisible.value = true
}

/**
 * 显示用户弹窗
 */
const showDialog = async (type: DialogType, row?: UserListItem): Promise<void> => {
    dialogType.value = type
    if (type === 'edit' && row?.id) {
        try {
            currentUserData.value = await fetchGetUser(row.id)
        } catch (error) {
            console.error(error)
            return
        }
    } else {
        currentUserData.value = row || {}
    }
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

<style lang="scss">
/* 在线用户的小圆点：向外扩散的光晕，表示连接保持中；用户设置了“减少动态效果”时不播放 */
.user-online-dot {
    animation: user-online-pulse 1.8s ease-out infinite;
}

@keyframes user-online-pulse {
    0% {
        box-shadow: 0 0 0 0 color-mix(in srgb, var(--el-color-success) 55%, transparent);
    }

    70% {
        box-shadow: 0 0 0 6px color-mix(in srgb, var(--el-color-success) 0%, transparent);
    }

    100% {
        box-shadow: 0 0 0 0 color-mix(in srgb, var(--el-color-success) 0%, transparent);
    }
}

@media (prefers-reduced-motion: reduce) {
    .user-online-dot {
        animation: none;
    }
}
</style>
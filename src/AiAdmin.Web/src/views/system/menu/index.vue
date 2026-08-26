<!-- 菜单管理页面 -->
<template>
    <ArtTablePage
        v-model:column-checks="columnChecks"
        :columns="columns"
        :data="filteredTableData"
        :default-filter="defaultFilter"
        :loading="loading"
        :pagination="pagination"
        :row-key="'path'"
        :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
        @filter-change="handleFilterChange"
        @page-change="handleCurrentChange"
        @refresh="handleRefresh"
        @reset="handleReset"
        @size-change="handleSizeChange"
        @sort-change="handleSortChange"
        ref="tablePageRef"
        resource="menu">
        <template #header-left>
            <ElSpace>
                <ElButton v-auth="'add'" v-ripple @click="handleAddMenu">{{ t('menuManagement.actions.add') }}</ElButton>
                <ElButton v-ripple @click="toggleExpand">{{
                    isExpanded ? t('menuManagement.actions.collapse') : t('menuManagement.actions.expand')
                }}</ElButton>
            </ElSpace>
        </template>
        <MenuDialog
            v-model:visible="dialogVisible"
            :editData="editData"
            :lockType="lockMenuType"
            :menus="tableData"
            :parentName="authParent?.name || ''"
            :saving="dialogSaving"
            :type="dialogType"
            @submit="handleSubmit" />
    </ArtTablePage>
</template>

<script lang="ts" setup>
import { formatMenuTitle } from '@/utils/router'
import { formatDateTime } from '@/utils/date'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { useTableColumns } from '@/hooks/core/useTableColumns'
import type { AppRouteRecord } from '@/types/router'
import MenuDialog from './modules/menu-dialog.vue'
import { fetchCreateMenu, fetchDeleteMenu, fetchGetMenuList, fetchUpdateMenu } from '@/api/system-manage'
import { ElTag, ElMessage, ElMessageBox } from 'element-plus'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'Menus' })
const { t, locale } = useI18n()
const defaultFilter: DynamicFilter = { field: 'IsEnabled', operator: 'Equal', value: true }

const isExpanded = ref(false)
const tablePageRef = ref<{ tableRef?: { elTableRef?: { toggleRowExpansion: (row: AppRouteRecord, expanded: boolean) => void } } }>()

// 弹窗相关
const dialogVisible = ref(false)
const dialogSaving = ref(false)
const dialogType = ref<'menu' | 'button'>('menu')
const editData = ref<AppRouteRecord | any>(null)
const lockMenuType = ref(false)
const authParent = ref<AppRouteRecord | null>(null)

/**
 * 获取菜单类型标签颜色
 * @param row 菜单行数据
 * @returns 标签颜色类型
 */
const getMenuTypeTag = (row: AppRouteRecord): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
    if (row.meta?.isAuthButton) return 'danger'
    if (row.children?.some((child) => !child.meta?.isAuthButton)) return 'info'
    if (row.meta?.link && row.meta?.isIframe) return 'success'
    if (row.path) return 'primary'
    if (row.meta?.link) return 'warning'
    return 'info'
}

/**
 * 获取菜单类型文本
 * @param row 菜单行数据
 * @returns 菜单类型文本
 */
const getMenuTypeText = (row: AppRouteRecord): string => {
    if (row.meta?.isAuthButton) return '按钮'
    if (row.children?.some((child) => !child.meta?.isAuthButton)) return '目录'
    if (row.meta?.link && row.meta?.isIframe) return '内嵌'
    if (row.path) return '菜单'
    if (row.meta?.link) return '外链'
    return '未知'
}

// 表格列配置
const { columnChecks, columns } = useTableColumns(() => [
    {
        prop: 'meta.title',
        label: t('menuManagement.fields.name'),
        sortable: 'custom',
        queryField: 'Name',
        minWidth: 120,
        formatter: (row: AppRouteRecord) => formatMenuTitle(row.meta?.title),
    },
    {
        prop: 'type',
        label: t('menuManagement.fields.type'),
        sortable: 'custom',
        align: 'center',
        queryField: false,
        formatter: (row: AppRouteRecord) => {
            return h(ElTag, { type: getMenuTypeTag(row) }, () => getMenuTypeText(row))
        },
    },
    {
        prop: 'path',
        label: t('menuManagement.fields.path'),
        sortable: 'custom',
        queryField: 'Path',
        formatter: (row: AppRouteRecord) => {
            if (row.meta?.isAuthButton) return ''
            return row.meta?.link || row.path || ''
        },
    },
    {
        prop: 'component',
        label: t('menuManagement.fields.component'),
        queryField: 'Component',
        queryValueType: 'string',
        minWidth: 180,
        showOverflowTooltip: true,
    },
    {
        prop: 'meta.authList',
        label: t('menuManagement.fields.permissions'),
        sortable: 'custom',
        queryField: false,
        formatter: (row: AppRouteRecord) => {
            if (row.meta?.isAuthButton) {
                return row.meta?.authMark || ''
            }
            if (!row.meta?.authList?.length) return ''
            return `${row.meta.authList.length} ${t('menuManagement.fields.permissionCount')}`
        },
    },
    {
        prop: 'sort',
        label: t('menuManagement.fields.sort'),
        queryField: 'Sort',
        queryValueType: 'number',
        width: 90,
        align: 'right',
        sortable: 'custom',
    },
    {
        prop: 'updatedAt',
        label: t('menuManagement.fields.updatedAt'),
        queryField: 'UpdatedAt',
        queryValueType: 'date',
        width: 180,
        sortable: 'custom',
        formatter: (row: AppRouteRecord & { updatedAt?: string }) => formatDateTime(row.updatedAt, locale.value),
    },
    {
        prop: 'status',
        label: t('menuManagement.fields.status'),
        sortable: 'custom',
        queryField: 'IsEnabled',
        queryValueField: 'isEnabled',
        queryValueType: 'boolean',
        align: 'center',
        formatter: (row: AppRouteRecord) =>
            row.meta?.isAuthButton
                ? ''
                : h(ArtEnabledSwitch, {
                      id: row.id!,
                      resource: 'menu',
                      modelValue: row.isEnabled ?? true,
                      'onUpdate:modelValue': () => {
                          void getMenuList()
                      },
                  }),
    },
    {
        prop: 'operation',
        label: '操作',
        width: 180,
        align: 'right',
        formatter: (row: AppRouteRecord) => {
            const buttonStyle = { style: 'text-align: right' }

            if (row.meta?.isAuthButton) {
                return h('div', buttonStyle, [
                    h(ArtButtonTable, {
                        type: 'edit',
                        onClick: () => handleEditAuth(row),
                    }),
                    h(ArtButtonTable, {
                        type: 'delete',
                        onClick: () => handleDeleteAuth(row),
                    }),
                ])
            }

            return h('div', buttonStyle, [
                h(ArtButtonTable, {
                    type: 'add',
                    onClick: () => handleAddAuth(row),
                    title: '新增权限',
                }),
                h(ArtButtonTable, {
                    type: 'edit',
                    onClick: () => handleEditMenu(row),
                }),
                h(ArtButtonTable, {
                    type: 'delete',
                    onClick: () => handleDeleteMenu(row),
                }),
            ])
        },
    },
])

const {
    data: tableData,
    loading,
    pagination,
    getData: getMenuList,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData: handleRefresh,
} = useTable({
    core: {
        apiFn: (params: { dynamicFilter?: DynamicFilter; sortField?: string; sortOrder?: 'asc' | 'desc' }) =>
            fetchGetMenuList(params.dynamicFilter, params.sortField, params.sortOrder),
        apiParams: { current: 1, size: 1000, dynamicFilter: defaultFilter },
        columnsFactory: () => columns.value,
    },
})

const handleReset = (): void => resetSearchParams()

const handleFilterChange = async (dynamicFilter?: DynamicFilter): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getMenuList()
}

/**
 * 深度克隆对象
 * @param obj 要克隆的对象
 * @returns 克隆后的对象
 */
const deepClone = <T,>(obj: T): T => {
    if (obj === null || typeof obj !== 'object') return obj
    if (obj instanceof Date) return new Date(obj) as T
    if (Array.isArray(obj)) return obj.map((item) => deepClone(item)) as T

    const cloned = {} as T
    for (const key in obj) {
        if (Object.prototype.hasOwnProperty.call(obj, key)) {
            cloned[key] = deepClone(obj[key])
        }
    }
    return cloned
}

/**
 * 将权限列表转换为子节点
 * @param items 菜单项数组
 * @returns 转换后的菜单项数组
 */
const convertAuthListToChildren = (items: AppRouteRecord[]): AppRouteRecord[] => {
    return items.map((item) => {
        const clonedItem = deepClone(item)

        if (clonedItem.children?.length) {
            clonedItem.children = convertAuthListToChildren(clonedItem.children)
        }

        if (item.meta?.authList?.length) {
            const authChildren: AppRouteRecord[] = item.meta.authList.map((auth: { title: string; authMark: string }) => ({
                path: `${item.path}_auth_${auth.authMark}`,
                name: `${String(item.name)}_auth_${auth.authMark}`,
                meta: {
                    title: auth.title,
                    authMark: auth.authMark,
                    isAuthButton: true,
                    parentPath: item.path,
                },
            }))

            clonedItem.children = clonedItem.children?.length ? [...clonedItem.children, ...authChildren] : authChildren
        }

        return clonedItem
    })
}

/**
 * 搜索菜单
 * @param items 菜单项数组
 * @returns 搜索结果数组
 */
// 过滤后的表格数据
const filteredTableData = computed(() => convertAuthListToChildren(tableData.value as AppRouteRecord[]))

const findMenuByPath = (items: AppRouteRecord[], path: string): AppRouteRecord | null => {
    for (const item of items) {
        if (item.path === path) return item
        const found = findMenuByPath(item.children || [], path)
        if (found) return found
    }
    return null
}
const findMenuByName = (items: AppRouteRecord[], name: string): AppRouteRecord | null => {
    for (const item of items) {
        if (item.name === name) return item
        const found = findMenuByName(item.children || [], name)
        if (found) return found
    }
    return null
}

/**
 * 从当前菜单树中定位真实菜单节点
 * @param candidate 菜单行
 * @returns 带主键的菜单节点
 */
const resolveParentMenu = (candidate?: AppRouteRecord | null): AppRouteRecord | null => {
    if (!candidate) return null
    const items = tableData.value as AppRouteRecord[]
    if (candidate.id) {
        const byId = items.length
            ? (() => {
                  const walk = (nodes: AppRouteRecord[]): AppRouteRecord | null => {
                      for (const node of nodes) {
                          if (String(node.id) === String(candidate.id)) return node
                          const found = walk(node.children || [])
                          if (found) return found
                      }
                      return null
                  }
                  return walk(items)
              })()
            : null
        if (byId) return byId
    }
    return findMenuByPath(items, candidate.path || '') || findMenuByName(items, String(candidate.name || '')) || (candidate.id ? candidate : null)
}

/**
 * 添加菜单
 */
const handleAddMenu = (): void => {
    dialogType.value = 'menu'
    editData.value = null
    lockMenuType.value = false
    dialogVisible.value = true
}

/**
 * 添加权限按钮
 */
const handleAddAuth = (parent: AppRouteRecord): void => {
    dialogType.value = 'button'
    // 默认打开权限表单，但允许在菜单和按钮之间切换
    lockMenuType.value = false
    const resolvedParent = resolveParentMenu(parent)
    authParent.value = resolvedParent
    editData.value = null
    dialogVisible.value = true
}

/**
 * 编辑菜单
 * @param row 菜单行数据
 */
const handleEditMenu = (row: AppRouteRecord): void => {
    dialogType.value = 'menu'
    editData.value = row
    lockMenuType.value = true
    dialogVisible.value = true
}

/**
 * 编辑权限按钮
 * @param row 权限行数据
 */
const handleEditAuth = (row: AppRouteRecord): void => {
    dialogType.value = 'button'
    editData.value = {
        title: row.meta?.title,
        authMark: row.meta?.authMark,
    }
    authParent.value = resolveParentMenu({ path: row.meta?.parentPath } as AppRouteRecord)
    lockMenuType.value = true
    dialogVisible.value = true
}

/**
 * 菜单表单数据类型
 */
interface MenuFormData {
    name: string
    path: string
    component?: string
    parentName?: string
    icon?: string
    sort?: number
    [key: string]: any
}

/**
 * 提交表单数据
 * @param formData 表单数据
 */
const handleSubmit = async (formData: MenuFormData): Promise<void> => {
    if (dialogSaving.value) return
    dialogSaving.value = true
    try {
        if (formData.menuType === 'button') {
            const parent = authParent.value
            if (!parent?.id) {
                ElMessage.error('未找到父级菜单，无法保存权限')
                return
            }
            const authList = [...(parent.meta?.authList || [])] as Array<{ title: string; authMark: string }>
            const originalMark = editData.value?.authMark
            const duplicate = authList.some((item) => item.authMark === formData.authLabel && item.authMark !== originalMark)
            if (duplicate) {
                ElMessage.warning('权限标识已存在')
                return
            }
            const nextAuth = { title: formData.authName || '', authMark: formData.authLabel || '' }
            const nextAuthList = originalMark ? authList.map((item) => (item.authMark === originalMark ? nextAuth : item)) : [...authList, nextAuth]
            await fetchUpdateMenu(parent.id, {
                name: parent.name,
                path: parent.path || '',
                component: typeof parent.component === 'string' ? parent.component : '',
                parentName: (parent as AppRouteRecord & { parentName?: string }).parentName || '',
                sort: (parent as AppRouteRecord & { sort?: number }).sort || 0,
                meta: { ...parent.meta, authList: nextAuthList },
                isEnabled: parent.isEnabled ?? true,
            })
            await getMenuList()
            dialogVisible.value = false
            return
        }
        const meta = {
            title: formData.name,
            icon: formData.icon,
            keepAlive: formData.keepAlive,
            isHide: formData.isHide,
            isHideTab: formData.isHideTab,
            link: formData.link,
            isIframe: formData.isIframe,
            showBadge: formData.showBadge,
            showTextBadge: formData.showTextBadge,
            fixedTab: formData.fixedTab,
            activePath: formData.activePath,
            isFullPage: formData.isFullPage,
        }
        const payload = {
            name: formData.label || formData.name,
            path: formData.path,
            component: formData.component || '',
            parentName: formData.parentName || '',
            sort: formData.sort || 0,
            meta,
            isEnabled: formData.isEnable,
        }
        if (formData.id) await fetchUpdateMenu(formData.id, payload)
        else await fetchCreateMenu(payload)
        await getMenuList()
        dialogVisible.value = false
    } finally {
        dialogSaving.value = false
    }
}

/**
 * 删除菜单
 */
const handleDeleteMenu = async (row?: AppRouteRecord): Promise<void> => {
    try {
        await ElMessageBox.confirm('确定要删除该菜单吗？删除后无法恢复', '提示', {
            confirmButtonText: '确定',
            cancelButtonText: '取消',
            type: 'warning',
        })
        if (row?.id) await fetchDeleteMenu(row.id)
        getMenuList()
    } catch (error) {
        if (error !== 'cancel') {
            ElMessage.error('删除失败')
        }
    }
}

/**
 * 删除权限按钮
 */
const handleDeleteAuth = async (row: AppRouteRecord): Promise<void> => {
    try {
        await ElMessageBox.confirm('确定要删除该权限吗？删除后无法恢复', '提示', {
            confirmButtonText: '确定',
            cancelButtonText: '取消',
            type: 'warning',
        })
        const parent = resolveParentMenu({ path: row.meta?.parentPath } as AppRouteRecord)
        const authMark = row.meta?.authMark
        if (!parent?.id || !authMark) {
            ElMessage.error('未找到父级菜单或权限，无法删除')
            return
        }
        const authList = (parent.meta?.authList || []).filter((item: { authMark: string }) => item.authMark !== authMark)
        await fetchUpdateMenu(parent.id, {
            name: parent.name,
            path: parent.path || '',
            component: typeof parent.component === 'string' ? parent.component : '',
            parentName: (parent as AppRouteRecord & { parentName?: string }).parentName || '',
            sort: (parent as AppRouteRecord & { sort?: number }).sort || 0,
            meta: { ...parent.meta, authList },
            isEnabled: parent.isEnabled ?? true,
        })
        ElMessage.success('删除成功')
        await getMenuList()
    } catch (error) {
        if (error !== 'cancel') {
            ElMessage.error('删除失败')
        }
    }
}

/**
 * 切换展开/收起所有菜单
 */
const toggleExpand = (): void => {
    isExpanded.value = !isExpanded.value
    nextTick(() => {
        const table = tablePageRef.value?.tableRef?.elTableRef
        if (table && filteredTableData.value) {
            const processRows = (rows: AppRouteRecord[]) => {
                rows.forEach((row) => {
                    if (row.children?.length) {
                        table.toggleRowExpansion(row, isExpanded.value)
                        processRows(row.children)
                    }
                })
            }
            processRows(filteredTableData.value)
        }
    })
}
</script>
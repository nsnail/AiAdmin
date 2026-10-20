<template>
    <ElDrawer v-model="visible" @close="handleClose" align-center class="el-dialog-border" title="菜单权限" width="520px">
        <ElTabs v-model="activeTab">
            <ElTabPane label="基本信息" name="form">
                <ElScrollbar height="70vh">
                    <ElTree
                        v-loading="loading"
                        :data="processedMenuList"
                        :default-expand-all="isExpandAll"
                        :props="defaultProps"
                        @check="handleNodeCheck"
                        check-strictly
                        node-key="id"
                        ref="treeRef"
                        show-checkbox>
                        <template #default="{ data }">
                            <div style="display: flex; align-items: center">
                                <span v-if="data.isAuth">
                                    {{ data.label }}
                                </span>
                                <span v-else>{{ defaultProps.label(data) }}</span>
                            </div>
                        </template>
                    </ElTree>
                </ElScrollbar>
            </ElTabPane>
            <ElTabPane label="原始数据" name="raw-data"><ArtRawData :data="props.roleData" /></ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="toggleExpandAll">{{ isExpandAll ? '全部收起' : '全部展开' }}</ElButton>
            <ElButton @click="toggleSelectAll" style="margin-left: 8px">{{ isSelectAll ? '取消全选' : '全部选择' }}</ElButton>
            <ElButton @click="handleClose">取消</ElButton>
            <ElButton :loading="saving" @click="savePermission" type="primary">保存</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { fetchGetMenuList, fetchGetRoleMenuAuths, fetchGetRoleMenus, fetchSaveRoleMenus, type RoleMenuAuth } from '@/api/system-manage'
import { formatMenuTitle } from '@/utils/router'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'

type RoleListItem = Api.SystemManage.RoleListItem

interface Props {
    modelValue: boolean
    roleData?: RoleListItem
}

interface Emits {
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
}

const props = withDefaults(defineProps<Props>(), {
    modelValue: false,
    roleData: undefined,
})

const emit = defineEmits<Emits>()

const treeRef = ref()
const isExpandAll = ref(true)
const isSelectAll = ref(false)
const loading = ref(false)
const saving = ref(false)
const activeTab = ref('form')
const menuList = ref<MenuNode[]>([])

/**
 * 弹窗显示状态双向绑定
 */
const visible = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value),
})

/**
 * 菜单节点类型
 */
interface MenuNode {
    id?: string
    name?: string
    label?: string
    meta?: {
        title?: string
        isHide?: boolean
        authList?: Array<{
            authMark: string
            title: string
            checked?: boolean
        }>
    }
    children?: MenuNode[]
    [key: string]: any
}

/** 按钮节点 key 的分隔标记，格式为 菜单主键 + 分隔标记 + 权限标识 */
const AUTH_KEY_SEPARATOR = ':auth:'

/**
 * 生成按钮权限节点的唯一 key
 * @param menuId 菜单主键
 * @param authMark 权限标识
 * @returns 按钮节点 key
 */
const buildAuthKey = (menuId: string | number, authMark: string): string => `${menuId}${AUTH_KEY_SEPARATOR}${authMark}`

/**
 * 解析按钮节点 key
 * @param key 树节点 key
 * @returns 菜单主键和权限标识，不是按钮节点时为空
 */
const parseAuthKey = (key: string): RoleMenuAuth | undefined => {
    const index = key.indexOf(AUTH_KEY_SEPARATOR)
    return index > 0 ? { menuId: key.slice(0, index), authMark: key.slice(index + AUTH_KEY_SEPARATOR.length) } : undefined
}

const processedMenuList = computed(() => {
    const processNode = (node: MenuNode): MenuNode | null => {
        if (typeof node.name !== 'string') return null
        const children = node.children?.map(processNode).filter((item): item is MenuNode => !!item)

        // 菜单声明的按钮权限作为该菜单下的可勾选子节点，使角色可以只授权部分按钮
        const authChildren: MenuNode[] = (node.meta?.authList ?? []).map((auth) => ({
            id: buildAuthKey(node.id ?? '', auth.authMark),
            name: buildAuthKey(node.id ?? '', auth.authMark),
            label: auth.title,
            isAuth: true,
        }))
        const merged = [...(children ?? []), ...authChildren]
        return { ...node, children: merged.length ? merged : undefined }
    }
    return menuList.value.map(processNode).filter((item): item is MenuNode => !!item)
})

/**
 * 树形组件配置
 */
const defaultProps = {
    children: 'children',
    label: (data: any) => formatMenuTitle(data.meta?.title) || data.label || '',
}

/**
 * 监听弹窗打开，初始化权限数据
 */
watch(
    () => props.modelValue,
    async (newVal) => {
        if (newVal && props.roleData) {
            activeTab.value = 'form'
            loading.value = true
            try {
                menuList.value = (await fetchGetMenuList()) as unknown as MenuNode[]
                const menuNames = await fetchGetRoleMenus(props.roleData.roleId)
                const grantedAuths = await fetchGetRoleMenuAuths(props.roleData.roleId)
                await nextTick()
                // 严格勾选下菜单与按钮互相独立：角色已授权的菜单和按钮各自回显，没有授予任何按钮的菜单也保持勾选
                const allKeys = new Set(getAllNodeKeys(processedMenuList.value))
                const authKeys = grantedAuths.map((item) => buildAuthKey(item.menuId, item.authMark))
                treeRef.value?.setCheckedKeys(
                    [...flattenMenuItems(menuNames as unknown as MenuNode[]).map((item) => item.id), ...authKeys].filter(
                        (id): id is string => Boolean(id) && allKeys.has(String(id)),
                    ),
                )
                refreshSelectAllState()
            } finally {
                loading.value = false
            }
        }
    },
)

/**
 * 关闭弹窗并清空选中状态
 */
const handleClose = () => {
    visible.value = false
    treeRef.value?.setCheckedKeys([])
}

/**
 * 保存权限配置
 */
const savePermission = async () => {
    if (!props.roleData || !treeRef.value || saving.value) return
    saving.value = true
    try {
        // 严格勾选下勾选结果就是授权结果：菜单与按钮各自独立，按钮被勾选时其所属菜单已在 handleNodeCheck 中同步勾选
        const allKeys = (treeRef.value.getCheckedKeys() as string[]).filter(Boolean)

        // 按钮节点拆成按钮权限，其余为菜单
        const auths = allKeys.map(parseAuthKey).filter((item): item is RoleMenuAuth => !!item)
        const menuIds = allKeys.filter((key) => !parseAuthKey(key))
        await fetchSaveRoleMenus(props.roleData.roleId, menuIds, auths)
        ElMessage.success('权限保存成功，相关用户下次登录后生效')
        emit('success')
        handleClose()
    } finally {
        saving.value = false
    }
}

/**
 * 切换全部展开/收起状态
 */
const toggleExpandAll = () => {
    const tree = treeRef.value
    if (!tree) return

    const nodes = tree.store.nodesMap
    // 这里保留 any，因为 Element Plus 的内部节点类型较复杂
    Object.values(nodes).forEach((node: any) => {
        node.expanded = !isExpandAll.value
    })

    isExpandAll.value = !isExpandAll.value
}

/**
 * 切换全选/取消全选状态
 */
const toggleSelectAll = () => {
    const tree = treeRef.value
    if (!tree) return

    if (!isSelectAll.value) {
        tree.setCheckedKeys(getAllNodeKeys(processedMenuList.value))
    } else {
        tree.setCheckedKeys([])
    }

    isSelectAll.value = !isSelectAll.value
}

/**
 * 递归获取所有节点（目录、菜单、按钮）的 key
 * @param nodes 节点列表
 * @returns 所有节点的 key 数组
 */
const getAllNodeKeys = (nodes: MenuNode[]): string[] => {
    return nodes.flatMap((node) => [...(node.id !== undefined ? [String(node.id)] : []), ...(node.children ? getAllNodeKeys(node.children) : [])])
}

const flattenMenuItems = (nodes: MenuNode[]): MenuNode[] => {
    return nodes.flatMap((node, index) => {
        return [node, ...(node.children ? flattenMenuItems(node.children) : [])]
    })
}

/**
 * 在树数据中查找节点
 * @param nodes 节点列表
 * @param key 节点 key
 * @param parents 从根到当前节点的祖先链，递归内部使用
 * @returns 目标节点及其祖先链，找不到时为空
 */
const findNode = (nodes: MenuNode[], key: string, parents: MenuNode[] = []): { node: MenuNode; parents: MenuNode[] } | undefined => {
    for (const node of nodes) {
        if (String(node.id) === key) return { node, parents }
        const found = node.children ? findNode(node.children, key, [...parents, node]) : undefined
        if (found) return found
    }
    return undefined
}

/**
 * 获取节点下所有后代节点 key
 * @param node 节点
 * @param onlyMenus 为 true 时只收集菜单和目录，不收集按钮
 * @returns 后代节点 key 数组
 */
const getDescendantKeys = (node: MenuNode, onlyMenus: boolean): string[] => {
    return (node.children ?? []).flatMap((child) => (onlyMenus && child.isAuth ? [] : [String(child.id), ...getDescendantKeys(child, onlyMenus)]))
}

/**
 * 处理节点勾选，菜单与按钮各自独立，只按下面的规则联动：
 * - 勾选按钮：同步勾选其所属菜单及上级目录，没有菜单就谈不上按钮
 * - 勾选菜单或目录：同步勾选下级菜单，但不自动勾选按钮，避免悄悄授予按钮权限
 * - 取消菜单或目录：同步取消其下所有菜单和按钮
 * - 取消按钮：只取消该按钮，所属菜单保持勾选，即只授权菜单而不授权任何按钮
 * @param data 被点击的节点
 * @param state 树当前的勾选状态
 */
const handleNodeCheck = (data: MenuNode, state: { checkedKeys: Array<string | number> }) => {
    const tree = treeRef.value
    if (!tree) return

    const key = String(data.id)
    const checked = new Set(state.checkedKeys.map(String))
    const isChecked = checked.has(key)
    const target = findNode(processedMenuList.value, key)
    if (target) {
        if (isChecked) {
            target.parents.forEach((parent) => checked.add(String(parent.id)))
            if (!data.isAuth) getDescendantKeys(target.node, true).forEach((id) => checked.add(id))
        } else if (!data.isAuth) {
            getDescendantKeys(target.node, false).forEach((id) => checked.delete(id))
        }
    }
    tree.setCheckedKeys([...checked])
    refreshSelectAllState()
}

/**
 * 同步全选按钮状态：所有节点都已勾选时为全选
 */
const refreshSelectAllState = () => {
    const tree = treeRef.value
    if (!tree) return

    const allKeys = getAllNodeKeys(processedMenuList.value)
    const checked = new Set((tree.getCheckedKeys() as string[]).map(String))
    isSelectAll.value = allKeys.length > 0 && allKeys.every((key) => checked.has(key))
}
</script>
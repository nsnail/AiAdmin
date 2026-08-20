<template>
  <ElDialog
    v-model="visible"
    title="菜单权限"
    width="520px"
    align-center
    class="el-dialog-border"
    @close="handleClose"
  >
    <ElTabs v-model="activeTab">
      <ElTabPane label="编辑" name="form">
        <ElScrollbar height="70vh">
          <ElTree
            ref="treeRef"
            :data="processedMenuList"
            v-loading="loading"
            show-checkbox
            node-key="id"
            :default-expand-all="isExpandAll"
            :props="defaultProps"
            @check="handleTreeCheck"
          >
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
      <ElButton @click="toggleSelectAll" style="margin-left: 8px">{{
        isSelectAll ? '取消全选' : '全部选择'
      }}</ElButton>
      <ElButton type="primary" :loading="saving" @click="savePermission">保存</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchGetMenuList, fetchGetRoleMenus, fetchSaveRoleMenus } from '@/api/system-manage'
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
    roleData: undefined
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
    set: (value) => emit('update:modelValue', value)
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

  const processedMenuList = computed(() => {
    const processNode = (node: MenuNode): MenuNode | null => {
      if (node.meta?.isHide || typeof node.name !== 'string') return null
      const children = node.children?.map(processNode).filter((item): item is MenuNode => !!item)
      return { ...node, children }
    }
    return menuList.value.map(processNode).filter((item): item is MenuNode => !!item)
  })

  /**
   * 树形组件配置
   */
  const defaultProps = {
    children: 'children',
    label: (data: any) => formatMenuTitle(data.meta?.title) || data.label || ''
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
          await nextTick()
          treeRef.value?.setCheckedKeys(
            flattenMenuItems(menuNames as unknown as MenuNode[])
              .map((item) => item.id)
              .filter(Boolean)
          )
          handleTreeCheck()
        } finally {
          loading.value = false
        }
      }
    }
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
    if (!props.roleData || !treeRef.value) return
    saving.value = true
    try {
      const selectedKeys = new Set([
        ...(treeRef.value.getCheckedKeys() as string[]),
        ...(treeRef.value.getHalfCheckedKeys() as string[])
      ])
      const allMenus = flattenMenuItems(menuList.value)
      const selectedNames = new Set(
        allMenus.filter((item) => selectedKeys.has(String(item.id))).map((item) => item.name)
      )
      const hiddenIds = allMenus
        .filter((item) => item.meta?.isHide === true && selectedNames.has(item.parentName))
        .map((item) => item.id)
        .filter((id): id is string => Boolean(id))
      const menuIds = [...new Set([...selectedKeys].filter(Boolean).concat(hiddenIds))]
      await fetchSaveRoleMenus(props.roleData.roleId, menuIds)
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
      const allKeys = getLeafNodeKeys(processedMenuList.value)
      tree.setCheckedKeys(allKeys)
    } else {
      tree.setCheckedKeys([])
    }

    isSelectAll.value = !isSelectAll.value
  }

  /**
   * 递归获取所有节点的 key
   * @param nodes 节点列表
   * @returns 所有节点的 key 数组
   */
  const getLeafNodeKeys = (nodes: MenuNode[]): string[] => {
    return nodes.flatMap((node) => {
      if (node.children?.length) return getLeafNodeKeys(node.children)
      return node.id !== undefined ? [String(node.id)] : []
    })
  }

  const flattenMenuItems = (nodes: MenuNode[]): MenuNode[] => {
    return nodes.flatMap((node, index) => {
      return [node, ...(node.children ? flattenMenuItems(node.children) : [])]
    })
  }

  /**
   * 处理树节点选中状态变化
   * 同步更新全选按钮状态
   */
  const handleTreeCheck = () => {
    const tree = treeRef.value
    if (!tree) return

    const leafKeys = new Set(getLeafNodeKeys(processedMenuList.value))
    const checkedKeys = (tree.getCheckedKeys() as string[]).filter((key) => leafKeys.has(key))
    const allKeys = [...leafKeys]

    isSelectAll.value = checkedKeys.length === allKeys.length && allKeys.length > 0
  }
</script>

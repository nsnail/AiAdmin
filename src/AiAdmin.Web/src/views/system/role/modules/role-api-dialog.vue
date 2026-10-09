<template>
    <ElDrawer v-model="visible" @close="handleClose" align-center class="el-dialog-border" title="接口权限" width="min(920px, 92vw)">
        <ElTabs v-model="activeTab">
            <ElTabPane label="基本信息" name="form">
                <ElScrollbar v-loading="loading" height="65vh">
                    <ElTree
                        :data="treeData"
                        :default-expand-all="isExpandAll"
                        :props="{ children: 'children', label: 'label' }"
                        @check="handleTreeCheck"
                        node-key="id"
                        ref="treeRef"
                        show-checkbox />
                </ElScrollbar>
            </ElTabPane>
            <ElTabPane label="原始数据" name="raw-data"><ArtRawData :data="props.roleData" /></ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="toggleExpandAll">{{
                t(isExpandAll ? 'roleManagement.actions.collapseAll' : 'roleManagement.actions.expandAll')
            }}</ElButton>
            <ElButton @click="toggleSelectAll" style="margin-left: 8px">
                {{ t(isSelectAll ? 'roleManagement.actions.deselectAll' : 'roleManagement.actions.selectAll') }}
            </ElButton>
            <ElButton @click="handleClose">取消</ElButton>
            <ElButton :loading="saving" @click="savePermission" type="primary">保存</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { fetchGetApiEndpointList, fetchGetRoleApis, fetchSaveRoleApis } from '@/api/system-manage'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { translateServerMessage } from '@/utils/i18n/server-message'
import { useI18n } from 'vue-i18n'

interface Props {
    modelValue: boolean
    roleData?: Api.SystemManage.RoleListItem
}

interface TreeNode {
    id: string
    label: string
    children?: TreeNode[]
}

const props = withDefaults(defineProps<Props>(), {
    modelValue: false,
    roleData: undefined,
})
const { t } = useI18n()
const emit = defineEmits<{
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
}>()

const treeRef = ref()
const endpoints = ref<Api.SystemManage.ApiEndpointItem[]>([])
const isExpandAll = ref(true)
const isSelectAll = ref(false)
const loading = ref(false)
const saving = ref(false)
const activeTab = ref('form')
const visible = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value),
})

const treeData = computed<TreeNode[]>(() => {
    const groups = new Map<string, Api.SystemManage.ApiEndpointItem[]>()
    endpoints.value.forEach((item) => {
        const controllerName = item.controllerName || item.controller
        const group = groups.get(controllerName) || []
        group.push(item)
        groups.set(controllerName, group)
    })
    return [...groups.entries()].map(([controller, items]) => ({
        id: `controller:${controller}`,
        label: `${translateServerMessage(items[0]?.controllerName) || items[0]?.controllerName || controller} [${items[0]?.controller || controller}]`,
        children: items.map((item) => ({
            id: item.id,
            label: `${translateServerMessage(item.name) || item.name}  [${item.method} ${item.path}]`,
        })),
    }))
})

watch(
    () => props.modelValue,
    async (opened) => {
        if (!opened || !props.roleData) return
        activeTab.value = 'form'
        loading.value = true
        try {
            const [endpointList, selectedIds] = await Promise.all([fetchGetApiEndpointList(), fetchGetRoleApis(props.roleData.roleId)])
            endpoints.value = endpointList
            await nextTick()
            treeRef.value?.setCheckedKeys(selectedIds)
            handleTreeCheck()
        } finally {
            loading.value = false
        }
    },
)

const handleClose = () => {
    visible.value = false
    treeRef.value?.setCheckedKeys([])
    isSelectAll.value = false
}

/**
 * 切换接口树的全部展开或收起状态
 */
const toggleExpandAll = (): void => {
    const tree = treeRef.value
    if (!tree) return

    Object.values(tree.store.nodesMap).forEach((node: any) => {
        node.expanded = !isExpandAll.value
    })
    isExpandAll.value = !isExpandAll.value
}

/**
 * 切换接口权限的全选或取消全选状态
 */
const toggleSelectAll = (): void => {
    const tree = treeRef.value
    if (!tree) return

    tree.setCheckedKeys(isSelectAll.value ? [] : endpoints.value.map((item) => item.id))
    isSelectAll.value = !isSelectAll.value
}

/**
 * 根据接口叶子节点的选中状态同步全选按钮
 */
const handleTreeCheck = (): void => {
    const tree = treeRef.value
    if (!tree) return

    const checkedKeys = new Set(tree.getCheckedKeys(true) as string[])
    const endpointKeys = endpoints.value.map((item) => item.id)
    isSelectAll.value = endpointKeys.length > 0 && endpointKeys.every((id) => checkedKeys.has(id))
}

const savePermission = async () => {
    if (!props.roleData || !treeRef.value || saving.value) return
    saving.value = true
    try {
        const apiIds = (treeRef.value.getCheckedKeys(true) as string[]).filter((id) => !id.startsWith('controller:'))
        await fetchSaveRoleApis(props.roleData.roleId, apiIds)
        ElMessage.success('接口权限保存成功，缓存已刷新')
        emit('success')
        handleClose()
    } finally {
        saving.value = false
    }
}
</script>
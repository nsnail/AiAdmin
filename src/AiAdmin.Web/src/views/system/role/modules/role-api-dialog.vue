<template>
    <ElDialog v-model="visible" @close="handleClose" align-center class="el-dialog-border" title="接口权限" width="640px">
        <ElTabs v-model="activeTab">
            <ElTabPane label="基本信息" name="form">
                <ElScrollbar v-loading="loading" height="65vh">
                    <ElTree
                        :data="treeData"
                        :props="{ children: 'children', label: 'label' }"
                        default-expand-all
                        node-key="id"
                        ref="treeRef"
                        show-checkbox />
                </ElScrollbar>
            </ElTabPane>
            <ElTabPane label="原始数据" name="raw-data"><ArtRawData :data="props.roleData" /></ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="handleClose">取消</ElButton>
            <ElButton :loading="saving" @click="savePermission" type="primary">保存</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import { fetchGetApiEndpointList, fetchGetRoleApis, fetchSaveRoleApis } from '@/api/system-manage'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'

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
const emit = defineEmits<{
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
}>()

const treeRef = ref()
const endpoints = ref<Api.SystemManage.ApiEndpointItem[]>([])
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
        label: controller,
        children: items.map((item) => ({
            id: item.id,
            label: `${item.method} ${item.path}`,
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
        } finally {
            loading.value = false
        }
    },
)

const handleClose = () => {
    visible.value = false
    treeRef.value?.setCheckedKeys([])
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
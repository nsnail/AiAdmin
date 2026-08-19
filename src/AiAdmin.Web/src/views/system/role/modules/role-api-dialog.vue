<template>
  <ElDialog
    v-model="visible"
    title="接口权限"
    width="640px"
    align-center
    class="el-dialog-border"
    @close="handleClose"
  >
    <ElScrollbar height="65vh" v-loading="loading">
      <ElTree
        ref="treeRef"
        :data="treeData"
        show-checkbox
        node-key="id"
        default-expand-all
        :props="{ children: 'children', label: 'label' }"
      />
    </ElScrollbar>
    <template #footer>
      <ElButton @click="handleClose">取消</ElButton>
      <ElButton type="primary" :loading="saving" @click="savePermission">保存</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import {
    fetchGetApiEndpointList,
    fetchGetRoleApis,
    fetchSaveRoleApis
  } from '@/api/system-manage'

  interface Props {
    modelValue: boolean
    roleData?: Api.SystemManage.RoleListItem
  }

  interface TreeNode {
    id: string | number
    label: string
    children?: TreeNode[]
  }

  const props = withDefaults(defineProps<Props>(), {
    modelValue: false,
    roleData: undefined
  })
  const emit = defineEmits<{
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
  }>()

  const treeRef = ref()
  const endpoints = ref<Api.SystemManage.ApiEndpointItem[]>([])
  const loading = ref(false)
  const saving = ref(false)
  const visible = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value)
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
        label: `${item.method} ${item.path}`
      }))
    }))
  })

  watch(
    () => props.modelValue,
    async (opened) => {
      if (!opened || !props.roleData) return
      loading.value = true
      try {
        const [endpointList, selectedIds] = await Promise.all([
          fetchGetApiEndpointList(),
          fetchGetRoleApis(props.roleData.roleId)
        ])
        endpoints.value = endpointList
        await nextTick()
        treeRef.value?.setCheckedKeys(selectedIds)
      } finally {
        loading.value = false
      }
    }
  )

  const handleClose = () => {
    visible.value = false
    treeRef.value?.setCheckedKeys([])
  }

  const savePermission = async () => {
    if (!props.roleData || !treeRef.value) return
    saving.value = true
    try {
      const apiIds = (treeRef.value.getCheckedKeys(true) as Array<string | number>)
        .map(Number)
        .filter(Number.isFinite)
      await fetchSaveRoleApis(props.roleData.roleId, apiIds)
      ElMessage.success('接口权限保存成功，缓存已刷新')
      emit('success')
      handleClose()
    } finally {
      saving.value = false
    }
  }
</script>

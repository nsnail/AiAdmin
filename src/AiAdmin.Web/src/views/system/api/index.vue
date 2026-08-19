<template>
  <div class="art-full-height">
    <ElCard class="art-table-card">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
        <ElInput
          v-model="keyword"
          clearable
          placeholder="搜索接口名称、方法或路径"
          class="w-full sm:w-80"
        >
          <template #prefix><ArtSvgIcon icon="ri:search-line" /></template>
        </ElInput>
        <ElButton type="primary" :loading="syncing" @click="syncEndpoints">
          <ArtSvgIcon icon="ri:refresh-line" class="mr-1" />
          同步接口
        </ElButton>
      </div>

      <ElTable
        v-loading="loading"
        :data="groupedEndpoints"
        row-key="key"
        default-expand-all
        :tree-props="{ children: 'children' }"
        height="calc(100vh - 220px)"
      >
        <ElTableColumn prop="name" label="接口名称" min-width="220" show-overflow-tooltip>
          <template #default="{ row }">
            <div class="flex items-center gap-2">
              <ArtSvgIcon v-if="row.isGroup" icon="ri:code-box-line" class="text-g-500" />
              <span :class="{ 'font-medium': row.isGroup }">{{ row.name }}</span>
              <ElTag v-if="row.isGroup" type="info" effect="plain" size="small">
                {{ row.children.length }} 个接口
              </ElTag>
            </div>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="method" label="方法" width="100">
          <template #default="{ row }">
            <ElTag v-if="!row.isGroup" :type="methodTagType(row.method)" effect="plain">
              {{ row.method }}
            </ElTag>
          </template>
        </ElTableColumn>
        <ElTableColumn prop="path" label="路径" min-width="280" show-overflow-tooltip />
        <ElTableColumn label="允许匿名访问" width="140">
          <template #default="{ row }">
            <ElSwitch
              v-if="!row.isGroup"
              v-model="row.allowAnonymous"
              @change="updateAnonymous(row)"
            />
          </template>
        </ElTableColumn>
        <ElTableColumn prop="action" label="操作" min-width="140" />
      </ElTable>
    </ElCard>
  </div>
</template>

<script setup lang="ts">
    import {
      fetchGetApiEndpointList,
      fetchSyncApiEndpoints,
      fetchUpdateApiAnonymous
    } from '@/api/system-manage'

  defineOptions({ name: 'ApiManagement' })

  type ApiEndpointItem = Api.SystemManage.ApiEndpointItem

  interface ApiTableRow extends Partial<ApiEndpointItem> {
    key: string
    name: string
    isGroup: boolean
    children?: ApiTableRow[]
  }

  const endpoints = ref<ApiEndpointItem[]>([])
  const keyword = ref('')
  const loading = ref(false)
  const syncing = ref(false)

  const groupedEndpoints = computed<ApiTableRow[]>(() => {
    const value = keyword.value.trim().toLowerCase()
    const groups = new Map<string, ApiEndpointItem[]>()
    endpoints.value.forEach((item) => {
      const controllerName = item.controllerName || item.controller
      const items = groups.get(controllerName) || []
      items.push(item)
      groups.set(controllerName, items)
    })

    return [...groups.entries()].flatMap(([controller, items]) => {
      const controllerMatched = controller.toLowerCase().includes(value)
      const matchedItems = value && !controllerMatched
        ? items.filter((item) =>
            [item.name, item.method, item.path, item.action].some((field) =>
              field.toLowerCase().includes(value)
            )
          )
        : items
      if (matchedItems.length === 0) return []

      return [{
        key: `controller:${controller}`,
        name: controller,
        isGroup: true,
        children: matchedItems.map((item) => ({
          ...item,
          key: `api:${item.id}`,
          isGroup: false
        }))
      }]
    })
  })

  const loadEndpoints = async () => {
    loading.value = true
    try {
      endpoints.value = await fetchGetApiEndpointList()
    } finally {
      loading.value = false
    }
  }

  const syncEndpoints = async () => {
    syncing.value = true
    try {
      const result = await fetchSyncApiEndpoints()
      ElMessage.success(
        `同步完成：新增 ${result.added}，更新 ${result.updated}，删除 ${result.deleted}`
      )
      await loadEndpoints()
    } finally {
      syncing.value = false
    }
  }

  const updateAnonymous = async (row: ApiTableRow) => {
    if (!row.id) return
    try {
      await fetchUpdateApiAnonymous(row.id, Boolean(row.allowAnonymous))
      ElMessage.success('匿名访问设置已更新')
    } catch {
      row.allowAnonymous = !row.allowAnonymous
    }
  }

  const methodTagType = (method: string) => {
    const types: Record<string, 'success' | 'warning' | 'danger' | 'info' | 'primary'> = {
      GET: 'success',
      POST: 'primary',
      PUT: 'warning',
      PATCH: 'warning',
      DELETE: 'danger'
    }
    return types[method] || 'info'
  }

  onMounted(loadEndpoints)
</script>

<template>
    <div class="art-full-height">
        <div class="mb-4 grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <ElCard shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.total')" :value="statistics.total" />
            </ElCard>
            <ElCard shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.controllers')" :value="statistics.controllers" />
            </ElCard>
            <ElCard shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.anonymous')" :value="statistics.anonymous" />
            </ElCard>
            <ElCard shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.methods')" :value="statistics.methods" />
            </ElCard>
        </div>
        <ElCard class="art-table-card">
            <div class="mb-4 flex flex-wrap items-center gap-3">
                <ElInput v-model="keyword" class="min-w-0 basis-full flex-1 sm:basis-auto" clearable placeholder="搜索接口名称、方法或路径">
                    <template #prefix><ArtSvgIcon icon="ri:search-line" /></template>
                </ElInput>
                <ElButton @click="advancedQueryVisible = true" class="w-24 shrink-0 justify-center">高级查询</ElButton>
                <ElButton :loading="syncing" @click="syncEndpoints" class="w-28 shrink-0 justify-center" type="primary">
                    <ArtSvgIcon class="mr-1" icon="ri:refresh-line" />
                    同步接口
                </ElButton>
            </div>

            <ArtTable
                :data="groupedEndpoints"
                :loading="loading"
                :tree-props="{ children: 'children' }"
                @sort-change="handleSortChange"
                default-expand-all
                height="calc(100vh - 220px)"
                row-key="key">
                <ElTableColumn label="接口名称" min-width="220" prop="name" show-overflow-tooltip sortable="custom">
                    <template #default="{ row }">
                        <span class="inline-flex items-center gap-2 whitespace-nowrap">
                            <ArtSvgIcon v-if="row.isGroup" class="text-g-500" icon="ri:code-box-line" />
                            <span :class="{ 'font-medium': row.isGroup }">{{ row.name }}</span>
                            <ElTag v-if="row.isGroup" effect="plain" size="small" type="info"> {{ row.children.length }} 个接口 </ElTag>
                        </span>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="方法" prop="method" sortable="custom" width="100">
                    <template #default="{ row }">
                        <ElTag v-if="!row.isGroup" :type="methodTagType(row.method)" effect="plain">
                            {{ row.method }}
                        </ElTag>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="路径" min-width="280" prop="path" show-overflow-tooltip sortable="custom" />
                <ElTableColumn label="允许匿名访问" prop="allowAnonymous" sortable="custom" width="140">
                    <template #default="{ row }">
                        <ElTag v-if="!row.isGroup" :type="row.allowAnonymous ? 'success' : 'info'" effect="plain">
                            {{ row.allowAnonymous ? '是' : '否' }}
                        </ElTag>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="操作" min-width="140" prop="action" sortable="custom" />
            </ArtTable>
        </ElCard>
        <ArtDynamicQueryDrawer v-model:visible="advancedQueryVisible" :fields="advancedQueryFields" @apply="applyAdvancedQuery" />
    </div>
</template>

<script lang="ts" setup>
import ArtDynamicQueryDrawer from '@/components/core/forms/art-dynamic-query-drawer/index.vue'
import { fetchGetApiEndpointList, fetchGetListFilterFields, fetchSyncApiEndpoints, type ListFilterField } from '@/api/system-manage'
import { translateServerMessage } from '@/utils/i18n/server-message'
import type { DynamicFilter, DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'ApiManagement' })
const { t } = useI18n()

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
const advancedQueryVisible = ref(false)
const activeDynamicFilter = ref<DynamicFilter>()
const sortField = ref<string>()
const sortOrder = ref<'asc' | 'desc'>()
const filterFields = ref<ListFilterField[]>([])
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({
        field: field.field,
        label: t(field.label),
        type: field.valueType,
    })),
)
const groupedEndpoints = computed<ApiTableRow[]>(() => {
    const value = keyword.value.trim().toLowerCase()
    const groups = new Map<string, ApiEndpointItem[]>()
    endpoints.value.forEach((item) => {
        const controllerName = translateServerMessage(item.controllerName || item.controller) || item.controller
        const items = groups.get(controllerName) || []
        items.push(item)
        groups.set(controllerName, items)
    })

    return [...groups.entries()].flatMap(([controller, items]) => {
        const controllerMatched = controller.toLowerCase().includes(value)
        const matchedItems =
            value && !controllerMatched
                ? items.filter((item) =>
                      [translateServerMessage(item.name) || item.name, item.method, item.path, item.action].some((field) =>
                          field.toLowerCase().includes(value),
                      ),
                  )
                : items
        if (matchedItems.length === 0) return []

        return [
            {
                key: `controller:${controller}`,
                name: controller,
                isGroup: true,
                children: matchedItems.map((item) => ({
                    ...item,
                    name: translateServerMessage(item.name) || item.name,
                    key: `api:${item.id}`,
                    isGroup: false,
                })),
            },
        ]
    })
})
const visibleEndpoints = computed(() => groupedEndpoints.value.flatMap((group) => group.children ?? []))
const statistics = computed(() => ({
    total: visibleEndpoints.value.length,
    controllers: new Set(visibleEndpoints.value.map((item) => item.controller || item.controllerName)).size,
    anonymous: visibleEndpoints.value.filter((item) => item.allowAnonymous).length,
    methods: new Set(visibleEndpoints.value.map((item) => item.method)).size,
}))

const loadEndpoints = async () => {
    loading.value = true
    try {
        endpoints.value = await fetchGetApiEndpointList(activeDynamicFilter.value, sortField.value, sortOrder.value)
    } finally {
        loading.value = false
    }
}

const syncEndpoints = async () => {
    if (syncing.value) return
    syncing.value = true
    try {
        const result = await fetchSyncApiEndpoints()
        ElMessage.success(`同步完成：新增 ${result.added}，更新 ${result.updated}，删除 ${result.deleted}`)
        await loadEndpoints()
    } finally {
        syncing.value = false
    }
}

const applyAdvancedQuery = async (dynamicFilter: DynamicFilter | undefined) => {
    activeDynamicFilter.value = dynamicFilter
    loading.value = true
    try {
        endpoints.value = await fetchGetApiEndpointList(dynamicFilter, sortField.value, sortOrder.value)
    } finally {
        loading.value = false
    }
}

const handleSortChange = async ({ prop, order }: { prop: string; order: 'ascending' | 'descending' | null }) => {
    sortField.value = order ? prop : undefined
    sortOrder.value = order ? (order === 'descending' ? 'desc' : 'asc') : undefined
    await loadEndpoints()
}

const methodTagType = (method: string) => {
    const types: Record<string, 'success' | 'warning' | 'danger' | 'info' | 'primary'> = {
        GET: 'success',
        POST: 'primary',
        PUT: 'warning',
        PATCH: 'warning',
        DELETE: 'danger',
    }
    return types[method] || 'info'
}

onMounted(async () => {
    await Promise.all([
        loadEndpoints(),
        fetchGetListFilterFields('api-endpoint').then((fields) => {
            filterFields.value = fields
        }),
    ])
})
</script>
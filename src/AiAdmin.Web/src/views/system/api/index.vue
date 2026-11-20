<template>
    <div class="art-full-height">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <ElCard class="art-card-sm" shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.total')" :value="statistics.total" />
            </ElCard>
            <ElCard class="art-card-sm" shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.controllers')" :value="statistics.controllers" />
            </ElCard>
            <ElCard class="art-card-sm" shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.anonymous')" :value="statistics.anonymous" />
            </ElCard>
            <ElCard class="art-card-sm" shadow="never">
                <ElStatistic :title="t('apiManagement.statistics.methods')" :value="statistics.methods" />
            </ElCard>
        </div>
        <ElCard class="art-table-card">
            <div class="mb-4 flex flex-wrap items-center gap-3">
                <ElInput v-model="keyword" class="min-w-0 basis-full flex-1 sm:basis-auto" clearable placeholder="搜索接口名称、方法或路径">
                    <template #prefix><ArtSvgIcon icon="ri:search-line" /></template>
                </ElInput>
                <ElButton @click="advancedQueryVisible = true" class="w-24 shrink-0 justify-center">高级查询</ElButton>
                <ElButton
                    :loading="loading"
                    :type="anonymousOnly ? 'primary' : 'default'"
                    @click="toggleAnonymousOnly"
                    class="shrink-0 justify-center">
                    <ArtSvgIcon class="mr-1" icon="ri:user-unfollow-line" />
                    {{ t('apiManagement.anonymousOnly') }}
                </ElButton>
                <ElButton :loading="syncing" @click="syncEndpoints" class="w-28 shrink-0 justify-center" type="primary">
                    <ArtSvgIcon class="mr-1" icon="ri:refresh-line" />
                    同步接口
                </ElButton>
            </div>

            <ArtTable
                :data="groupedEndpoints"
                :loading="loading"
                :row-actions="rowActions"
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
                            <ElTag v-if="row.isGroup" effect="light" size="small" type="info"> {{ row.children.length }} 个接口 </ElTag>
                        </span>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="方法" prop="method" sortable="custom" width="100">
                    <template #default="{ row }">
                        <ElTag v-if="!row.isGroup" :type="methodTagType(row.method)" effect="light" size="small">
                            {{ row.method }}
                        </ElTag>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="路径" min-width="280" prop="path" show-overflow-tooltip sortable="custom" />
                <ElTableColumn label="允许匿名访问" prop="allowAnonymous" sortable="custom" width="140">
                    <template #default="{ row }">
                        <ElTag v-if="!row.isGroup" :type="row.allowAnonymous ? 'success' : 'info'" effect="light" size="small">
                            {{ row.allowAnonymous ? '是' : '否' }}
                        </ElTag>
                    </template>
                </ElTableColumn>
                <ElTableColumn label="操作" min-width="140" prop="action" sortable="custom" />
            </ArtTable>
        </ElCard>
        <ArtDynamicQueryDrawer v-model:visible="advancedQueryVisible" :fields="advancedQueryFields" @apply="applyAdvancedQuery" />
        <RateLimitDialog v-model:visible="rateLimitVisible" :preset-api="presetApi" :saving="ruleSaving" @submit="saveRateLimit" />
        <CircuitBreakerDialog v-model:visible="circuitBreakerVisible" :preset-api="presetApi" :saving="ruleSaving" @submit="saveCircuitBreaker" />
    </div>
</template>

<script lang="ts" setup>
import ArtDynamicQueryDrawer from '@/components/core/forms/art-dynamic-query-drawer/index.vue'
import {
    fetchCreateCircuitBreakerRule,
    fetchCreateRateLimitRule,
    fetchGetApiEndpointList,
    fetchGetListFilterFields,
    fetchSyncApiEndpoints,
    type ListFilterField,
    type SaveCircuitBreakerRule,
    type SaveRateLimitRule,
} from '@/api/system-manage'
import type { RowAction } from '@/types/component'
import { useUserStore } from '@/store/modules/user'
import CircuitBreakerDialog from '../circuit-breaker/modules/circuit-breaker-dialog.vue'
import RateLimitDialog from '../rate-limit/modules/rate-limit-dialog.vue'
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
/** 是否只看匿名接口，与高级查询条件以 And 合并后一起发给后端 */
const anonymousOnly = ref(false)
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

/** 合并高级查询条件与匿名接口筛选，得到实际请求的动态筛选条件 */
const effectiveFilter = computed<DynamicFilter | undefined>(() => {
    if (!anonymousOnly.value) return activeDynamicFilter.value
    const anonymousFilter: DynamicFilter = { field: 'AllowAnonymous', operator: 'Equal', value: true }
    return activeDynamicFilter.value ? { logic: 'And', filters: [activeDynamicFilter.value, anonymousFilter] } : anonymousFilter
})

const loadEndpoints = async () => {
    loading.value = true
    try {
        endpoints.value = await fetchGetApiEndpointList(effectiveFilter.value, sortField.value, sortOrder.value)
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
    await loadEndpoints()
}

/** 切换只看匿名接口并重新查询 */
const toggleAnonymousOnly = async () => {
    anonymousOnly.value = !anonymousOnly.value
    await loadEndpoints()
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

/** 从右键菜单添加限流、熔断规则时使用的弹窗状态和预选接口 */
const rateLimitVisible = ref(false)
const circuitBreakerVisible = ref(false)
const ruleSaving = ref(false)
const presetApi = ref<{ id: string; method: string; path: string }>()

/**
 * 打开添加规则的弹窗，并预选当前右键的接口
 * @param row 被右键的接口行
 * @param kind 规则类型：限流或熔断
 */
const openRuleDialog = (row: ApiTableRow, kind: 'rateLimit' | 'circuitBreaker'): void => {
    presetApi.value = { id: String(row.id), method: row.method ?? '', path: row.path ?? '' }
    if (kind === 'rateLimit') rateLimitVisible.value = true
    else circuitBreakerVisible.value = true
}

/** 保存限流规则：成功后关闭弹窗并提示，规则在“接口限流”页管理 */
const saveRateLimit = async (form: SaveRateLimitRule): Promise<void> => {
    ruleSaving.value = true
    try {
        await fetchCreateRateLimitRule(form)
        rateLimitVisible.value = false
    } finally {
        ruleSaving.value = false
    }
}

/** 保存熔断规则：成功后关闭弹窗并提示，规则在“接口熔断”页管理 */
const saveCircuitBreaker = async (form: SaveCircuitBreakerRule): Promise<void> => {
    ruleSaving.value = true
    try {
        await fetchCreateCircuitBreakerRule(form)
        circuitBreakerVisible.value = false
    } finally {
        ruleSaving.value = false
    }
}

/** 限流和熔断规则只有超级管理员能管理，其他用户没有这两个菜单的权限，右键里也不展示，避免点了才被后端拒绝 */
const userStore = useUserStore()
const canManageRules = computed(() => userStore.getUserInfo.roles?.includes('R_SUPER') === true)

/** 行右键菜单：控制器分组行不是接口，没有菜单；接口行可以直接添加限流或熔断规则 */
const rowActions = (row: ApiTableRow): RowAction[] =>
    row.isGroup || !canManageRules.value
        ? []
        : [
              {
                  key: 'add-rate-limit',
                  label: t('apiManagement.actions.addRateLimit'),
                  icon: 'ri:speed-up-line',
                  shortcut: 'L',
                  onClick: () => openRuleDialog(row, 'rateLimit'),
              },
              {
                  key: 'add-circuit-breaker',
                  label: t('apiManagement.actions.addCircuitBreaker'),
                  icon: 'ri:flashlight-line',
                  shortcut: 'B',
                  onClick: () => openRuleDialog(row, 'circuitBreaker'),
              },
          ]

onMounted(async () => {
    await Promise.all([
        loadEndpoints(),
        fetchGetListFilterFields('api-endpoint').then((fields) => {
            filterFields.value = fields
        }),
    ])
})
</script>
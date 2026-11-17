<template>
    <div class="cache-page art-full-height">
        <ElCard class="server-card art-card-sm">
            <template #header
                ><div class="flex items-center justify-between">
                    <span class="font-medium">{{ t('redisCache.server.title') }}</span
                    ><ElButton :title="t('redisCache.actions.refresh')" @click="loadAll" circle text
                        ><ArtSvgIcon icon="ri:refresh-line"
                    /></ElButton></div
            ></template>
            <ElDescriptions v-loading="serverLoading" :column="4" border>
                <ElDescriptionsItem :label="t('redisCache.server.endpoint')">{{ serverInfo?.endpoint || '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.version')">{{ serverInfo?.version || '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.mode')">{{ serverInfo?.mode || '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.clients')">{{ serverInfo?.connectedClients ?? '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.usedMemory')">{{ serverInfo?.usedMemory || '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.maxMemory')">{{ serverInfo?.maxMemory || '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.databaseSize')">{{ serverInfo?.databaseSize ?? '-' }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.cpuUsage')">{{ formatPercent(serverInfo?.cpuUsagePercent) }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.uptime')">{{ formatUptime(serverInfo?.uptimeSeconds) }}</ElDescriptionsItem>
                <ElDescriptionsItem :label="t('redisCache.server.hitRate')">{{ formatPercent(serverInfo?.cacheHitRatePercent) }}</ElDescriptionsItem>
            </ElDescriptions>
        </ElCard>
        <ElCard class="art-table-card">
            <template #header
                ><div class="flex items-center justify-between gap-3">
                    <ElInput v-model="pattern" :placeholder="t('redisCache.filters.pattern')" @keyup.enter="loadKeys" clearable
                        ><template #prefix><ArtSvgIcon icon="ri:search-line" /></template
                    ></ElInput>
                    <div class="flex shrink-0 gap-2">
                        <ElButton @click="openDialog()"><ArtSvgIcon class="mr-1" icon="ri:add-line" />{{ t('redisCache.actions.add') }}</ElButton
                        ><ElButton :title="t('redisCache.actions.refresh')" @click="loadKeys" circle text
                            ><ArtSvgIcon icon="ri:refresh-line"
                        /></ElButton>
                    </div></div
            ></template>
            <ArtTable :data="sortedKeys" :loading="loading" :row-actions="rowActions" @sort-change="handleSortChange" height="calc(100vh - 330px)">
                <ElTableColumn :label="t('redisCache.fields.key')" min-width="300" prop="key" show-overflow-tooltip sortable="custom" />
                <ElTableColumn :label="t('redisCache.fields.type')" prop="type" sortable="custom" width="120" />
                <ElTableColumn :label="t('redisCache.fields.memory')" align="right" prop="memoryBytes" sortable="custom" width="140"
                    ><template #default="{ row }">{{ formatMemory(row.memoryBytes) }}</template></ElTableColumn
                >
                <ElTableColumn :label="t('redisCache.fields.length')" align="right" prop="length" sortable="custom" width="100" />
                <ElTableColumn :label="t('redisCache.fields.ttl')" align="right" prop="timeToLiveMilliseconds" sortable="custom" width="160"
                    ><template #default="{ row }">{{ formatTtl(row.timeToLiveMilliseconds) }}</template></ElTableColumn
                >
            </ArtTable>
        </ElCard>
        <ElDrawer
            v-model="dialogVisible"
            :title="dialogReadonly ? t('redisCache.dialog.view') : form.key ? t('redisCache.dialog.edit') : t('redisCache.dialog.add')"
            destroy-on-close
            width="680px">
            <ElForm label-width="110px"
                ><ElFormItem :label="t('redisCache.fields.key')" required
                    ><ElInput v-model="form.key" :disabled="dialogReadonly || Boolean(editingKey)" /></ElFormItem
                ><ElFormItem :label="t('redisCache.fields.type')"><ElInput :model-value="valueType" disabled /></ElFormItem
                ><ElFormItem v-if="!isCollection" :label="t('redisCache.fields.value')" required
                    ><ElInput v-model="form.value" :disabled="dialogReadonly" :rows="12" type="textarea" /></ElFormItem
                ><ElFormItem :label="t('redisCache.fields.expireSeconds')"
                    ><ElInputNumber v-model="form.expireSeconds" :disabled="dialogReadonly" :min="0" /></ElFormItem
            ></ElForm>
            <div v-if="isCollection" class="collection-items">
                <div class="collection-head">
                    <span class="font-medium">{{ t('redisCache.items.title', { loaded: items.length, total: itemsTotal }) }}</span>
                    <span v-if="itemsTruncated" class="collection-tip">{{ t('redisCache.items.truncated') }}</span>
                </div>
                <ElTable v-loading="itemsLoading && !items.length" :data="items" border height="360" size="small">
                    <ElTableColumn :label="t('redisCache.items.index')" align="right" prop="index" width="80" />
                    <ElTableColumn v-if="showFieldColumn" :label="fieldLabel" min-width="140" prop="field" show-overflow-tooltip />
                    <ElTableColumn :label="t('redisCache.fields.value')" min-width="220" prop="value" show-overflow-tooltip />
                    <ElTableColumn v-if="valueType === 'SortedSet'" :label="t('redisCache.items.score')" align="right" prop="score" width="120" />
                </ElTable>
                <div class="collection-more">
                    <ElButton v-if="nextCursor" :loading="itemsLoading" @click="loadMoreItems" size="small">{{
                        t('redisCache.items.loadMore')
                    }}</ElButton>
                    <span v-else-if="items.length" class="collection-tip">{{ t('redisCache.items.allLoaded') }}</span>
                    <span v-else-if="!itemsLoading" class="collection-tip">{{ t('redisCache.items.empty') }}</span>
                </div>
            </div>
            <template #footer
                ><ElButton @click="dialogVisible = false">{{ t('redisCache.actions.close') }}</ElButton
                ><ElButton v-if="!dialogReadonly && !isCollection" :loading="saving" @click="save" type="primary">{{
                    t('redisCache.actions.save')
                }}</ElButton></template
            >
        </ElDrawer>
    </div>
</template>

<script lang="ts" setup>
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { RowAction } from '@/types/component'
import {
    fetchDeleteRedisValue,
    fetchGetRedisItems,
    fetchGetRedisKeys,
    fetchGetRedisServerInfo,
    fetchGetRedisValue,
    fetchSaveRedisValue,
    type RedisCacheItem,
    type RedisServerInfo,
} from '@/api/system-manage'

defineOptions({ name: 'RedisCacheManagement' })
const { t } = useI18n()
const serverInfo = ref<RedisServerInfo>()
const keys = ref<Awaited<ReturnType<typeof fetchGetRedisKeys>>>([])
const pattern = ref('')
/** 当前排序字段与方向，order 为空表示不排序 */
const sortState = reactive<{ prop: string; order: string | null }>({ prop: '', order: null })
/** 键列表一次性返回，排序在前端完成；TTL 为负的永久键视为最大 */
const sortedKeys = computed(() => {
    const { prop, order } = sortState
    if (!prop || !order) return keys.value
    const direction = order === 'ascending' ? 1 : -1
    const valueOf = (row: Record<string, unknown>) => {
        const value = row[prop]
        if (prop === 'key' || prop === 'type') return String(value ?? '')
        const numeric = Number(value)
        return prop === 'timeToLiveMilliseconds' && numeric < 0 ? Number.MAX_SAFE_INTEGER : numeric
    }
    return [...keys.value].sort((a, b) => {
        const left = valueOf(a as Record<string, unknown>)
        const right = valueOf(b as Record<string, unknown>)
        const result = typeof left === 'number' && typeof right === 'number' ? left - right : String(left ?? '').localeCompare(String(right ?? ''))
        return result * direction
    })
})
/** 表头或右键菜单触发排序时记录状态 */
const handleSortChange = (sort: { prop?: string; order?: string | null }) => {
    sortState.prop = sort.prop ?? ''
    sortState.order = sort.order ?? null
}
const loading = ref(false)
const serverLoading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const dialogReadonly = ref(false)
const editingKey = ref('')
const valueType = ref('String')
const form = reactive({ key: '', value: '', expireSeconds: 0 })
/** 需要分页读取元素的集合类型，这些类型在界面上只读 */
const COLLECTION_TYPES = ['List', 'Set', 'Hash', 'SortedSet', 'Stream']
const isCollection = computed(() => COLLECTION_TYPES.includes(valueType.value))
const items = ref<RedisCacheItem[]>([])
const itemsTotal = ref(0)
const nextCursor = ref<string | null>(null)
const itemsTruncated = ref(false)
const itemsLoading = ref(false)
/** 递增的请求序号，关闭抽屉或切换键时丢弃过期响应 */
let itemsSeq = 0
/** Hash 与 Stream 有字段名或条目 ID 列，其他类型没有 */
const showFieldColumn = computed(() => valueType.value === 'Hash' || valueType.value === 'Stream')
const fieldLabel = computed(() => (valueType.value === 'Stream' ? t('redisCache.items.entryId') : t('redisCache.items.field')))
const loadServer = async () => {
    serverLoading.value = true
    try {
        serverInfo.value = await fetchGetRedisServerInfo()
    } finally {
        serverLoading.value = false
    }
}
const loadKeys = async () => {
    loading.value = true
    try {
        keys.value = await fetchGetRedisKeys(pattern.value || undefined)
    } finally {
        loading.value = false
    }
}
const loadAll = async () => {
    await Promise.all([loadServer(), loadKeys()])
}
const openDialog = async (key = '', readonly = false) => {
    dialogReadonly.value = readonly
    editingKey.value = key
    Object.assign(form, { key, value: '', expireSeconds: 0 })
    valueType.value = 'String'
    resetItems()
    dialogVisible.value = true
    if (key) {
        const result = await fetchGetRedisValue(key)
        Object.assign(form, {
            key: result.key,
            value: result.value,
            expireSeconds: result.timeToLiveMilliseconds > 0 ? Math.ceil(result.timeToLiveMilliseconds / 1000) : 0,
        })
        valueType.value = result.type
        if (COLLECTION_TYPES.includes(result.type)) await loadMoreItems()
    }
}
/** 清空已读取的集合元素，并让进行中的请求作废 */
const resetItems = () => {
    itemsSeq++
    items.value = []
    itemsTotal.value = 0
    nextCursor.value = null
    itemsTruncated.value = false
    itemsLoading.value = false
}
/** 读取集合类型的下一页元素：首次从头开始，之后沿用上一页返回的游标 */
const loadMoreItems = async () => {
    const seq = ++itemsSeq
    itemsLoading.value = true
    try {
        const page = await fetchGetRedisItems(form.key, nextCursor.value)
        if (seq !== itemsSeq) return
        items.value = [...items.value, ...page.items]
        itemsTotal.value = page.total
        nextCursor.value = page.nextCursor
        itemsTruncated.value = itemsTruncated.value || page.truncated
    } finally {
        if (seq === itemsSeq) itemsLoading.value = false
    }
}
const save = async () => {
    saving.value = true
    try {
        await fetchSaveRedisValue(form)
        ElMessage.success(t('redisCache.messages.saved'))
        dialogVisible.value = false
        await loadKeys()
    } finally {
        saving.value = false
    }
}
/** 行右键菜单操作，仅 String 类型允许编辑 */
const rowActions = (row: { key: string; type: string }): RowAction[] => {
    const actions: RowAction[] = [{ key: 'view', label: t('redisCache.actions.view'), icon: 'ri:eye-line', onClick: () => openDialog(row.key, true) }]
    if (row.type === 'String') {
        actions.push({ key: 'edit', label: t('redisCache.actions.edit'), icon: 'ri:pencil-line', onClick: () => openDialog(row.key) })
    }
    actions.push({
        key: 'delete',
        label: t('redisCache.actions.delete'),
        icon: 'ri:delete-bin-5-line',
        danger: true,
        onClick: () => deleteKey(row.key),
    })
    return actions
}
const deleteKey = async (key: string) => {
    await ElMessageBox.confirm(t('redisCache.messages.confirmDelete', { key }), t('redisCache.actions.delete'), { type: 'warning' })
    await fetchDeleteRedisValue(key)
    ElMessage.success(t('redisCache.messages.deleted'))
    await loadKeys()
}
const formatTtl = (value: number) => (value < 0 ? t('redisCache.fields.persistent') : `${Math.ceil(value / 1000)}s`)
const formatMemory = (value: number) =>
    value > 0
        ? value < 1024
            ? `${value} B`
            : value < 1024 * 1024
              ? `${(value / 1024).toFixed(1)} KB`
              : `${(value / 1024 / 1024).toFixed(1)} MB`
        : '-'
const formatPercent = (value?: number) => (value === undefined ? '-' : `${value.toFixed(2)}%`)
const formatUptime = (value?: number) =>
    value === undefined ? '-' : `${Math.floor(value / 86400)}d ${Math.floor((value % 86400) / 3600)}h ${Math.floor((value % 3600) / 60)}m`
onMounted(loadAll)
</script>

<style scoped>
.cache-page {
    display: flex;
    flex-direction: column;
    gap: 12px;
}
.server-card {
    flex-shrink: 0;
}
.collection-items {
    margin-top: 4px;
}
.collection-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;
}
.collection-tip {
    font-size: 12px;
    color: var(--el-text-color-secondary);
}
.collection-more {
    display: flex;
    justify-content: center;
    margin-top: 10px;
}
.cache-page :deep(.el-card__header) {
    padding: 14px 16px;
}
.cache-page > .art-table-card {
    flex: 1;
    min-height: 0;
}
.cache-page > .art-table-card :deep(.el-card__body) {
    height: calc(100% - 58px);
}
.cache-page > .art-table-card :deep(.el-input) {
    max-width: 420px;
}
</style>
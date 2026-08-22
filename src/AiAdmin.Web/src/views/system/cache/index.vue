<template>
    <div class="cache-page art-full-height">
        <ElCard class="server-card">
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
            <ArtTable :data="keys" :loading="loading" height="calc(100vh - 330px)">
                <ElTableColumn :label="t('redisCache.fields.key')" min-width="300" prop="key" show-overflow-tooltip />
                <ElTableColumn :label="t('redisCache.fields.type')" prop="type" width="120" />
                <ElTableColumn :label="t('redisCache.fields.memory')" width="140"
                    ><template #default="{ row }">{{ formatMemory(row.memoryBytes) }}</template></ElTableColumn
                >
                <ElTableColumn :label="t('redisCache.fields.length')" prop="length" width="100" />
                <ElTableColumn :label="t('redisCache.fields.ttl')" width="160"
                    ><template #default="{ row }">{{ formatTtl(row.timeToLiveMilliseconds) }}</template></ElTableColumn
                >
                <ElTableColumn :label="t('redisCache.fields.actions')" class-name="cache-actions-column" fixed="right" width="210"
                    ><template #default="{ row }"
                        ><ArtButtonTable :title="t('redisCache.actions.view')" @click="openDialog(row.key, true)" type="view" /><ArtButtonTable
                            v-if="row.type === 'String'"
                            :title="t('redisCache.actions.edit')"
                            @click="openDialog(row.key)"
                            type="edit" /><ArtButtonTable
                            :title="t('redisCache.actions.delete')"
                            @click="deleteKey(row.key)"
                            type="delete" /></template
                ></ElTableColumn>
            </ArtTable>
        </ElCard>
        <ElDialog
            v-model="dialogVisible"
            :title="dialogReadonly ? t('redisCache.dialog.view') : form.key ? t('redisCache.dialog.edit') : t('redisCache.dialog.add')"
            destroy-on-close
            width="680px">
            <ElForm label-width="110px"
                ><ElFormItem :label="t('redisCache.fields.key')" required
                    ><ElInput v-model="form.key" :disabled="dialogReadonly || Boolean(editingKey)" /></ElFormItem
                ><ElFormItem :label="t('redisCache.fields.type')"><ElInput :model-value="valueType" disabled /></ElFormItem
                ><ElFormItem :label="t('redisCache.fields.value')" required
                    ><ElInput v-model="form.value" :disabled="dialogReadonly" :rows="12" type="textarea" /></ElFormItem
                ><ElFormItem :label="t('redisCache.fields.expireSeconds')"
                    ><ElInputNumber v-model="form.expireSeconds" :disabled="dialogReadonly" :min="0" /></ElFormItem
            ></ElForm>
            <template #footer
                ><ElButton @click="dialogVisible = false">{{ t('redisCache.actions.close') }}</ElButton
                ><ElButton v-if="!dialogReadonly" :loading="saving" @click="save" type="primary">{{
                    t('redisCache.actions.save')
                }}</ElButton></template
            >
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { ElMessage, ElMessageBox } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import {
    fetchDeleteRedisValue,
    fetchGetRedisKeys,
    fetchGetRedisServerInfo,
    fetchGetRedisValue,
    fetchSaveRedisValue,
    type RedisServerInfo,
} from '@/api/system-manage'

defineOptions({ name: 'RedisCacheManagement' })
const { t } = useI18n()
const serverInfo = ref<RedisServerInfo>()
const keys = ref<Awaited<ReturnType<typeof fetchGetRedisKeys>>>([])
const pattern = ref('')
const loading = ref(false)
const serverLoading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const dialogReadonly = ref(false)
const editingKey = ref('')
const valueType = ref('String')
const form = reactive({ key: '', value: '', expireSeconds: 0 })
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
    dialogVisible.value = true
    if (key) {
        const result = await fetchGetRedisValue(key)
        Object.assign(form, {
            key: result.key,
            value: result.value,
            expireSeconds: result.timeToLiveMilliseconds > 0 ? Math.ceil(result.timeToLiveMilliseconds / 1000) : 0,
        })
        valueType.value = result.type
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
.cache-page :deep(.cache-actions-column .cell) {
    overflow: visible;
    text-overflow: clip;
}
</style>
<template>
    <ElDrawer
        v-model="dialogVisible"
        :title="t('scheduledJob.executionDetail.title')"
        align-center
        destroy-on-close
        width="min(1040px, calc(100vw - 32px))">
        <ElTabs v-if="execution" v-model="activeTab" class="execution-detail-tabs">
            <ElTabPane :label="t('scheduledJob.executionDetail.tabs.overview')" name="overview">
                <ElDescriptions :column="2" border>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.id')">
                        <ArtListIdCell :created-at="execution.startedAt" :id="execution.id" />
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.scheduledJobId')">
                        <ArtListIdCell :created-at="execution.createdAt" :id="execution.scheduledJobId" />
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.status')">
                        <ElTag :type="currentStatus.type" effect="light" size="small">{{ currentStatus.label }}</ElTag>
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.createdAt')">
                        {{ formatTime(execution.createdAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.startedAt')">
                        {{ formatTime(execution.startedAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.duration')">
                        {{ duration }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.requestMethod')">
                        <ElTag effect="light" size="small" type="info">{{ execution.requestMethod }}</ElTag>
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.responseStatusCode')">
                        {{ execution.responseStatusCode ?? '-' }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.requestUrl')" :span="2">
                        <span class="break-all">{{ execution.requestUrl || '-' }}</span>
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.errorMessage')" :span="2">
                        <span :class="{ 'error-message': execution.errorMessage }">{{ execution.errorMessage || '-' }}</span>
                    </ElDescriptionsItem>
                </ElDescriptions>
            </ElTabPane>
            <ElTabPane :label="t('scheduledJob.executionDetail.tabs.request')" name="request">
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.requestHeaders') }}</div>
                    <ArtJsonEditor :model-value="requestHeaders" class="execution-editor execution-header-editor" height="180px" readonly />
                </div>
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.requestBody') }}</div>
                    <ArtJsonEditor :model-value="requestBody" class="execution-editor" height="260px" readonly />
                </div>
            </ElTabPane>
            <ElTabPane :label="t('scheduledJob.executionDetail.tabs.response')" name="response">
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.responseHeaders') }}</div>
                    <ArtJsonEditor :model-value="responseHeaders" class="execution-editor execution-header-editor" height="180px" readonly />
                </div>
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.responseBody') }}</div>
                    <ArtJsonEditor :model-value="responseBody" class="execution-editor" height="260px" readonly />
                </div>
            </ElTabPane>
            <ElTabPane :label="t('rawData')" name="raw-data">
                <ArtRawData :data="execution" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="dialogVisible = false">{{ t('scheduledJob.executionDetail.close') }}</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { ScheduledJobExecution } from '@/api/system-manage'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtJsonEditor from '@/components/core/forms/art-json-editor/index.vue'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { formatDateTime } from '@/utils/date'

const props = defineProps<{
    visible: boolean
    execution?: ScheduledJobExecution
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
}>()
const { t, locale } = useI18n()
const activeTab = ref('overview')
const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value),
})
const statusMap = computed<Record<number, { label: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }>>(() => ({
    0: { label: t('scheduledJob.status.waiting'), type: 'info' },
    1: { label: t('scheduledJob.status.running'), type: 'primary' },
    2: { label: t('scheduledJob.status.success'), type: 'success' },
    3: { label: t('scheduledJob.status.failed'), type: 'danger' },
    4: { label: t('scheduledJob.status.timeout'), type: 'warning' },
}))
const currentStatus = computed(
    () =>
        statusMap.value[props.execution?.status ?? -1] || {
            label: t('scheduledJob.executionDetail.unknown'),
            type: 'info' as const,
        },
)
const duration = computed(() => {
    if (!props.execution?.createdAt) return '-'
    const milliseconds = Math.max(0, new Date(props.execution.createdAt).getTime() - new Date(props.execution.startedAt).getTime())
    return t('scheduledJob.executionDetail.durationValue', { value: milliseconds })
})
const formatTime = (value: string | null): string => (value ? formatDateTime(value, locale.value) : '-')
const tryFormatJson = (value: string, fallback: string): string => {
    if (!value?.trim()) return fallback
    try {
        return JSON.stringify(JSON.parse(value), null, 2)
    } catch {
        return value
    }
}
const requestHeaders = computed(() => tryFormatJson(props.execution?.requestHeaders || '', '{}'))
const requestBody = computed(() => tryFormatJson(props.execution?.requestBody || '', ''))
const responseHeaders = computed(() => tryFormatJson(props.execution?.responseHeaders || '', '{}'))
const responseBody = computed(() => tryFormatJson(props.execution?.responseBody || '', ''))

watch(dialogVisible, (visible) => {
    if (visible) activeTab.value = 'overview'
})
</script>

<style scoped>
.break-all {
    word-break: break-all;
}
.error-message {
    color: var(--el-color-danger);
}
.editor-section + .editor-section {
    margin-top: 18px;
}
.editor-label {
    margin-bottom: 8px;
    color: var(--el-text-color-primary);
    font-size: 14px;
    font-weight: 500;
}
.execution-editor {
    width: 100%;
    height: 260px;
    border: 1px solid var(--el-border-color-lighter);
    border-radius: 4px;
}
.execution-header-editor {
    height: 180px;
}
@media (max-width: 767px) {
    .execution-editor {
        height: 300px;
    }
}
</style>
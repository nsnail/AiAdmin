<template>
    <ElDialog
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
                        <ElTag :type="currentStatus.type">{{ currentStatus.label }}</ElTag>
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.createdAt')">
                        {{ formatTime(execution.createdAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.updatedAt')">
                        {{ formatTime(execution.updatedAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.startedAt')">
                        {{ formatTime(execution.startedAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.finishedAt')">
                        {{ formatTime(execution.finishedAt) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.duration')">
                        {{ duration }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('scheduledJob.executionDetail.fields.requestMethod')">
                        <ElTag type="info">{{ execution.requestMethod }}</ElTag>
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
                    <VAceEditor
                        :options="editorOptions"
                        :value="requestHeaders"
                        class="execution-editor execution-header-editor"
                        lang="json"
                        readonly
                        theme="tomorrow" />
                </div>
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.requestBody') }}</div>
                    <VAceEditor
                        :lang="requestBodyLanguage"
                        :options="editorOptions"
                        :value="requestBody"
                        class="execution-editor"
                        readonly
                        theme="tomorrow" />
                </div>
            </ElTabPane>
            <ElTabPane :label="t('scheduledJob.executionDetail.tabs.response')" name="response">
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.responseHeaders') }}</div>
                    <VAceEditor
                        :options="editorOptions"
                        :value="responseHeaders"
                        class="execution-editor execution-header-editor"
                        lang="json"
                        readonly
                        theme="tomorrow" />
                </div>
                <div class="editor-section">
                    <div class="editor-label">{{ t('scheduledJob.executionDetail.fields.responseBody') }}</div>
                    <VAceEditor
                        :lang="responseBodyLanguage"
                        :options="editorOptions"
                        :value="responseBody"
                        class="execution-editor"
                        readonly
                        theme="tomorrow" />
                </div>
            </ElTabPane>
            <ElTabPane :label="t('rawData')" name="raw-data">
                <VAceEditor
                    :options="editorOptions"
                    :value="rawData"
                    class="execution-editor raw-data-editor"
                    lang="json"
                    readonly
                    theme="tomorrow" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="dialogVisible = false">{{ t('scheduledJob.executionDetail.close') }}</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import { VAceEditor } from 'vue3-ace-editor'
import 'ace-builds/src-noconflict/mode-json'
import 'ace-builds/src-noconflict/mode-text'
import 'ace-builds/src-noconflict/theme-tomorrow'
import { useI18n } from 'vue-i18n'
import type { ScheduledJobExecution } from '@/api/system-manage'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'

const props = defineProps<{
    visible: boolean
    execution?: ScheduledJobExecution
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
}>()
const { t, locale } = useI18n()
const activeTab = ref('overview')
const editorOptions = {
    useWorker: false,
    tabSize: 2,
    useSoftTabs: true,
    showPrintMargin: false,
    showGutter: true,
    highlightActiveLine: false,
    wrap: true,
    fontSize: 13,
}
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
    if (!props.execution?.finishedAt) return '-'
    const milliseconds = Math.max(0, new Date(props.execution.finishedAt).getTime() - new Date(props.execution.startedAt).getTime())
    return t('scheduledJob.executionDetail.durationValue', { value: milliseconds })
})
const formatTime = (value: string | null): string =>
    value
        ? new Date(value).toLocaleString(locale.value.startsWith('zh') ? 'zh-CN' : 'en-US', {
              hour12: false,
          })
        : '-'
const tryFormatJson = (value: string, fallback: string): string => {
    if (!value?.trim()) return fallback
    try {
        return JSON.stringify(JSON.parse(value), null, 2)
    } catch {
        return value
    }
}
const isJson = (value: string): boolean => {
    if (!value?.trim()) return false
    try {
        JSON.parse(value)
        return true
    } catch {
        return false
    }
}
const requestHeaders = computed(() => tryFormatJson(props.execution?.requestHeaders || '', '{}'))
const requestBody = computed(() => tryFormatJson(props.execution?.requestBody || '', ''))
const responseHeaders = computed(() => tryFormatJson(props.execution?.responseHeaders || '', '{}'))
const responseBody = computed(() => tryFormatJson(props.execution?.responseBody || '', ''))
const requestBodyLanguage = computed(() => (isJson(props.execution?.requestBody || '') ? 'json' : 'text'))
const responseBodyLanguage = computed(() => (isJson(props.execution?.responseBody || '') ? 'json' : 'text'))
const rawData = computed(() => JSON.stringify(props.execution ?? null, null, 2))

watch(dialogVisible, (visible) => {
    if (visible) activeTab.value = 'overview'
})
</script>

<style scoped>
.execution-detail-tabs {
    min-height: 500px;
}
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
.raw-data-editor {
    height: 500px;
}
@media (max-width: 767px) {
    .execution-detail-tabs {
        min-height: 420px;
    }
    .execution-editor,
    .raw-data-editor {
        height: 300px;
    }
}
</style>
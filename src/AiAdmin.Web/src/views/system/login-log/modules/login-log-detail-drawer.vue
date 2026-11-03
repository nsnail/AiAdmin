<!-- 登录日志详情抽屉：基本信息与原始数据，登录日志页和仪表板最近登录共用 -->
<template>
    <ElDrawer v-model="dialogVisible" :title="t('loginLog.detail.title')" destroy-on-close width="850px">
        <ElTabs v-if="log" v-model="activeTab" type="card">
            <ElTabPane :label="t('loginLog.detail.tabs.details')" name="details">
                <ElDescriptions :column="2" :label-width="150" border>
                    <ElDescriptionsItem v-for="field in detailFields" :key="field" :label="t(`loginLog.fields.${field}`)">
                        <span class="detail-value">{{ formatValue(log[field]) }}</span>
                    </ElDescriptionsItem>
                </ElDescriptions>
            </ElTabPane>
            <ElTabPane :label="t('loginLog.detail.tabs.rawData')" name="rawData">
                <ArtRawData :data="log" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="dialogVisible = false">{{ t('loginLog.detail.close') }}</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { ElButton, ElDescriptions, ElDescriptionsItem, ElDrawer } from 'element-plus'
import { useI18n } from 'vue-i18n'
import type { LoginLogRecord } from '@/api/system-manage'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'

defineOptions({ name: 'LoginLogDetailDrawer' })

const props = defineProps<{
    visible: boolean
    log?: LoginLogRecord
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
}>()
const { t } = useI18n()
const activeTab = ref('details')
const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value),
})
const detailFields = [
    'userName',
    'ownerId',
    'ownerDepartmentId',
    'clientIp',
    'region',
    'operatingSystem',
    'browser',
    'deviceType',
    'platform',
    'language',
    'timeZone',
    'screenResolution',
    'viewportSize',
    'colorDepth',
    'pixelRatio',
    'touchPoints',
    'userAgent',
    'clientHints',
    'createdAt',
] as const

/**
 * 详情字段取值：空值显示为 -，对象转 JSON 文本
 * @param value 字段原始值
 * @returns 展示文本
 */
const formatValue = (value: unknown) =>
    value === undefined || value === null || value === '' ? '-' : typeof value === 'string' ? value : JSON.stringify(value)

// 每次打开都回到第一个页签
watch(dialogVisible, (visible) => {
    if (visible) activeTab.value = 'details'
})
</script>

<style scoped>
.detail-value {
    white-space: pre-wrap;
    word-break: break-word;
}
</style>
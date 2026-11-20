<!-- 用户详情全屏对话框：基本信息、该用户的操作日志（API 请求日志）与原始数据 -->
<template>
    <ElDialog
        v-model="dialogVisible"
        :title="t('userManagement.detail.title')"
        append-to-body
        class="detail-fullscreen-dialog"
        destroy-on-close
        fullscreen>
        <ElTabs v-if="user" v-model="activeTab" class="detail-fullscreen-tabs" type="card">
            <ElTabPane :label="t('userManagement.detail.tabs.basic')" name="basic">
                <ElDescriptions :column="2" :label-width="130" border>
                    <ElDescriptionsItem :label="t('userManagement.fields.userName')">{{ formatValue(user.userName) }}</ElDescriptionsItem>
                    <ElDescriptionsItem label="ID">{{ formatValue(user.id) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.email')">{{ formatValue(user.userEmail) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.phone')">{{ formatValue(user.userPhone) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.gender')">
                        {{ t(user.userGender === 2 ? 'userManagement.gender.female' : 'userManagement.gender.male') }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.status')">
                        {{ t(user.isEnabled ? 'userManagement.status.enabled' : 'userManagement.status.disabled') }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.roles')">{{ formatValue(user.roleNames.join(', ')) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.departments')">
                        {{ formatValue(user.departmentNames.join(', ')) }}
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.online')">
                        <span class="inline-flex items-center gap-2">
                            <span :class="['inline-block size-2 rounded-full', user.isOnline ? 'bg-success' : 'bg-g-300']"></span>
                            {{ t(user.isOnline ? 'userManagement.status.online' : 'userManagement.status.offline') }}
                        </span>
                    </ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.lastOnline')">{{ formatTime(user.lastOnlineAt) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.lastLogin')">{{ formatTime(user.lastLoginAt) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.lastLoginIp')">{{ formatValue(user.lastLoginIp) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.lastLoginRegion')">{{
                        formatValue(user.lastLoginRegion)
                    }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.createdAt')">{{ formatTime(user.createTime) }}</ElDescriptionsItem>
                    <ElDescriptionsItem :label="t('userManagement.fields.updatedAt')">{{ formatTime(user.updateTime) }}</ElDescriptionsItem>
                </ElDescriptions>
            </ElTabPane>
            <ElTabPane :label="t('userManagement.detail.tabs.operationLog')" lazy name="operationLog">
                <ApiRequestLogList :user-id="user.id" />
            </ElTabPane>
            <ElTabPane :label="t('userManagement.detail.tabs.rawData')" name="rawData">
                <ArtRawData :data="user" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton @click="dialogVisible = false">{{ t('userManagement.detail.close') }}</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import { ElButton, ElDescriptions, ElDescriptionsItem, ElDialog } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { formatDateTime } from '@/utils/date'
import ApiRequestLogList from '@/views/system/api-request-log/modules/api-request-log-list.vue'

defineOptions({ name: 'UserDetailDialog' })

const props = defineProps<{
    visible: boolean
    user?: Api.SystemManage.UserListItem
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
}>()
const { t, locale } = useI18n()

const activeTab = ref('basic')
const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value),
})

const formatTime = (value: string | null | undefined): string => (value ? formatDateTime(value, locale.value) : '-')
const formatValue = (value: unknown): string => (value === undefined || value === null || value === '' ? '-' : String(value))

// 每次打开都回到第一个页签；日志页签 lazy 且随对话框销毁，重新打开时列表重新初始化
watch(dialogVisible, (visible) => {
    if (visible) activeTab.value = 'basic'
})
</script>
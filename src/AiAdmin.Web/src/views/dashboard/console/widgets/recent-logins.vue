<!-- 最近登录记录 -->
<template>
    <WidgetCard :title="t('dashboard.widgets.recentLogins.title')" to="/log-management/login-log">
        <div v-if="!rows.length" class="flex-cc h-full text-sm text-g-500">{{ t('dashboard.empty') }}</div>
        <ElScrollbar v-else v-loading="opening">
            <div
                v-for="item in rows"
                :class="[
                    'flex items-center justify-between border-b border-g-200 py-2 text-sm last:border-0',
                    canOpen ? 'cursor-pointer hover:bg-g-100' : '',
                ]"
                :key="item.id"
                @click="openDetail(item.id)">
                <div class="min-w-0">
                    <p class="truncate font-medium text-g-900">{{ item.userName }}</p>
                    <p class="truncate text-xs text-g-500">
                        {{ item.clientIp }} · {{ item.region || t('dashboard.unknownRegion') }} · {{ item.browser }}
                    </p>
                </div>
                <span class="ml-3 shrink-0 text-xs text-g-500">{{ formatTime(item.createdAt) }}</span>
            </div>
        </ElScrollbar>
    </WidgetCard>
    <LoginLogDetailDrawer v-model:visible="detailVisible" :log="selectedLog" />
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { fetchGetLoginLogList, type LoginLogRecord } from '@/api/system-manage'
import LoginLogDetailDrawer from '@/views/system/login-log/modules/login-log-detail-drawer.vue'
import { useDashboardContext, useWidgetLink } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRecentLogins' })

const { t } = useI18n()
const { overview } = useDashboardContext()
const { linkable } = useWidgetLink(() => '/log-management/login-log')
const rows = computed(() => overview.value?.recentLogins ?? [])
/** 详情抽屉是否显示 */
const detailVisible = ref(false)
/** 当前查看的登录日志 */
const selectedLog = ref<LoginLogRecord>()
/** 是否正在加载详情，加载期间显示遮罩并忽略重复点击 */
const opening = ref(false)
/** 有登录日志页面权限且不在布局编辑模式时才可点击 */
const canOpen = computed(() => linkable())

/**
 * 点击条目弹出登录日志详情，复用登录日志页的详情抽屉；列表只含摘要，详情按主键查完整记录
 * @param id 登录日志主键
 */
async function openDetail(id: string) {
    if (!canOpen.value || opening.value) return
    opening.value = true
    try {
        const page = await fetchGetLoginLogList({ current: 1, size: 1, dynamicFilter: { field: 'Id', operator: 'Equal', value: id } })
        const record = page.records?.[0]
        if (record) {
            selectedLog.value = record
            detailVisible.value = true
        } else {
            ElMessage.warning(t('dashboard.loginLogNotFound'))
        }
    } finally {
        opening.value = false
    }
}
const formatTime = (value: string) => new Date(value).toLocaleString()
</script>
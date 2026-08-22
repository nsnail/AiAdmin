<template>
    <ElDrawer v-model="visible" :with-header="false" @closed="emit('update:value', false)" class="notification-drawer" direction="rtl" size="480px">
        <div class="notification-header">
            <span class="text-base font-medium">{{ t('notice.title') }}</span>
            <ArtIconButton @click="visible = false" icon="ri:close-line" />
        </div>
        <div v-if="items.length" class="notification-actions-bar">
            <ElButton @click="readAll" class="read-all-button" link size="small">
                <ArtSvgIcon icon="ri:mail-check-line" />
                {{ t('notice.btnRead') }}
            </ElButton>
            <ElPopconfirm
                :cancel-button-text="t('common.cancel')"
                :confirm-button-text="t('common.confirm')"
                :title="t('notice.clearConfirm')"
                @confirm="clearAll">
                <template #reference>
                    <ElButton class="clear-all-button" link size="small">
                        <ArtSvgIcon icon="ri:delete-bin-7-line" />
                        {{ t('notice.clearAll') }}
                    </ElButton>
                </template>
            </ElPopconfirm>
        </div>
        <div class="notification-body">
            <div @scroll="onScroll" class="notification-list scrollbar-thin" ref="listElement">
                <div v-for="item in items" :class="{ unread: !item.isRead }" :key="item.id" class="notification-item">
                    <div @click="toggle(item)" class="notification-row">
                        <ElAvatar :size="30" :src="item.senderAvatar || undefined" class="notification-avatar">{{
                            item.senderName.slice(0, 1)
                        }}</ElAvatar>
                        <div class="notification-sender-block">
                            <div class="notification-sender">{{ item.senderName }}</div>
                            <div class="notification-time">{{ formatDate(item.createdAt) }}</div>
                        </div>
                        <div class="notification-title">{{ item.title }}</div>
                        <ArtSvgIcon :icon="expandedId === item.id ? 'ri:arrow-up-s-line' : 'ri:arrow-down-s-line'" class="notification-expand" />
                    </div>
                    <div v-html="item.content" v-if="expandedId === item.id" class="notification-content" />
                    <div v-if="expandedId === item.id" class="notification-actions">
                        <ElPopconfirm
                            :cancel-button-text="t('common.cancel')"
                            :confirm-button-text="t('common.confirm')"
                            :title="t('notice.deleteConfirm')"
                            @confirm="remove(item.id)">
                            <template #reference>
                                <ArtIconButton @click.stop class="notification-delete-button" icon="ri:delete-bin-line" />
                            </template>
                        </ElPopconfirm>
                    </div>
                </div>
                <div v-if="loading" class="notification-state">{{ t('notice.loading') }}</div>
                <div v-else-if="!items.length" class="notification-state">{{ t('notice.empty') }}</div>
                <div v-else-if="!hasMore" class="notification-state">{{ t('notice.noMore') }}</div>
            </div>
        </div>
    </ElDrawer>
</template>
<script lang="ts" setup>
import {
    fetchClearNotifications,
    fetchDeleteNotification,
    fetchGetNotifications,
    fetchMarkAllNotificationsRead,
    fetchMarkNotificationRead,
} from '@/api/system-manage'
import { useI18n } from 'vue-i18n'
import mittBus from '@/utils/sys/mittBus'
defineOptions({ name: 'ArtNotification' })
const props = defineProps<{ value: boolean }>()
const emit = defineEmits<{ 'update:value': [value: boolean]; 'unread-change': [value: number] }>()
const { t } = useI18n()
const visible = ref(false)
const items = ref<Api.SystemManage.UserMessageListItem[]>([])
const page = ref(1)
const hasMore = ref(true)
const loading = ref(false)
const listElement = ref<HTMLElement>()
const unreadCount = ref(0)
const expandedId = ref<number>()
const publishUnread = () => emit('unread-change', unreadCount.value)
const load = async (reset = false) => {
    if (loading.value || (!hasMore.value && !reset)) return
    if (reset) {
        page.value = 1
        hasMore.value = true
        items.value = []
    }
    loading.value = true
    try {
        const result = await fetchGetNotifications(page.value, 20)
        if (reset) unreadCount.value = result.unreadCount
        if (reset && result.items.some((item) => item.isPopup && !item.isRead)) emit('update:value', true)
        items.value.push(...result.items)
        hasMore.value = result.hasMore
        page.value++
        publishUnread()
    } finally {
        loading.value = false
    }
}
const toggle = async (item: Api.SystemManage.UserMessageListItem) => {
    expandedId.value = expandedId.value === item.id ? undefined : item.id
    if (!item.isRead) {
        await fetchMarkNotificationRead(item.id)
        item.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
        publishUnread()
    }
}
const remove = async (id: number) => {
    const item = items.value.find((x) => x.id === id)
    await fetchDeleteNotification(id)
    items.value = items.value.filter((x) => x.id !== id)
    if (item && !item.isRead) {
        unreadCount.value = Math.max(0, unreadCount.value - 1)
        publishUnread()
    }
}
const readAll = async () => {
    await fetchMarkAllNotificationsRead()
    items.value.forEach((x) => (x.isRead = true))
    unreadCount.value = 0
    publishUnread()
}
const clearAll = async () => {
    await fetchClearNotifications()
    items.value = []
    hasMore.value = false
    unreadCount.value = 0
    publishUnread()
}
const onScroll = () => {
    const el = listElement.value
    if (el && el.scrollTop + el.clientHeight >= el.scrollHeight - 40) load()
}
const formatDate = (value: string) => new Date(value).toLocaleString()
watch(
    () => props.value,
    (value) => {
        visible.value = value
        if (value) load(true)
    },
)
let refreshTimer: ReturnType<typeof setInterval> | undefined
onMounted(() => {
    load(true)
    refreshTimer = setInterval(() => load(true), 30000)
    mittBus.on('refreshNotifications', () => load(true))
})
onBeforeUnmount(() => {
    if (refreshTimer) clearInterval(refreshTimer)
    mittBus.off('refreshNotifications')
})
</script>
<style scoped>
@reference '@styles/core/tailwind.css';
.notification-body {
}
.notification-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 6px 8px 4px;
}
.notification-actions-bar {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding: 0 8px 8px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}
.notification-actions-bar :deep(.art-svg-icon) {
    margin-right: 6px;
}
.read-all-button {
    --el-button-text-color: var(--el-text-color-secondary);
    --el-button-hover-text-color: var(--el-text-color-regular);
    color: var(--el-text-color-secondary) !important;
}
.clear-all-button {
    --el-button-text-color: var(--el-color-danger);
    --el-button-hover-text-color: var(--el-color-danger-light-3);
    color: var(--el-color-danger) !important;
}
.notification-list {
    height: calc(100% - 60px);
    overflow-y: auto;
    margin-top: 0;
}
.notification-item {
    padding: 11px 14px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}
.notification-row {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
}
.notification-avatar {
    flex: 0 0 auto;
}
.notification-sender-block {
    flex: 0 0 128px;
    min-width: 0;
}
.notification-sender {
    overflow: hidden;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
}
.notification-item.unread .notification-title {
    font-weight: 700;
}
.notification-item:not(.unread) .notification-title {
    color: var(--el-text-color-placeholder);
}
.notification-title {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    font-size: 13px;
    font-weight: 700;
    line-height: 1.4;
}
.notification-time {
    color: var(--el-text-color-secondary);
    font-size: 12px;
    white-space: nowrap;
}
.notification-expand {
    color: var(--el-text-color-secondary);
}
.notification-content {
    padding: 10px 24px 5px 0;
    overflow-wrap: anywhere;
    color: var(--el-text-color-regular);
    font-size: 13px;
    line-height: 1.6;
}
.notification-actions {
    display: flex;
    justify-content: flex-end;
}
.notification-delete-button {
    color: var(--el-color-danger);
    width: 28px;
    height: 28px;
    font-size: 16px;
    font-weight: 400;
}
.notification-state {
    padding: 16px;
    color: var(--el-text-color-secondary);
    text-align: center;
    font-size: 12px;
}
.bar-active {
    color: var(--theme-color) !important;
    border-bottom: 2px solid var(--theme-color);
}
.scrollbar-thin::-webkit-scrollbar {
    width: 5px !important;
}
.dark .scrollbar-thin::-webkit-scrollbar-track {
    background-color: var(--default-box-color);
}
.dark .scrollbar-thin::-webkit-scrollbar-thumb {
    background-color: #222 !important;
}
.notification-content :deep(p) {
    margin: 6px 0;
}
.notification-content :deep(img) {
    max-width: 100%;
    height: auto;
}
.notification-content :deep(ul),
.notification-content :deep(ol) {
    padding-left: 20px;
}
.notification-content :deep(a) {
    color: var(--el-color-primary);
}
.notification-drawer :deep(.el-drawer__body) {
    padding: 0;
}
</style>

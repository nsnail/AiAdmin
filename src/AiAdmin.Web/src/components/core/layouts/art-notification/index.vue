<template>
    <div
        v-show="visible"
        :style="{ transform: show ? 'scaleY(1)' : 'scaleY(0.9)', opacity: show ? 1 : 0 }"
        @click.stop
        class="art-notification-panel art-card-sm !shadow-xl">
        <div class="flex-cb px-3.5 mt-3.5">
            <span class="text-base font-medium">{{ t('notice.title') }}</span>
            <div v-if="items.length">
                <ElButton @click="readAll" link size="small">{{ t('notice.btnRead') }}</ElButton
                ><ElButton @click="clearAll" link size="small">{{ t('notice.clearAll') }}</ElButton>
            </div>
        </div>
        <div class="notification-body">
            <div @scroll="onScroll" class="notification-list scrollbar-thin" ref="listElement">
                <div v-for="item in items" :class="{ unread: !item.isRead }" :key="item.id" class="notification-item">
                    <div @click="toggle(item)" class="notification-row">
                        <div class="notification-title">{{ item.title }}</div>
                        <div class="notification-time">{{ formatDate(item.createdAt) }}</div>
                        <ArtSvgIcon :icon="expandedId === item.id ? 'ri:arrow-up-s-line' : 'ri:arrow-down-s-line'" class="notification-expand" />
                    </div>
                    <div v-html="item.content" v-if="expandedId === item.id" class="notification-content" />
                    <div class="notification-actions">
                        <ElButton @click.stop="remove(item.id)" link size="small"
                            ><ArtSvgIcon icon="ri:delete-bin-line" />{{ t('messageManagement.delete') }}</ElButton
                        >
                    </div>
                </div>
                <div v-if="loading" class="notification-state">{{ t('notice.loading') }}</div>
                <div v-else-if="!items.length" class="notification-state">{{ t('notice.empty') }}</div>
                <div v-else-if="!hasMore" class="notification-state">{{ t('notice.noMore') }}</div>
            </div>
        </div>
    </div>
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
const show = ref(false)
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
const animate = (value: boolean) => {
    if (value) {
        visible.value = true
        nextTick(() => (show.value = true))
        load(true)
    } else {
        show.value = false
        setTimeout(() => (visible.value = false), 300)
    }
}
watch(() => props.value, animate)
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
.art-notification-panel {
    @apply absolute top-14.5 right-5 w-90 h-125 overflow-hidden transition-all duration-300 origin-top will-change-[top,left] max-[640px]:top-[65px] max-[640px]:right-0 max-[640px]:w-full max-[640px]:h-[80vh];
}
.notification-body {
    height: calc(100% - 95px);
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
    white-space: nowrap;
    font-size: 13px;
    font-weight: 700;
}
.notification-time {
    color: var(--el-text-color-secondary);
    font-size: 12px;
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
</style>

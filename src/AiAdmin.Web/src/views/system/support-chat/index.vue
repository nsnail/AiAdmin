<!-- 在线客服工作台：左侧会话列表，右侧对话窗口，用户消息通过 WebSocket 实时到达 -->
<template>
    <div class="support-chat art-full-height">
        <ElCard class="support-card" shadow="never">
            <div class="support-layout">
                <!-- 会话列表 -->
                <aside class="conversation-pane">
                    <div class="pane-header">
                        <ElInput
                            v-model="keyword"
                            :placeholder="t('supportChat.searchUser')"
                            @keyup.enter="reloadConversations"
                            clearable
                            size="default" />
                        <ElRadioGroup v-model="statusFilter" @change="reloadConversations" size="small">
                            <ElRadioButton value="all">{{ t('supportChat.filter.all') }}</ElRadioButton>
                            <ElRadioButton value="open">{{ t('supportChat.filter.open') }}</ElRadioButton>
                            <ElRadioButton value="closed">{{ t('supportChat.filter.closed') }}</ElRadioButton>
                        </ElRadioGroup>
                    </div>
                    <div v-loading="listLoading" @scroll="onListScroll" class="conversation-list" ref="listRef">
                        <div v-if="!listLoading && !conversations.length" class="empty-tip">{{ t('supportChat.noConversation') }}</div>
                        <div
                            v-for="item in conversations"
                            :class="{ active: item.id === activeId }"
                            :key="item.id"
                            @click="selectConversation(item)"
                            class="conversation-item">
                            <ElBadge :hidden="!item.serviceUnreadCount" :max="99" :value="item.serviceUnreadCount">
                                <ArtUserAvatar :name="item.userName" :size="40" :src="item.userAvatar" />
                            </ElBadge>
                            <div class="conversation-body">
                                <div class="conversation-top">
                                    <span class="conversation-name">{{ item.userName }}</span>
                                    <span class="conversation-time">{{ formatListTime(item.lastMessageAt) }}</span>
                                </div>
                                <div class="conversation-preview">
                                    <ElTag v-if="item.isClosed" size="small" type="info">{{ t('supportChat.closedTag') }}</ElTag>
                                    <span class="truncate">{{ item.lastMessagePreview }}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </aside>

                <!-- 对话窗口 -->
                <section class="chat-pane">
                    <div v-if="!activeConversation" class="chat-empty">{{ t('supportChat.selectTip') }}</div>
                    <template v-else>
                        <div class="chat-header">
                            <div class="chat-title">
                                <ArtUserAvatar :name="activeConversation.userName" :size="32" :src="activeConversation.userAvatar" />
                                <span class="font-medium">{{ activeConversation.userName }}</span>
                                <ElTag v-if="activeConversation.isClosed" size="small" type="info">{{ t('supportChat.closedTag') }}</ElTag>
                            </div>
                            <ElPopconfirm
                                v-if="!activeConversation.isClosed"
                                :title="t('supportChat.closeConfirm')"
                                @confirm="closeConversation"
                                width="260">
                                <template #reference>
                                    <ElButton :loading="closing" plain size="small">{{ t('supportChat.close') }}</ElButton>
                                </template>
                            </ElPopconfirm>
                        </div>
                        <div v-loading="messageLoading" @scroll="onMessageScroll" class="message-area" ref="messageRef">
                            <div v-if="hasMore" class="load-more-tip">{{ t('supportChat.loadMoreHint') }}</div>
                            <div
                                v-for="message in messages"
                                :class="['message-row', message.isFromService ? 'is-service' : 'is-user']"
                                :key="message.id">
                                <ArtUserAvatar :name="message.senderName" :size="32" class="shrink-0" />
                                <div class="message-main">
                                    <div class="message-meta">
                                        <span class="font-medium">{{ message.senderName }}</span>
                                        <span class="text-g-600">{{ formatTime(message.createdAt) }}</span>
                                    </div>
                                    <div class="message-bubble">{{ message.content }}</div>
                                </div>
                            </div>
                        </div>
                        <div class="reply-area">
                            <ElInput
                                v-model="replyText"
                                :placeholder="t('supportChat.replyPlaceholder')"
                                :rows="3"
                                @keydown.enter.exact.prevent="sendReply"
                                maxlength="2000"
                                resize="none"
                                show-word-limit
                                type="textarea" />
                            <div class="reply-footer">
                                <span class="text-xs text-g-500">{{ t('supportChat.sendHint') }}</span>
                                <ElButton v-ripple :disabled="!replyText.trim()" :loading="sending" @click="sendReply" type="primary">
                                    {{ t('supportChat.send') }}
                                </ElButton>
                            </div>
                        </div>
                    </template>
                </section>
            </div>
        </ElCard>
    </div>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import {
    fetchCloseSupportConversation,
    fetchGetSupportConversations,
    fetchGetSupportMessages,
    fetchReplySupport,
    type DynamicFilter,
} from '@/api/system-manage'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import mittBus from '@/utils/sys/mittBus'

defineOptions({ name: 'SupportChat' })

/** 会话列表每页数量 */
const LIST_PAGE_SIZE = 20
/** 滚动到边缘多少像素内触发加载更多 */
const SCROLL_THRESHOLD = 40

const { t } = useI18n()

const keyword = ref('')
const statusFilter = ref<'all' | 'open' | 'closed'>('all')
const conversations = ref<Api.SystemManage.SupportConversationListItem[]>([])
const listPage = ref(1)
const listTotal = ref(0)
const listLoading = ref(false)
const listRef = ref<HTMLElement>()

const activeId = ref<number>()
const messages = ref<Api.SystemManage.SupportMessageItem[]>([])
const hasMore = ref(false)
const messageLoading = ref(false)
const loadingMore = ref(false)
const messageRef = ref<HTMLElement>()
const replyText = ref('')
const sending = ref(false)
const closing = ref(false)

const activeConversation = computed(() => conversations.value.find((x) => x.id === activeId.value))

/**
 * 格式化消息时间为本地时分
 * @param value ISO 时间字符串
 */
const formatTime = (value: string): string => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })

/**
 * 格式化会话列表时间：当天显示时分，否则显示月日
 * @param value ISO 时间字符串
 */
const formatListTime = (value: string): string => {
    const date = new Date(value)
    return date.toDateString() === new Date().toDateString() ? formatTime(value) : date.toLocaleDateString([], { month: '2-digit', day: '2-digit' })
}

/**
 * 按用户名和状态构建动态筛选条件，统一使用 dynamicFilter 协议
 * @returns 动态筛选根节点，没有任何条件时返回 undefined
 */
const buildFilter = (): DynamicFilter | undefined => {
    const filters: DynamicFilter[] = []
    const name = keyword.value.trim()
    if (name) filters.push({ field: 'userName', operator: 'Contains', value: name })
    if (statusFilter.value !== 'all') {
        filters.push({ field: 'isClosed', operator: 'Equal', value: statusFilter.value === 'closed' })
    }
    return filters.length ? { logic: 'And', filters } : undefined
}

/**
 * 加载会话列表的一页
 * @param reset 是否从第一页重新加载
 */
const loadConversations = async (reset = false): Promise<void> => {
    if (listLoading.value) return
    if (reset) {
        listPage.value = 1
        conversations.value = []
    } else if (conversations.value.length >= listTotal.value && listTotal.value > 0) {
        return
    }
    listLoading.value = true
    try {
        const result = await fetchGetSupportConversations({
            current: listPage.value,
            size: LIST_PAGE_SIZE,
            dynamicFilter: buildFilter(),
            sortField: 'lastMessageAt',
            sortOrder: 'desc',
        })
        const known = new Set(conversations.value.map((x) => x.id))
        conversations.value.push(...result.records.filter((x) => !known.has(x.id)))
        listTotal.value = result.total
        listPage.value++
    } finally {
        listLoading.value = false
    }
}

/** 重置并重新加载会话列表，筛选条件变化后回到第一页 */
const reloadConversations = (): Promise<void> => loadConversations(true)

/** 会话列表滚动到底部时加载下一页 */
const onListScroll = (): void => {
    const el = listRef.value
    if (el && el.scrollTop + el.clientHeight >= el.scrollHeight - SCROLL_THRESHOLD) loadConversations()
}

/** 滚动对话窗口到底部 */
const scrollToBottom = (): void => {
    nextTick(() => {
        if (messageRef.value) messageRef.value.scrollTop = messageRef.value.scrollHeight
    })
}

/**
 * 选中会话并加载消息，后端会把用户消息标记为客服已读
 * @param item 会话列表项
 */
const selectConversation = async (item: Api.SystemManage.SupportConversationListItem): Promise<void> => {
    activeId.value = item.id
    messages.value = []
    hasMore.value = false
    messageLoading.value = true
    try {
        const result = await fetchGetSupportMessages(item.id)
        // 加载期间用户可能又切换了会话，旧结果直接丢弃
        if (activeId.value !== item.id) return
        messages.value = result.items
        hasMore.value = result.hasMore
        item.serviceUnreadCount = 0
        scrollToBottom()
    } finally {
        messageLoading.value = false
    }
}

/** 对话窗口滚动到顶部时加载更早的消息，并保持当前阅读位置不跳动 */
const onMessageScroll = async (): Promise<void> => {
    const el = messageRef.value
    if (!el || !hasMore.value || loadingMore.value || el.scrollTop > SCROLL_THRESHOLD || !messages.value.length || activeId.value === undefined)
        return
    loadingMore.value = true
    const previousHeight = el.scrollHeight
    const conversationId = activeId.value
    try {
        const result = await fetchGetSupportMessages(conversationId, messages.value[0].id)
        if (activeId.value !== conversationId) return
        messages.value = [...result.items, ...messages.value]
        hasMore.value = result.hasMore
        await nextTick()
        el.scrollTop = el.scrollHeight - previousHeight
    } finally {
        loadingMore.value = false
    }
}

/**
 * 追加一条消息，按消息主键去重，避免接口返回与实时推送重复显示
 * @param message 新消息
 */
const appendMessage = (message: Api.SystemManage.SupportMessageItem): void => {
    if (messages.value.some((x) => x.id === message.id)) return
    messages.value.push(message)
    scrollToBottom()
}

/** 回复当前会话的用户 */
const sendReply = async (): Promise<void> => {
    const text = replyText.value.trim()
    const conversation = activeConversation.value
    if (!text || !conversation || sending.value) return
    sending.value = true
    try {
        const message = await fetchReplySupport(conversation.id, text)
        appendMessage(message)
        replyText.value = ''
        conversation.lastMessagePreview = message.content
        conversation.lastMessageAt = message.createdAt
        conversation.isClosed = false
    } finally {
        sending.value = false
    }
}

/** 关闭当前会话，用户再次发言时会话自动重新打开 */
const closeConversation = async (): Promise<void> => {
    const conversation = activeConversation.value
    if (!conversation) return
    closing.value = true
    try {
        await fetchCloseSupportConversation(conversation.id)
        conversation.isClosed = true
    } finally {
        closing.value = false
    }
}

/**
 * 处理实时推送的客服消息：更新或新增会话并置顶；正在查看的会话直接显示并向后端确认已读
 * @param payload 消息推送载荷
 */
const onRealtimeMessage = (payload: Api.SystemManage.RealtimeSupportMessage): void => {
    // 客服自己发出的回复由接口返回追加，这里只同步列表摘要，避免重复
    const { message } = payload
    const viewing = activeId.value === message.conversationId
    let conversation = conversations.value.find((x) => x.id === message.conversationId)
    if (!conversation) {
        // 新会话：只有在没有筛选条件时才直接插入列表，否则等用户刷新
        if (keyword.value.trim() || statusFilter.value === 'closed') return
        conversation = {
            id: message.conversationId,
            createdAt: message.createdAt,
            userId: payload.ownerUserId,
            userName: payload.userName,
            userAvatar: null,
            lastMessagePreview: message.content,
            lastMessageAt: message.createdAt,
            serviceUnreadCount: 0,
            isClosed: false,
        }
        conversations.value.unshift(conversation)
    }
    conversation.lastMessagePreview = message.content
    conversation.lastMessageAt = message.createdAt
    conversation.isClosed = false
    conversations.value = [conversation, ...conversations.value.filter((x) => x.id !== conversation.id)]
    if (message.isFromService) return
    if (viewing) {
        appendMessage(message)
        conversation.serviceUnreadCount = 0
        // 正在查看的会话立即确认已读，避免其他客服页面角标残留
        fetchGetSupportMessages(message.conversationId)
    } else {
        conversation.serviceUnreadCount = payload.serviceUnreadCount
    }
}

/**
 * 处理实时推送的已读状态，同步其他客服页面看到的未读数
 * @param payload 已读状态推送载荷
 */
const onRealtimeRead = (payload: Api.SystemManage.RealtimeSupportRead): void => {
    const conversation = conversations.value.find((x) => x.id === payload.conversationId)
    if (conversation) conversation.serviceUnreadCount = payload.serviceUnreadCount
}

onMounted(() => {
    loadConversations(true)
    mittBus.on('realtimeSupportMessage', onRealtimeMessage)
    mittBus.on('realtimeSupportRead', onRealtimeRead)
})

onBeforeUnmount(() => {
    mittBus.off('realtimeSupportMessage', onRealtimeMessage)
    mittBus.off('realtimeSupportRead', onRealtimeRead)
})
</script>

<style lang="scss" scoped>
.support-chat {
    display: flex;
    flex-direction: column;
}

.support-card {
    flex: 1;
    min-height: 0;

    :deep(.el-card__body) {
        height: 100%;
        padding: 0;
    }
}

.support-layout {
    display: flex;
    height: 100%;
    min-height: 0;
}

.conversation-pane {
    display: flex;
    flex-direction: column;
    width: 320px;
    min-height: 0;
    border-right: 1px solid var(--el-border-color-lighter);
}

.pane-header {
    display: flex;
    flex-direction: column;
    gap: 10px;
    padding: 12px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.conversation-list {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
}

.empty-tip {
    padding: 40px 0;
    font-size: 13px;
    color: var(--el-text-color-secondary);
    text-align: center;
}

.conversation-item {
    display: flex;
    gap: 10px;
    align-items: center;
    padding: 12px;
    cursor: pointer;

    &:hover {
        background: var(--el-fill-color-light);
    }

    &.active {
        background: var(--el-color-primary-light-9);
    }
}

.conversation-body {
    flex: 1;
    min-width: 0;
}

.conversation-top {
    display: flex;
    gap: 8px;
    align-items: center;
    justify-content: space-between;
}

.conversation-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.conversation-time {
    flex-shrink: 0;
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.conversation-preview {
    display: flex;
    gap: 6px;
    align-items: center;
    margin-top: 4px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.chat-pane {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
    min-height: 0;
}

.chat-empty {
    display: flex;
    flex: 1;
    align-items: center;
    justify-content: center;
    color: var(--el-text-color-secondary);
}

.chat-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px 16px;
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.chat-title {
    display: flex;
    gap: 10px;
    align-items: center;
}

.message-area {
    flex: 1;
    min-height: 0;
    padding: 20px 16px;
    overflow-y: auto;
}

.load-more-tip {
    margin-bottom: 16px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
    text-align: center;
}

.message-row {
    display: flex;
    gap: 8px;
    align-items: flex-start;
    margin-bottom: 24px;

    &.is-service {
        flex-direction: row-reverse;

        .message-main {
            align-items: flex-end;
        }

        .message-meta {
            flex-direction: row-reverse;
        }

        .message-bubble {
            background: color-mix(in srgb, var(--el-color-primary) 15%, transparent);
        }
    }

    &.is-user .message-bubble {
        background: var(--el-fill-color);
    }
}

.message-main {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    max-width: 70%;
}

.message-meta {
    display: flex;
    gap: 8px;
    margin-bottom: 4px;
    font-size: 12px;
}

.message-bubble {
    padding: 10px 14px;
    font-size: 14px;
    line-height: 1.4;
    word-break: break-word;
    white-space: pre-wrap;
    border-radius: 6px;
}

.reply-area {
    padding: 12px 16px 16px;
    border-top: 1px solid var(--el-border-color-lighter);
}

.reply-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 10px;
}
</style>
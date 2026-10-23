<!-- 在线客服浮层：用户通过顶部聊天按钮打开，与客服管理员对话，消息经 WebSocket 实时推送 -->
<template>
    <div>
        <ElDrawer v-model="isDrawerVisible" :size="isMobile ? '100%' : '480px'" :with-header="false" class="chat-drawer" destroy-on-close>
            <div class="mb-5 flex-cb">
                <div>
                    <span class="text-base font-medium">{{ t('supportChat.title') }}</span>
                    <div class="mt-1.5 flex-c gap-1">
                        <div class="h-2 w-2 rounded-full bg-success/100"></div>
                        <span class="text-xs text-g-600">{{ t('supportChat.subtitle') }}</span>
                    </div>
                </div>
                <div>
                    <ElIcon :size="20" @click="closeChat" class="c-p">
                        <Close />
                    </ElIcon>
                </div>
            </div>
            <div class="flex h-[calc(100%-70px)] flex-col">
                <!-- 聊天消息区域 -->
                <div
                    v-loading="loading"
                    @scroll="onScroll"
                    class="flex-1 overflow-y-auto border-t-d px-4 py-7.5 [&::-webkit-scrollbar]:!w-1"
                    ref="messageContainer">
                    <div v-if="!loading && !messages.length" class="flex-cc h-full text-sm text-g-500">{{ t('supportChat.empty') }}</div>
                    <div v-else-if="hasMore" class="mb-4 text-center text-xs text-g-500">{{ t('supportChat.loadMoreHint') }}</div>
                    <template v-for="message in messages" :key="message.id">
                        <div :class="['mb-7.5 flex w-full items-start gap-2', message.isFromService ? 'flex-row' : 'flex-row-reverse']">
                            <ArtUserAvatar
                                :name="message.isFromService ? t('supportChat.service') : message.senderName"
                                :size="32"
                                class="shrink-0" />
                            <div :class="['flex max-w-[70%] flex-col', message.isFromService ? 'items-start' : 'items-end']">
                                <div :class="['mb-1 flex gap-2 text-xs', message.isFromService ? 'flex-row' : 'flex-row-reverse']">
                                    <span class="font-medium">{{ message.isFromService ? t('supportChat.service') : t('supportChat.me') }}</span>
                                    <span class="text-g-600">{{ formatTime(message.createdAt) }}</span>
                                </div>
                                <div
                                    :class="[
                                        'whitespace-pre-wrap break-words rounded-md px-3.5 py-2.5 text-sm leading-[1.4] text-g-900',
                                        message.isFromService ? 'message-left bg-g-300/50' : 'message-right bg-theme/15',
                                    ]">
                                    {{ message.content }}
                                </div>
                            </div>
                        </div>
                    </template>
                </div>

                <!-- 聊天输入区域 -->
                <div class="px-4 pt-4">
                    <ElInput
                        v-model="messageText"
                        :placeholder="t('supportChat.placeholder')"
                        :rows="3"
                        @keydown.enter.exact.prevent="sendMessage"
                        maxlength="2000"
                        resize="none"
                        show-word-limit
                        type="textarea" />
                    <div class="mt-3 flex-cb">
                        <span class="text-xs text-g-500">{{ t('supportChat.sendHint') }}</span>
                        <ElButton v-ripple :disabled="!messageText.trim()" :loading="sending" @click="sendMessage" class="min-w-20" type="primary">
                            {{ t('supportChat.send') }}
                        </ElButton>
                    </div>
                </div>
            </div>
        </ElDrawer>
    </div>
</template>

<script lang="ts" setup>
import { Close } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'
import { fetchGetMySupportMessages, fetchGetMySupportState, fetchSendSupportMessage } from '@/api/system-manage'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import { useUserStore } from '@/store/modules/user'
import { mittBus } from '@/utils/sys'

defineOptions({ name: 'ArtChatWindow' })

/** 移动端断点，小于该宽度时浮层全屏 */
const MOBILE_BREAKPOINT = 640
/** 滚动到顶部多少像素内触发加载更早的消息 */
const LOAD_MORE_THRESHOLD = 40

const { t } = useI18n()
const userStore = useUserStore()
const { width } = useWindowSize()
const isMobile = computed(() => width.value < MOBILE_BREAKPOINT)

const isDrawerVisible = ref(false)
const loading = ref(false)
const sending = ref(false)
const hasMore = ref(false)
const loadingMore = ref(false)
const messageText = ref('')
const messages = ref<Api.SystemManage.SupportMessageItem[]>([])
const messageContainer = ref<HTMLElement | null>(null)
/** 我的会话主键，发消息或读取状态后确定 */
const conversationId = ref<number | null>(null)
/**
 * 判断推送是否属于自己的会话：客服的连接会收到所有用户的会话推送，必须按会话所属用户过滤
 * @param ownerUserId 推送载荷中的会话所属用户主键
 */
const isMine = (ownerUserId: number): boolean => String(ownerUserId) === String(userStore.info?.userId)
/** 浮层未打开期间累计的客服未读数 */
const unreadCount = ref(0)

/**
 * 格式化消息时间为本地时分
 * @param value ISO 时间字符串
 */
const formatTime = (value: string): string => new Date(value).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })

/** 滚动到消息列表底部 */
const scrollToBottom = (): void => {
    nextTick(() => {
        if (messageContainer.value) messageContainer.value.scrollTop = messageContainer.value.scrollHeight
    })
}

/**
 * 同步未读数并通知顶部按钮刷新角标
 * @param count 当前未读数
 */
const setUnread = (count: number): void => {
    unreadCount.value = count
    mittBus.emit('supportUnreadChange', count)
}

/** 加载最近的消息，后端会同时把客服消息标记为已读 */
const loadMessages = async (): Promise<void> => {
    loading.value = true
    try {
        const result = await fetchGetMySupportMessages()
        messages.value = result.items
        hasMore.value = result.hasMore
        if (result.items.length) conversationId.value = result.items[0].conversationId
        setUnread(0)
        scrollToBottom()
    } finally {
        loading.value = false
    }
}

/** 向上滚动到顶部时加载更早的消息，并保持当前阅读位置不跳动 */
const onScroll = async (): Promise<void> => {
    const el = messageContainer.value
    if (!el || !hasMore.value || loadingMore.value || el.scrollTop > LOAD_MORE_THRESHOLD || !messages.value.length) return
    loadingMore.value = true
    const previousHeight = el.scrollHeight
    try {
        const result = await fetchGetMySupportMessages(messages.value[0].id)
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

/** 发送消息，以接口返回的消息为准追加，失败时保留输入内容 */
const sendMessage = async (): Promise<void> => {
    const text = messageText.value.trim()
    if (!text || sending.value) return
    sending.value = true
    try {
        const message = await fetchSendSupportMessage(text)
        conversationId.value = message.conversationId
        appendMessage(message)
        messageText.value = ''
    } finally {
        sending.value = false
    }
}

/** 打开聊天窗口并加载消息 */
const openChat = (): void => {
    isDrawerVisible.value = true
    loadMessages()
}

/** 关闭聊天窗口 */
const closeChat = (): void => {
    isDrawerVisible.value = false
}

/**
 * 处理实时推送的客服消息：只处理自己会话的消息；浮层打开时直接显示并保持已读，否则累计未读
 * @param payload 消息推送载荷
 */
const onRealtimeMessage = (payload: Api.SystemManage.RealtimeSupportMessage): void => {
    if (!isMine(payload.ownerUserId)) return
    if (!payload.message.isFromService) {
        // 自己在其他页面发出的消息，浮层打开时同步显示
        if (isDrawerVisible.value) appendMessage(payload.message)
        return
    }
    if (isDrawerVisible.value) {
        appendMessage(payload.message)
        // 浮层已打开，立即向后端确认已读，避免角标反复出现
        fetchGetMySupportMessages().then(() => setUnread(0))
    } else {
        conversationId.value = payload.message.conversationId
        setUnread(payload.userUnreadCount)
    }
}

/**
 * 处理实时推送的已读状态：自己在其他页面读过后，同步清除本页角标
 * @param payload 已读状态推送载荷
 */
const onRealtimeRead = (payload: Api.SystemManage.RealtimeSupportRead): void => {
    if (!isMine(payload.ownerUserId)) return
    setUnread(payload.userUnreadCount)
}

/** 初始化角标：页面加载后读取自己会话的未读数 */
const loadState = async (): Promise<void> => {
    const state = await fetchGetMySupportState()
    conversationId.value = state.conversationId
    setUnread(state.userUnreadCount)
}

onMounted(() => {
    mittBus.on('openChat', openChat)
    mittBus.on('realtimeSupportMessage', onRealtimeMessage)
    mittBus.on('realtimeSupportRead', onRealtimeRead)
    loadState()
})

onUnmounted(() => {
    mittBus.off('openChat', openChat)
    mittBus.off('realtimeSupportMessage', onRealtimeMessage)
    mittBus.off('realtimeSupportRead', onRealtimeRead)
})
</script>
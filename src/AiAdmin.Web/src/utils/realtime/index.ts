/**
 * 实时推送连接：与后端 /api/ws 保持一条 WebSocket 长连接，并把推送的消息通过全局事件总线分发
 *
 * - system-info：后端系统名称、版本号和更新内容（每次连接建立时推送），用于发现后端升级
 * - unread：当前用户站内信未读数量变化
 * - support-message：在线客服消息，用户收到自己会话的消息，客服收到所有会话的消息
 * - support-read：在线客服会话已读状态，用于多个页面之间同步未读数
 *
 * 连接底层复用 WebSocketClient（心跳、断线重连），断线重连后后端会重新推送上述两类消息
 */
import type { DashboardSystemInfo } from '@/api/dashboard'
import { useUserStore } from '@/store/modules/user'
import WebSocketClient from '@/utils/socket'
import mittBus from '@/utils/sys/mittBus'

/** 后端推送的消息信封 */
interface RealtimeEnvelope {
    type: string
    data: unknown
}

/** 重连基础间隔（毫秒），实际间隔按指数退避递增 */
const RECONNECT_INTERVAL = 5000
/** 最大连续重连次数，令牌失效等无法恢复的情况下避免无限重试 */
const MAX_RECONNECT_ATTEMPTS = 30

/**
 * 根据接口基础地址和访问令牌生成 WebSocket 地址
 * 浏览器 WebSocket 无法携带请求头，因此令牌通过查询参数传递
 * @param token 访问令牌
 * @returns WebSocket 完整地址
 */
function buildUrl(token: string) {
    const base = new URL(import.meta.env.VITE_API_URL || '/', window.location.origin)
    const protocol = base.protocol === 'https:' ? 'wss:' : 'ws:'
    const path = base.pathname.replace(/\/$/, '')
    return `${protocol}//${base.host}${path}/api/ws?access_token=${encodeURIComponent(token)}`
}

/**
 * 处理后端推送的消息，非 JSON 文本（如心跳回复 pong）直接忽略
 * @param event WebSocket 消息事件
 */
function handleMessage(event: MessageEvent) {
    if (typeof event.data !== 'string') return
    let envelope: RealtimeEnvelope
    try {
        envelope = JSON.parse(event.data) as RealtimeEnvelope
    } catch {
        return
    }
    if (envelope.type === 'system-info') {
        mittBus.emit('realtimeSystemInfo', envelope.data as DashboardSystemInfo)
    } else if (envelope.type === 'unread') {
        mittBus.emit('realtimeUnread', envelope.data as { count: number; hasNew: boolean })
    } else if (envelope.type === 'support-message') {
        mittBus.emit('realtimeSupportMessage', envelope.data as Api.SystemManage.RealtimeSupportMessage)
    } else if (envelope.type === 'support-read') {
        mittBus.emit('realtimeSupportRead', envelope.data as Api.SystemManage.RealtimeSupportRead)
    }
}

/** 建立实时连接，未登录时不连接 */
export function startRealtime() {
    const { accessToken } = useUserStore()
    if (!accessToken) return
    WebSocketClient.getInstance({
        url: buildUrl(accessToken),
        messageHandler: handleMessage,
        reconnectInterval: RECONNECT_INTERVAL,
        maxReconnectAttempts: MAX_RECONNECT_ATTEMPTS,
    }).init()
}

/** 关闭实时连接并销毁实例 */
export function stopRealtime() {
    WebSocketClient.destroyInstance()
}
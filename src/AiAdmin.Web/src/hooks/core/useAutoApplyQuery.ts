/**
 * useAutoApplyQuery - 搜索栏定时应用查询
 *
 * 开启后固定当时的查询条件，每隔指定秒数重新应用一次，并用全局顶部的 ElMessage 展示倒计时，
 * 点击消息上的关闭按钮即可停止。组件卸载时自动停止并关闭提示。
 *
 * ## 行为
 *
 * 1. 开启时固定当前条件，之后用户再修改筛选控件不会影响定时应用的内容
 * 2. 浏览器标签页不可见时暂停倒计时，不应用，也不在后台持续请求
 * 3. 提示消息不会自动消失，停止、重新开启或组件卸载时关闭
 *
 * @module useAutoApplyQuery
 * @author AiAdmin Team
 */

import { h, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, type MessageHandler } from 'element-plus'

/** 倒计时步长（毫秒） */
const TICK_MS = 1000

/**
 * 创建定时应用查询控制器
 * @param apply 每次到点时执行的应用动作
 * @returns 当前间隔（秒，0 表示未开启）、开启与停止方法
 */
export function useAutoApplyQuery(apply: () => void) {
    const { t } = useI18n()
    /** 当前生效的间隔（秒），0 表示未开启 */
    const intervalSeconds = ref(0)
    /** 距离下一次应用的剩余秒数 */
    const remaining = ref(0)

    let timer: ReturnType<typeof setInterval> | undefined
    let handler: MessageHandler | undefined
    /** 由代码主动关闭消息时置位，用来区分用户点击关闭按钮 */
    let closingByCode = false

    /**
     * 提示内容：渲染函数读取响应式的剩余秒数，倒计时变化时文字自动更新，不需要重建消息
     * @returns 例如 “30 秒后自动应用查询”
     */
    const renderText = () => h('span', t('table.searchBar.autoApply.countdown', { seconds: remaining.value }))

    /** 关闭提示消息 */
    function closeMessage() {
        if (!handler) return
        closingByCode = true
        handler.close()
        handler = undefined
        closingByCode = false
    }

    /** 停止定时应用并关闭提示 */
    function stop() {
        if (timer) clearInterval(timer)
        timer = undefined
        intervalSeconds.value = 0
        remaining.value = 0
        closeMessage()
    }

    /** 每秒执行一次：页面可见时递减，归零则应用并重新计时 */
    function tick() {
        if (document.visibilityState !== 'visible') return
        remaining.value -= 1
        if (remaining.value <= 0) {
            remaining.value = intervalSeconds.value
            apply()
        }
    }

    /**
     * 开启定时应用；已开启时先停止再按新间隔重新计时
     * @param seconds 应用间隔（秒）
     */
    function start(seconds: number) {
        stop()
        if (seconds <= 0) return
        intervalSeconds.value = seconds
        remaining.value = seconds
        handler = ElMessage({
            message: renderText,
            type: 'info',
            duration: 0,
            showClose: true,
            grouping: false,
            onClose: () => {
                handler = undefined
                if (!closingByCode) stop()
            },
        })
        timer = setInterval(tick, TICK_MS)
    }

    onBeforeUnmount(stop)

    return { intervalSeconds, start, stop }
}
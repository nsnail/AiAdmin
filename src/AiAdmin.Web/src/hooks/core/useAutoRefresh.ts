/**
 * useAutoRefresh - 页面定时刷新
 *
 * 当前激活标签页启用定时刷新后，每秒递减倒计时，并用全局顶部的 ElMessage 展示剩余秒数，
 * 倒计时归零时刷新当前页面并重新开始计时。点击消息上的关闭按钮即可停止定时刷新。
 *
 * ## 行为
 *
 * 1. 只处理当前激活标签页的配置，切换标签页或修改间隔时重新开始倒计时
 * 2. 浏览器标签页不可见时暂停倒计时，不刷新，也不在后台持续请求
 * 3. 提示消息不会自动消失，页面卸载、停止刷新或切到未启用的标签页时关闭
 *
 * @module useAutoRefresh
 * @author AiAdmin Team
 */

import { computed, h, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElMessage, type MessageHandler } from 'element-plus'
import { AUTO_REFRESH_MIN_SECONDS, useWorktabStore } from '@/store/modules/worktab'
import { useCommon } from '@/hooks/core/useCommon'

/** 倒计时步长（毫秒） */
const TICK_MS = 1000

/**
 * 启用当前标签页的定时刷新倒计时与提示
 * @param activePath 当前激活标签页的路径
 * @param enabled 是否允许显示，全屏页面等场景传 false
 */
export function useAutoRefresh(activePath: Ref<string>, enabled: Ref<boolean>) {
    const { t } = useI18n()
    const worktabStore = useWorktabStore()
    const { refresh } = useCommon()

    /** 当前标签页的刷新间隔（秒），0 表示未启用；本地已保存的小于下限的值按下限执行 */
    const intervalSeconds = computed(() => {
        const seconds = enabled.value ? (worktabStore.getTab(activePath.value)?.autoRefreshSeconds ?? 0) : 0
        return seconds > 0 ? Math.max(AUTO_REFRESH_MIN_SECONDS, seconds) : 0
    })
    /** 距离下一次刷新的剩余秒数 */
    const remaining = ref(0)

    let timer: ReturnType<typeof setInterval> | undefined
    let handler: MessageHandler | undefined
    /** 由代码主动关闭消息时置位，用来区分用户点击关闭按钮 */
    let closingByCode = false

    /**
     * 提示内容：渲染函数读取响应式的剩余秒数，倒计时变化时消息文字自动更新，不需要重建消息
     * @returns 例如 “30 秒后自动刷新页面”
     */
    const renderText = () => h('span', t('worktab.autoRefresh.countdown', { seconds: remaining.value }))

    /** 关闭提示消息 */
    function closeMessage() {
        if (!handler) return
        closingByCode = true
        handler.close()
        handler = undefined
        closingByCode = false
    }

    /** 创建提示消息，用户点击关闭按钮时停止当前标签页的定时刷新 */
    function openMessage() {
        const path = activePath.value
        handler = ElMessage({
            message: renderText,
            type: 'info',
            duration: 0,
            showClose: true,
            grouping: false,
            onClose: () => {
                handler = undefined
                if (!closingByCode) worktabStore.setAutoRefresh(path, 0)
            },
        })
    }

    /** 清除倒计时并关闭提示 */
    function stop() {
        if (timer) clearInterval(timer)
        timer = undefined
        closeMessage()
    }

    /** 每秒执行一次：页面可见时递减，归零则刷新并重新计时 */
    function tick() {
        if (document.visibilityState !== 'visible') return
        remaining.value -= 1
        if (remaining.value <= 0) {
            remaining.value = intervalSeconds.value
            refresh()
        }
    }

    /**
     * 按新的间隔重新开始倒计时
     * @param seconds 刷新间隔（秒），0 表示关闭
     */
    function restart(seconds: number) {
        stop()
        if (seconds <= 0) return
        remaining.value = seconds
        openMessage()
        timer = setInterval(tick, TICK_MS)
    }

    watch(intervalSeconds, restart, { immediate: true })
    onBeforeUnmount(stop)

    return { intervalSeconds, remaining }
}
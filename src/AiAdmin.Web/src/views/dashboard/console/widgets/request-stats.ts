/**
 * 操作日志统计的共享数据源：访问量趋势和接口占比两个卡片共用同一个接口，
 * 同一时间只发一次请求，卡片显示期间每隔一段时间自动刷新，卡片隐藏或页面离开后停止
 */
import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue'
import { fetchGetDashboardRequestStats, type DashboardRequestStats } from '@/api/dashboard'

/** 自动刷新间隔（毫秒） */
const REFRESH_INTERVAL = 60_000

const stats = ref<DashboardRequestStats | null>(null)
const loading = ref(false)
const failed = ref(false)
let inFlight: Promise<void> | null = null
let subscribers = 0
let timer: ReturnType<typeof setInterval> | undefined

/** 请求一次统计，已有请求在途时复用 */
function load(): Promise<void> {
    inFlight ??= (async () => {
        loading.value = true
        failed.value = false
        try {
            stats.value = await fetchGetDashboardRequestStats()
        } catch {
            failed.value = true
        } finally {
            loading.value = false
            inFlight = null
        }
    })()
    return inFlight
}

/**
 * 卡片使用操作日志统计：首个使用者挂载时开始加载并启动定时刷新，最后一个使用者卸载时停止
 * @returns 统计数据、加载状态和失败标记
 */
export function useRequestStats(): {
    stats: Ref<DashboardRequestStats | null>
    loading: Ref<boolean>
    failed: Ref<boolean>
    reload: () => Promise<void>
} {
    onMounted(() => {
        subscribers += 1
        if (subscribers === 1) {
            void load()
            timer = setInterval(() => void load(), REFRESH_INTERVAL)
        } else if (!stats.value && !inFlight) {
            void load()
        }
    })
    onBeforeUnmount(() => {
        subscribers -= 1
        if (subscribers <= 0) {
            subscribers = 0
            if (timer) clearInterval(timer)
            timer = undefined
        }
    })
    return { stats, loading, failed, reload: load }
}
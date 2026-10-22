/**
 * 仪表盘数据上下文：页面请求一次总览数据，各卡片组件通过 inject 取用，卡片之间互不依赖
 */
import { inject, type InjectionKey, type Ref } from 'vue'
import type { DashboardOverview } from '@/api/dashboard'

export interface DashboardContext {
    /** 总览数据，首次加载完成前为空 */
    overview: Ref<DashboardOverview | null>
    /** 是否正在加载 */
    loading: Ref<boolean>
    /** 统计天数 */
    days: Ref<number>
}

export const dashboardContextKey: InjectionKey<DashboardContext> = Symbol('dashboard-context')

/**
 * 卡片组件获取仪表盘数据上下文
 * @returns 数据上下文
 */
export function useDashboardContext(): DashboardContext {
    const context = inject(dashboardContextKey)
    if (!context) {
        throw new Error('Dashboard widget must be used inside the dashboard page')
    }
    return context
}
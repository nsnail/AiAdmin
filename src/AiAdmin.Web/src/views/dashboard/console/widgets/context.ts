/**
 * 仪表盘数据上下文：页面请求一次总览数据，各卡片组件通过 inject 取用，卡片之间互不依赖
 */
import { inject, type InjectionKey, type Ref } from 'vue'
import { useRouter } from 'vue-router'
import type { DashboardOverview } from '@/api/dashboard'

export interface DashboardContext {
    /** 总览数据，首次加载完成前为空 */
    overview: Ref<DashboardOverview | null>
    /** 是否正在加载 */
    loading: Ref<boolean>
    /** 统计天数 */
    days: Ref<number>
    /** 是否处于布局编辑模式，编辑模式下卡片不响应点击跳转 */
    editing: Ref<boolean>
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

/**
 * 卡片点击联动：跳转到相关业务页面，编辑布局时不跳转，当前用户没有该页面权限（路由未注册）时不可点击
 * @param target 目标页面路径的获取函数，返回空表示不联动
 * @returns 是否可跳转与跳转方法，跳转方法可附带路由查询参数，由目标页面据此预设筛选
 */
export function useWidgetLink(target: () => string | undefined) {
    const router = useRouter()
    const { editing } = useDashboardContext()
    /** 目标路由已注册且不是兜底的 404 路由时才可跳转 */
    const linkable = (): boolean => {
        const path = target()
        return !!path && !editing.value && router.resolve(path).name !== 'Exception404'
    }
    /** 跳转到目标页面 */
    const go = (query?: Record<string, string>) => {
        const path = target()
        if (path && linkable()) {
            void router.push({ path, query })
        }
    }
    return { linkable, go }
}
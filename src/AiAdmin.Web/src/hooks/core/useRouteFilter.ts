/**
 * useRouteFilter - 从路由参数套用列表筛选
 *
 * 其他页面通过链接跳转到列表页时（如点击消息列表里的账号编号），把条件放在路由 query 里，列表页据此只查询目标记录。
 *
 * - 首次进入：初始筛选（initialFilter）直接取路由参数，随后清掉已消费的参数，避免刷新页面时重复套用；
 * - 页面带缓存（keepAlive）再次进入：页面不会重新创建，需要在激活时重新套用筛选并查询。
 *
 * 路由参数无效或嵌入其他容器时不生效，由调用方通过 enabled 控制。
 *
 * @module useRouteFilter
 */
import { onActivated } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { DynamicFilter } from '@/api/system-manage'

/** 页面需要提供的能力：把筛选同步到搜索栏、写入唯一筛选状态并重新查询 */
export interface RouteFilterOptions {
    /** 路由参数名，如 accountId、deviceId */
    queryKey: string
    /** 路由参数值转换为筛选条件，值无效时返回空 */
    toFilter: (value: string) => DynamicFilter | undefined
    /** 是否启用，嵌入其他容器时应返回 false */
    enabled?: () => boolean
    /** 把筛选同步到搜索栏（调用 ArtTablePage 暴露的 setFilter） */
    setFilter: (filter: DynamicFilter) => void
    /** 写入唯一筛选状态并重新查询 */
    apply: (filter: DynamicFilter) => Promise<void>
}

/**
 * 读取并套用路由参数里的筛选
 * @param options 路由参数名、转换方式和页面能力
 * @returns 首次加载应使用的初始筛选，没有有效参数时为空
 */
export const useRouteFilter = (options: RouteFilterOptions): { initialFilter: DynamicFilter | undefined } => {
    const route = useRoute()
    const router = useRouter()
    const isEnabled = (): boolean => options.enabled?.() ?? true

    /** 读取当前路由参数对应的筛选 */
    const read = (): DynamicFilter | undefined => {
        if (!isEnabled()) return undefined
        const value = route.query[options.queryKey]
        return typeof value === 'string' ? options.toFilter(value) : undefined
    }

    /** 清掉已消费的路由参数 */
    const clearQuery = (): Promise<unknown> => router.replace({ path: route.path, query: {} })

    const initialFilter = read()
    let firstActivated = true

    onActivated(async () => {
        if (!isEnabled()) return
        const filter = read()
        if (firstActivated) {
            // 首次进入的参数已作为初始筛选套用，这里只负责清掉
            firstActivated = false
            if (filter) await clearQuery()
            return
        }
        if (!filter) return
        await clearQuery()
        options.setFilter(filter)
        await options.apply(filter)
    })

    return { initialFilter }
}
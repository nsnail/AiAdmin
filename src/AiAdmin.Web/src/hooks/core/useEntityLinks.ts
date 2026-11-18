/**
 * useEntityLinks - 列表中指向其他管理页的跳转（框架部分）
 *
 * 只包含框架自带管理页（用户）的跳转与通用的路由位置生成函数，业务管理页的跳转见 hooks/business/useBusinessEntityLinks。
 * 条件通过路由参数传递，由目标页的 useRouteFilter 读取
 *
 * @module useEntityLinks
 */
import { useRouter } from 'vue-router'

/** 跳转目标：目标页完整路径与携带筛选条件的路由参数名 */
export interface EntityLinkTarget {
    path: string
    queryKey: string
}

/** 框架跳转目标。路径必须与 router/modules 里的路由一致，tests/entity-links.test.ts 会逐个核对 */
export const ENTITY_LINK_TARGETS = {
    user: { path: '/permission/user', queryKey: 'userId' },
} as const satisfies Record<string, EntityLinkTarget>

export type EntityLinkType = keyof typeof ENTITY_LINK_TARGETS

/**
 * 生成跳转到目标管理页的路由位置
 * @param target 跳转目标，框架与业务各自维护自己的目标表
 * @param id 记录编号，以字符串传递避免 JS 数值精度丢失
 * @returns 路由位置，路径为目标页完整路径，筛选条件放在路由参数里
 */
export const buildEntityLocation = (target: EntityLinkTarget, id: string): { path: string; query: Record<string, string> } => ({
    path: target.path,
    query: { [target.queryKey]: id },
})

export const useEntityLinks = () => {
    const router = useRouter()

    return {
        /**
         * 转到用户管理页并只筛选该用户
         * @param userId 用户编号，以字符串传递避免 JS 数值精度丢失
         */
        openUser: (userId: string): Promise<unknown> => router.push(buildEntityLocation(ENTITY_LINK_TARGETS.user, userId)),
    }
}
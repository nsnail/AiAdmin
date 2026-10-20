/**
 * useRouteAuth - 基于当前路由菜单权限的按钮权限判断
 *
 * 读取当前路由 meta.authList（后端按角色过滤后下发的按钮权限），与 v-auth 指令同源，不依赖访问模式。
 * 页面里需要用 v-if 控制按钮显示、或在表格列 formatter 中按权限渲染时使用。
 *
 * 与 v-auth 的区别：v-auth 在元素挂载后才把它从 DOM 中移除，ElSpace 等布局容器会残留间距外壳，
 * 按钮栏出现空白或间距不一致；v-if 在渲染阶段就不生成元素，布局不受影响。
 *
 * 与 useAuth 的区别：useAuth 在 frontend 访问模式下读取用户信息里的 buttons，后端不返回该字段，
 * 会使所有按钮被判定为无权限。
 *
 * ## 使用示例
 *
 * ```vue
 * <ElButton v-if="hasAuth('delete')">删除</ElButton>
 * ```
 *
 * ```typescript
 * const { hasAuth } = useRouteAuth()
 * formatter: (row) => h('div', [hasAuth('delete') ? h(ArtButtonTable, { type: 'delete' }) : null])
 * ```
 *
 * @module useRouteAuth
 */

import { useRoute } from 'vue-router'

/** 菜单元数据中的按钮权限项 */
interface RouteAuthItem {
    authMark: string
}

export const useRouteAuth = () => {
    const route = useRoute()

    /**
     * 判断当前路由是否拥有某个按钮权限，每次调用都读取最新的路由元数据
     * @param authMark 权限标识
     * @returns 拥有该权限时为 true
     */
    const hasAuth = (authMark: string): boolean => {
        const authList = (route.meta.authList as RouteAuthItem[] | undefined) ?? []
        return authList.some((item) => item?.authMark === authMark)
    }

    return { hasAuth }
}
/**
 * 快速入口工具模块
 *
 * 提供按菜单权限过滤快速入口条目的纯函数，不依赖 Vue 和状态库，便于单独测试
 */
import type { AppRouteRecord } from '@/types/router'
import type { FastEnterBaseItem } from '@/types/config'
import { existsInMenu } from './route'

/**
 * 判断快速入口条目是否对当前账号可见：声明了 menuPath 的必须存在于当前账号的菜单树中，没声明的（如打开通知抽屉）始终可见
 * @param item 快速入口条目
 * @param menuList 当前账号可访问的菜单树
 * @returns 可见时为 true
 */
export const isFastEnterVisible = (item: FastEnterBaseItem, menuList: AppRouteRecord[]): boolean =>
    !item.menuPath || existsInMenu(item.menuPath, menuList)
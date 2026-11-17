/**
 * 左侧菜单个性化设置：隐藏、上移、下移，仅保存在本地存储
 */
import type { AppRouteRecord } from '@/types/router'
import { useMenuStore } from '@/store/modules/menu'

const STORAGE_KEY = 'sidebar-menu-customize'

interface MenuCustomizeState {
    /** 被隐藏的菜单键 */
    hidden: string[]
    /** 各父级下的子菜单排序，键为父级菜单键（根为空字符串） */
    order: Record<string, string[]>
}

/**
 * 读取本地存储中的设置
 * @returns 设置状态
 */
const loadState = (): MenuCustomizeState => {
    try {
        const raw = localStorage.getItem(STORAGE_KEY)
        if (raw) {
            const parsed = JSON.parse(raw)
            return {
                hidden: Array.isArray(parsed.hidden) ? parsed.hidden : [],
                order: parsed.order && typeof parsed.order === 'object' ? parsed.order : {},
            }
        }
    } catch {
        // 本地存储不可用时使用默认值
    }
    return { hidden: [], order: {} }
}

const state = ref<MenuCustomizeState>(loadState())

/**
 * 保存设置到本地存储
 */
const persist = (): void => {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(state.value))
    } catch {
        // 忽略写入失败
    }
}

/**
 * 获取菜单项的唯一键
 * @param item 菜单项
 * @returns 菜单键
 */
export const getMenuKey = (item: AppRouteRecord): string => item.path || item.meta?.title || ''

/**
 * 按已保存的排序对同级菜单排序，未记录的项保持原相对顺序并排在后面
 * @param items 同级菜单
 * @param parentKey 父级菜单键
 * @returns 排序后的菜单
 */
const sortSiblings = (items: AppRouteRecord[], parentKey: string): AppRouteRecord[] => {
    const saved = state.value.order[parentKey]
    if (!saved?.length) return items
    const rank = (item: AppRouteRecord): number => {
        const idx = saved.indexOf(getMenuKey(item))
        return idx === -1 ? saved.length : idx
    }
    return items
        .map((item, index) => ({ item, index }))
        .sort((a, b) => rank(a.item) - rank(b.item) || a.index - b.index)
        .map((x) => x.item)
}

/**
 * 菜单个性化组合式函数
 * @returns 应用、操作方法以及是否存在自定义设置
 */
export const useMenuCustomize = () => {
    const hasCustomize = computed(() => state.value.hidden.length > 0 || Object.keys(state.value.order).length > 0)

    /**
     * 递归应用隐藏与排序
     * @param items 菜单列表
     * @param parentKey 父级菜单键
     * @returns 处理后的菜单列表
     */
    const applyCustomize = (items: AppRouteRecord[], parentKey = ''): AppRouteRecord[] => {
        return sortSiblings(items, parentKey)
            .filter((item) => !state.value.hidden.includes(getMenuKey(item)))
            .map((item) => (item.children?.length ? { ...item, children: applyCustomize(item.children, getMenuKey(item)) } : item))
    }

    /**
     * 查找菜单项所在的同级列表
     * @param items 菜单列表
     * @param key 菜单键
     * @param parentKey 父级菜单键
     * @returns 同级列表与父级键
     */
    const findSiblings = (items: AppRouteRecord[], key: string, parentKey = ''): { siblings: AppRouteRecord[]; parentKey: string } | undefined => {
        if (items.some((i) => getMenuKey(i) === key)) return { siblings: items, parentKey }
        for (const item of items) {
            if (item.children?.length) {
                const found = findSiblings(item.children, key, getMenuKey(item))
                if (found) return found
            }
        }
        return undefined
    }

    /**
     * 隐藏菜单项
     * @param key 菜单键
     */
    const hideMenu = (key: string): void => {
        if (!canHide(useMenuStore().menuList, key)) return
        if (!state.value.hidden.includes(key)) state.value.hidden.push(key)
        persist()
    }

    /**
     * 移动菜单项
     * @param allMenus 完整菜单列表（未应用设置）
     * @param key 菜单键
     * @param offset -1 上移，1 下移
     */
    const moveMenu = (allMenus: AppRouteRecord[], key: string, offset: -1 | 1): void => {
        const found = findSiblings(allMenus, key)
        if (!found) return
        const keys = sortSiblings(found.siblings, found.parentKey)
            .filter((i) => !state.value.hidden.includes(getMenuKey(i)) || getMenuKey(i) === key)
            .map(getMenuKey)
        const from = keys.indexOf(key)
        const to = from + offset
        if (from === -1 || to < 0 || to >= keys.length) return
        ;[keys[from], keys[to]] = [keys[to], keys[from]]
        // 隐藏项追加在末尾，保证其排序不丢失
        const hiddenKeys = found.siblings.map(getMenuKey).filter((k) => !keys.includes(k))
        state.value.order[found.parentKey] = [...keys, ...hiddenKeys]
        persist()
    }

    /**
     * 判断菜单项能否移动
     * @param allMenus 完整菜单列表
     * @param key 菜单键
     * @param offset -1 上移，1 下移
     * @returns 是否可移动
     */
    const canMove = (allMenus: AppRouteRecord[], key: string, offset: -1 | 1): boolean => {
        const found = findSiblings(allMenus, key)
        if (!found) return false
        const keys = sortSiblings(found.siblings, found.parentKey)
            .filter((i) => !state.value.hidden.includes(getMenuKey(i)))
            .map(getMenuKey)
        const to = keys.indexOf(key) + offset
        return to >= 0 && to < keys.length
    }

    /**
     * 判断菜单项能否隐藏：同级只剩最后一个可见菜单项时不允许隐藏
     * @param allMenus 完整菜单列表
     * @param key 菜单键
     * @returns 是否可隐藏
     */
    const canHide = (allMenus: AppRouteRecord[], key: string): boolean => {
        const found = findSiblings(allMenus, key)
        if (!found) return false
        const visible = found.siblings.filter((i) => !i.meta?.isHide && !state.value.hidden.includes(getMenuKey(i)))
        return visible.length > 1
    }

    /**
     * 恢复默认菜单
     */
    const resetCustomize = (): void => {
        state.value = { hidden: [], order: {} }
        persist()
    }

    return { hasCustomize, applyCustomize, hideMenu, moveMenu, canMove, canHide, resetCustomize }
}
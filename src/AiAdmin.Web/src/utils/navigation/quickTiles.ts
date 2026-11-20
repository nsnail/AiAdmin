/**
 * 常用功能磁贴工具模块
 *
 * 把当前账号可访问的菜单树展平成可选页面，并按可访问菜单清洗用户保存的磁贴；
 * 不依赖 Vue、i18n 和状态库，便于单独测试
 */
import type { AppRouteRecord } from '@/types/router'
import { isNavigableMenuItem } from './route'

/** 可添加为磁贴的页面 */
export interface TileCandidate {
    /** 完整页面路径，同时作为磁贴的唯一标识 */
    path: string
    /** 菜单标题，可能是 menus.* 形式的 i18n 键，由调用方负责翻译 */
    title: string
    /** 菜单图标 */
    icon: string
    /** 所属目录标题，用于在选择列表中分组 */
    groupTitle: string
}

/** 磁贴最多保存的个数，避免无限增长 */
export const MAX_QUICK_TILES = 24

/**
 * 拼接子菜单的完整路径，规则与菜单路径规范化一致
 * @param path 菜单路径
 * @param parent 父级完整路径
 * @returns 完整页面路径
 */
const joinPath = (path: string, parent: string): string => (path.startsWith('/') ? path : `${parent}/${path}`.replace(/\/{2,}/g, '/'))

/**
 * 判断菜单项是否可以作为磁贴：必须是可导航的内部页面，外链和 iframe 不支持直达
 * @param item 菜单项
 * @returns 可作为磁贴时为 true
 */
const isTileTarget = (item: AppRouteRecord): boolean =>
    isNavigableMenuItem(item) && !item.meta?.link && item.meta?.isIframe !== true && !!item.component && !item.children?.length

/**
 * 把菜单树展平成可添加的磁贴候选，隐藏菜单（如个人中心）和目录不会出现
 * @param menuList 当前账号可访问的菜单树
 * @param parentPath 父级完整路径
 * @param groupTitle 父级目录标题
 * @returns 候选页面列表，保持菜单顺序
 */
export const collectTileCandidates = (menuList: AppRouteRecord[], parentPath = '', groupTitle = ''): TileCandidate[] =>
    menuList.flatMap((item) => {
        const path = joinPath(item.path ?? '', parentPath)
        const title = String(item.meta?.title ?? '')
        if (item.children?.length) {
            return collectTileCandidates(item.children, path, title)
        }
        return isTileTarget(item) ? [{ path, title, icon: String(item.meta?.icon ?? ''), groupTitle }] : []
    })

/**
 * 按当前可访问的页面清洗已保存的磁贴：去掉重复项和已无权限访问的页面，并限制总数，保持保存顺序
 * @param saved 已保存的页面路径
 * @param candidates 当前可添加的候选页面
 * @returns 仍然有效的页面路径
 */
export const sanitizeTiles = (saved: unknown, candidates: TileCandidate[]): string[] => {
    if (!Array.isArray(saved)) return []
    const allowed = new Set(candidates.map((x) => x.path))
    const result: string[] = []
    for (const path of saved) {
        if (typeof path === 'string' && allowed.has(path) && !result.includes(path)) result.push(path)
        if (result.length >= MAX_QUICK_TILES) break
    }
    return result
}
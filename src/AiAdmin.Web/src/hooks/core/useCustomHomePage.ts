/**
 * 自定义首页：用户把某个页面设为首页，仅保存在本地存储，按用户隔离
 *
 * 点击 Logo、登录后默认跳转、根路径重定向、关闭全部标签后回首页等位置读取首页路径时，
 * 菜单 store 会优先返回这里保存的页面；该页面不在当前用户的可访问菜单中时自动回退到默认首页
 */
import { ref } from 'vue'

const STORAGE_KEY_PREFIX = 'custom-home-page'

/** 各用户的自定义首页，键为用户标识 */
const homePages = ref<Record<string, string>>({})
/** 已从本地存储载入过的用户，避免重复读取 */
const loadedUsers = new Set<string>()

/**
 * 生成存储键
 * @param userKey 用户标识
 * @returns 本地存储键
 */
const storageKeyOf = (userKey: string): string => `${STORAGE_KEY_PREFIX}:${userKey}`

/**
 * 确保指定用户的自定义首页已从本地存储载入，存储不可用时视为未设置
 * @param userKey 用户标识
 */
const ensureLoaded = (userKey: string): void => {
    if (loadedUsers.has(userKey)) return
    loadedUsers.add(userKey)
    try {
        const raw = localStorage.getItem(storageKeyOf(userKey))
        if (raw) homePages.value = { ...homePages.value, [userKey]: raw }
    } catch {
        // 隐私模式或存储被禁用时仅本次会话生效
    }
}

/**
 * 读取指定用户的自定义首页
 * @param userKey 用户标识
 * @returns 自定义首页路径，未设置时为空字符串
 */
export const getCustomHomePage = (userKey: string): string => {
    ensureLoaded(userKey)
    return homePages.value[userKey] ?? ''
}

/**
 * 设置或清除指定用户的自定义首页
 * @param userKey 用户标识
 * @param path 首页路径，传空字符串表示清除
 */
export const setCustomHomePage = (userKey: string, path: string): void => {
    ensureLoaded(userKey)
    const next = { ...homePages.value }
    if (path) next[userKey] = path
    else delete next[userKey]
    homePages.value = next
    try {
        if (path) localStorage.setItem(storageKeyOf(userKey), path)
        else localStorage.removeItem(storageKeyOf(userKey))
    } catch {
        // 忽略写入失败
    }
}
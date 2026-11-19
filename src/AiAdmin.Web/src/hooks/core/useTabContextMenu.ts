/**
 * 标签页右键菜单的共享入口：菜单由标签页组件持有，内容区等其他区域通过这里打开同一个菜单，保证菜单项与行为一致
 */

/** 打开当前标签页右键菜单的函数，由标签页组件注册 */
let opener: ((e: MouseEvent) => void) | null = null

/**
 * 注册打开标签页右键菜单的函数
 * @param fn 接收右键事件并在事件位置弹出菜单的函数
 * @returns 取消注册的函数，仅在当前注册的仍是 fn 时才会清除
 */
export const registerTabMenuOpener = (fn: (e: MouseEvent) => void): (() => void) => {
    opener = fn
    return () => {
        if (opener === fn) opener = null
    }
}

/**
 * 在事件位置弹出当前标签页的右键菜单
 * @param e 右键事件
 * @returns 菜单已弹出返回 true，标签页组件尚未注册时返回 false，调用方应保留浏览器默认行为
 */
export const openActiveTabMenu = (e: MouseEvent): boolean => {
    if (!opener) return false
    opener(e)
    return true
}
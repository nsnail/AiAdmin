/**
 * 快捷键工具
 *
 * 快捷键以文本保存，格式为 "修饰键+主键"，如 "Ctrl+Q"、"Ctrl+Shift+W"、"Alt+1"。
 * 修饰键固定顺序为 Ctrl、Alt、Shift、Meta，主键为单个字母、数字或功能键名称；空字符串表示未配置。
 * 全部是不依赖 Vue 与 DOM 实例的纯函数，便于单元测试
 */

/** 键盘事件中判断组合键所需的最小结构 */
export interface ShortcutKeyEvent {
    key: string
    ctrlKey: boolean
    altKey: boolean
    shiftKey: boolean
    metaKey: boolean
    isComposing?: boolean
    repeat?: boolean
}

/** 仅修饰键本身被按下时 key 的取值，录入时需要忽略 */
const MODIFIER_KEYS = new Set(['Control', 'Alt', 'Shift', 'Meta', 'OS'])

/** 主键名称规范化：单个字符统一为大写，其余按首字母大写保留（如 Escape、F5） */
const normalizeMainKey = (key: string): string => {
    if (key === ' ') return 'Space'
    if (key.length === 1) return key.toUpperCase()
    return key.charAt(0).toUpperCase() + key.slice(1)
}

/**
 * 把键盘事件转换成快捷键文本
 * @param event 键盘事件
 * @returns 快捷键文本；只按下修饰键、输入法组合中或没有任何修饰键（Ctrl、Alt、Meta）时返回空字符串
 */
export const eventToShortcut = (event: ShortcutKeyEvent): string => {
    if (event.isComposing || MODIFIER_KEYS.has(event.key)) return ''

    // 必须带 Ctrl、Alt 或 Meta 之一：只有 Shift 或不带修饰键的快捷键会吞掉输入框里正常的字符输入
    if (!event.ctrlKey && !event.altKey && !event.metaKey) return ''
    const parts: string[] = []
    if (event.ctrlKey) parts.push('Ctrl')
    if (event.altKey) parts.push('Alt')
    if (event.shiftKey) parts.push('Shift')
    if (event.metaKey) parts.push('Meta')
    parts.push(normalizeMainKey(event.key))
    return parts.join('+')
}

/**
 * 判断键盘事件是否命中指定快捷键
 * @param event 键盘事件
 * @param shortcut 快捷键文本，空字符串表示未配置，永远不命中
 * @returns 命中返回 true
 */
export const matchShortcut = (event: ShortcutKeyEvent, shortcut: string): boolean => {
    if (!shortcut || event.repeat) return false
    return eventToShortcut(event) === shortcut
}

/**
 * 找出与其他快捷键重复的项，用于配置时阻止冲突
 * @param shortcuts 键为功能标识、值为快捷键文本的映射
 * @param key 正在设置的功能标识
 * @param value 正在设置的快捷键文本
 * @returns 已占用该快捷键的另一个功能标识；没有冲突或快捷键为空时返回 undefined
 */
export const findShortcutConflict = (shortcuts: Record<string, string>, key: string, value: string): string | undefined => {
    if (!value) return undefined
    return Object.keys(shortcuts).find((other) => other !== key && shortcuts[other] === value)
}

/**
 * 把快捷键文本拆成用于展示的按键列表
 * @param shortcut 快捷键文本
 * @returns 按键列表，如 ['Ctrl', 'Q']；空字符串返回空数组
 */
export const splitShortcut = (shortcut: string): string[] => (shortcut ? shortcut.split('+') : [])
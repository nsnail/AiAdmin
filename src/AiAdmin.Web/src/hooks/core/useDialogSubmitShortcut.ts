/**
 * useDialogSubmitShortcut - 抽屉/弹窗 Ctrl+Enter 快捷提交
 *
 * 在全局监听 Ctrl+Enter（macOS 为 Cmd+Enter），当存在已打开的 ElDrawer / ElDialog 时，
 * 点击最上层面板底部的主要按钮（保存、确定等）。按钮处于禁用或加载中时不会触发，避免重复提交。
 * 仅在应用根组件调用一次即可。
 *
 * @module useDialogSubmitShortcut
 */

/** 最上层面板的选择器：抽屉与对话框；MessageBox 自带回车处理，不在此列 */
const PANEL_SELECTOR = '.el-drawer, .el-dialog'

/** 底部区域选择器 */
const FOOTER_SELECTOR = '.el-drawer__footer, .el-dialog__footer'

/**
 * 判断元素当前是否可见
 * @param el 目标元素
 * @returns 可见时为 true
 */
const isVisible = (el: HTMLElement): boolean => el.getClientRects().length > 0

/**
 * 找到最上层的可见抽屉或对话框
 * @returns 最上层面板，没有时为 undefined
 */
const findTopPanel = (): HTMLElement | undefined => {
    const panels = Array.from(document.querySelectorAll<HTMLElement>(PANEL_SELECTOR)).filter(isVisible)
    // 后打开的面板挂载在 DOM 更靠后的位置，同层级取最后一个
    return panels[panels.length - 1]
}

/**
 * 在面板底部查找可点击的主要按钮
 * @param panel 抽屉或对话框元素
 * @returns 主要按钮，没有或不可用时为 undefined
 */
const findSubmitButton = (panel: HTMLElement): HTMLButtonElement | undefined => {
    const footer = panel.querySelector<HTMLElement>(FOOTER_SELECTOR)
    if (!footer) return undefined
    const buttons = Array.from(footer.querySelectorAll<HTMLButtonElement>('button.el-button--primary')).filter(isVisible)
    const button = buttons[buttons.length - 1]
    if (!button || button.disabled || button.classList.contains('is-loading')) return undefined
    return button
}

/**
 * 键盘事件处理：Ctrl/Cmd + Enter 触发最上层面板的保存按钮
 * @param event 键盘事件
 */
const handleKeydown = (event: KeyboardEvent): void => {
    if (event.key !== 'Enter' || !(event.ctrlKey || event.metaKey) || event.isComposing) return
    const panel = findTopPanel()
    if (!panel) return
    // 事件来源不在最上层面板内（如 MessageBox、下拉层）时不处理
    const target = event.target as HTMLElement | null
    if (target && target !== document.body && !panel.contains(target) && !target.closest('.el-overlay')) return
    const button = findSubmitButton(panel)
    if (!button) return
    event.preventDefault()
    // 先让当前输入框失焦，保证 v-model.lazy 等绑定先写回再提交
    ;(document.activeElement as HTMLElement | null)?.blur?.()
    button.click()
}

export const useDialogSubmitShortcut = () => {
    onMounted(() => window.addEventListener('keydown', handleKeydown))
    onBeforeUnmount(() => window.removeEventListener('keydown', handleKeydown))
}
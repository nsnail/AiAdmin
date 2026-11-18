/**
 * 表格双击行打开详情的判断逻辑
 *
 * 约定：双击表格行时执行行操作中标记了 dblclick 的操作；没有任何操作标记时，回退到 key 为 view 的“打开详情”操作。
 * 纯函数，不依赖 Vue 与 DOM 实例，便于单元测试
 */

/** 表格行操作的最小结构，只取判断所需字段 */
export interface DblclickAction {
    key: string
    disabled?: boolean
    dblclick?: boolean
}

/** 双击打开详情所用的行操作 key */
export const VIEW_ACTION_KEY = 'view'

/** 点击这些元素时视为在操作控件，不触发双击打开详情 */
const INTERACTIVE_SELECTOR = [
    'button',
    'a',
    'input',
    'textarea',
    'select',
    'label',
    '.el-switch',
    '.el-checkbox',
    '.el-radio',
    '.el-button',
    '.el-link',
    '.el-input',
    '.el-select',
    '.el-tag.is-closable',
    '.el-dropdown',
    '.el-popover',
    '[data-no-row-dblclick]',
].join(',')

/** 能查找最近祖先的目标元素最小结构，用于在不引入 DOM 的情况下测试 */
export interface ClosestTarget {
    closest: (selector: string) => unknown
}

/**
 * 判断双击事件的目标是否落在交互控件上
 * @param target 事件目标，无法确定时视为不在控件上
 * @returns 落在按钮、开关、链接、输入框等控件上返回 true
 */
export const isInteractiveTarget = (target: ClosestTarget | null | undefined): boolean => {
    return Boolean(target?.closest?.(INTERACTIVE_SELECTOR))
}

/**
 * 找出双击应执行的行操作：优先取标记了 dblclick 的第一个操作，其次取 key 为 view 的操作
 * @param actions 当前行可用的行操作
 * @returns 可执行的操作；没有可用操作，或选中的操作已禁用时返回 undefined（不会退而执行其他操作）
 */
export const findDblclickAction = <T extends DblclickAction>(actions: readonly T[] | undefined): T | undefined => {
    const action = actions?.find((item) => item.dblclick) ?? actions?.find((item) => item.key === VIEW_ACTION_KEY)
    return action && !action.disabled ? action : undefined
}
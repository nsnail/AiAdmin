import assert from 'node:assert/strict'
import test from 'node:test'

import { findDblclickAction, isInteractiveTarget, VIEW_ACTION_KEY } from '../src/utils/table/rowDblclick'

test('双击使用 key 为 view 的行操作', () => {
    const actions = [
        { key: 'edit', label: '编辑' },
        { key: 'view', label: '详情' },
        { key: 'delete', label: '删除' },
    ]
    assert.equal(VIEW_ACTION_KEY, 'view')
    assert.equal(findDblclickAction(actions)?.label, '详情')
})

test('没有详情操作的表格双击不执行任何操作', () => {
    assert.equal(findDblclickAction([{ key: 'edit' }, { key: 'delete' }]), undefined)
    assert.equal(findDblclickAction([]), undefined)
    assert.equal(findDblclickAction(undefined), undefined)
})

test('详情操作被禁用时双击不执行', () => {
    assert.equal(findDblclickAction([{ key: 'view', disabled: true }]), undefined)
    assert.deepEqual(findDblclickAction([{ key: 'view', disabled: false }]), { key: 'view', disabled: false })
})

test('不会把 edit 或 delete 当成详情操作执行', () => {
    assert.equal(findDblclickAction([{ key: 'edit' }]), undefined)
    assert.equal(findDblclickAction([{ key: 'delete' }]), undefined)
})

test('标记了 dblclick 的操作优先于 view', () => {
    const actions = [{ key: 'run' }, { key: 'executions', dblclick: true }, { key: 'view' }]
    assert.equal(findDblclickAction(actions)?.key, 'executions')
})

test('没有 view 但标记了 dblclick 时执行标记的操作（计划作业打开执行记录）', () => {
    const actions = [{ key: 'run' }, { key: 'executions', dblclick: true }, { key: 'edit' }, { key: 'delete' }]
    assert.equal(findDblclickAction(actions)?.key, 'executions')
})

test('多个操作都标记时取第一个', () => {
    assert.equal(
        findDblclickAction([
            { key: 'a', dblclick: true },
            { key: 'b', dblclick: true },
        ])?.key,
        'a',
    )
})

test('标记的操作被禁用时不执行，也不退而执行 view', () => {
    const actions = [{ key: 'executions', dblclick: true, disabled: true }, { key: 'view' }]
    assert.equal(findDblclickAction(actions), undefined)
})

test('落在按钮、开关、链接等控件上的双击不触发', () => {
    // 记录实际传给 closest 的选择器，逐个确认关键控件都在其中
    let used = ''
    const target = {
        closest: (selector: string) => {
            used = selector
            return {}
        },
    }
    assert.equal(isInteractiveTarget(target), true)
    const selectors = used.split(',')
    for (const expected of ['button', 'a', 'input', '.el-switch', '.el-checkbox', '.el-button', '.el-link', '[data-no-row-dblclick]']) {
        assert.ok(selectors.includes(expected), `缺少选择器 ${expected}`)
    }
})

test('落在普通单元格文字上的双击会触发', () => {
    assert.equal(isInteractiveTarget({ closest: () => null }), false)
})

test('无法确定事件目标时不阻止双击', () => {
    assert.equal(isInteractiveTarget(null), false)
    assert.equal(isInteractiveTarget(undefined), false)
})
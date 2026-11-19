import assert from 'node:assert/strict'
import test from 'node:test'

import { eventToShortcut, findShortcutConflict, matchShortcut, splitShortcut, type ShortcutKeyEvent } from '../src/utils/sys/shortcut'

const press = (key: string, modifiers: Partial<ShortcutKeyEvent> = {}): ShortcutKeyEvent => ({
    key,
    ctrlKey: false,
    altKey: false,
    shiftKey: false,
    metaKey: false,
    ...modifiers,
})

test('Ctrl+Q 转换为 Ctrl+Q，字母统一大写', () => {
    assert.equal(eventToShortcut(press('q', { ctrlKey: true })), 'Ctrl+Q')
    assert.equal(eventToShortcut(press('Q', { ctrlKey: true })), 'Ctrl+Q')
})

test('修饰键固定按 Ctrl、Alt、Shift、Meta 顺序输出', () => {
    assert.equal(eventToShortcut(press('w', { metaKey: true, shiftKey: true, altKey: true, ctrlKey: true })), 'Ctrl+Alt+Shift+Meta+W')
    assert.equal(eventToShortcut(press('1', { altKey: true })), 'Alt+1')
})

test('没有 Ctrl、Alt、Meta 的组合不接受，避免吞掉正常输入', () => {
    assert.equal(eventToShortcut(press('q')), '')
    assert.equal(eventToShortcut(press('Q', { shiftKey: true })), '')
})

test('只按下修饰键本身或输入法组合中返回空', () => {
    assert.equal(eventToShortcut(press('Control', { ctrlKey: true })), '')
    assert.equal(eventToShortcut(press('Shift', { shiftKey: true })), '')
    assert.equal(eventToShortcut(press('q', { ctrlKey: true, isComposing: true })), '')
})

test('功能键和空格规范化名称', () => {
    assert.equal(eventToShortcut(press('F5', { ctrlKey: true })), 'Ctrl+F5')
    assert.equal(eventToShortcut(press(' ', { ctrlKey: true })), 'Ctrl+Space')
    assert.equal(eventToShortcut(press('Escape', { altKey: true })), 'Alt+Escape')
})

test('命中配置的快捷键', () => {
    assert.equal(matchShortcut(press('q', { ctrlKey: true }), 'Ctrl+Q'), true)
    assert.equal(matchShortcut(press('k', { ctrlKey: true }), 'Ctrl+Q'), false)
})

test('修饰键多一个或少一个都不命中', () => {
    assert.equal(matchShortcut(press('q', { ctrlKey: true, shiftKey: true }), 'Ctrl+Q'), false)
    assert.equal(matchShortcut(press('q', { ctrlKey: true }), 'Ctrl+Shift+Q'), false)
})

test('未配置（空字符串）永远不命中', () => {
    assert.equal(matchShortcut(press('q', { ctrlKey: true }), ''), false)
})

test('按住不放产生的重复事件不重复触发', () => {
    assert.equal(matchShortcut(press('q', { ctrlKey: true, repeat: true }), 'Ctrl+Q'), false)
})

test('检测与其他功能的冲突，自己不算冲突', () => {
    const current = { closeCurrentTab: 'Ctrl+Q', closeOtherTabs: '', closeAllTabs: 'Ctrl+Alt+Q', quickSearch: 'Ctrl+K' }
    assert.equal(findShortcutConflict(current, 'closeOtherTabs', 'Ctrl+K'), 'quickSearch')
    assert.equal(findShortcutConflict(current, 'closeCurrentTab', 'Ctrl+Q'), undefined)
    assert.equal(findShortcutConflict(current, 'closeOtherTabs', 'Ctrl+Shift+Q'), undefined)
})

test('清除（空字符串）不算冲突，多个功能都可以为空', () => {
    const current = { a: '', b: '', c: 'Ctrl+Q' }
    assert.equal(findShortcutConflict(current, 'a', ''), undefined)
})

test('拆分快捷键用于展示', () => {
    assert.deepEqual(splitShortcut('Ctrl+Shift+W'), ['Ctrl', 'Shift', 'W'])
    assert.deepEqual(splitShortcut(''), [])
})
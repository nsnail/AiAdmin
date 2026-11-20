import assert from 'node:assert/strict'
import test from 'node:test'

import { exportFileName, normalizeExportValue } from '../src/utils/table/export'

const labels = { yes: '是', no: '否' }

test('空值导出为空字符串，不出现 null 或 undefined', () => {
    assert.equal(normalizeExportValue(null, 'zh-CN', labels), '')
    assert.equal(normalizeExportValue(undefined, 'zh-CN', labels), '')
})

test('布尔值按当前语言导出为是或否', () => {
    assert.equal(normalizeExportValue(true, 'zh-CN', labels), '是')
    assert.equal(normalizeExportValue(false, 'zh-CN', labels), '否')
    assert.equal(normalizeExportValue(true, 'en-US', { yes: 'Yes', no: 'No' }), 'Yes')
})

test('数字和普通字符串原样保留，数字 0 不会被当成空值', () => {
    assert.equal(normalizeExportValue(0, 'zh-CN', labels), 0)
    assert.equal(normalizeExportValue(42, 'zh-CN', labels), 42)
    assert.equal(normalizeExportValue('abc', 'zh-CN', labels), 'abc')
    assert.equal(normalizeExportValue('', 'zh-CN', labels), '')
})

test('ISO 时间字符串按列表同样的格式化方式输出', () => {
    const result = String(normalizeExportValue('2026-11-20T03:30:08.324Z', 'zh-CN', labels))
    assert.match(result, /^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/)
    assert.ok(!result.includes('T'))
})

test('不是时间格式的字符串不会被误转换', () => {
    assert.equal(normalizeExportValue('2026-11-20', 'zh-CN', labels), '2026-11-20')
    assert.equal(normalizeExportValue('order-2026-11-20T03:30:08', 'zh-CN', labels), 'order-2026-11-20T03:30:08')
})

test('数组用逗号连接，对象序列化为 JSON', () => {
    assert.equal(normalizeExportValue(['a', 'b', null], 'zh-CN', labels), 'a, b, ')
    assert.equal(normalizeExportValue({ a: 1 }, 'zh-CN', labels), '{"a":1}')
})

test('文件名带资源名和时间戳，后缀随格式变化，资源名中的连字符转下划线', () => {
    const excel = exportFileName('tg-account', 'excel')
    const json = exportFileName('tg-account', 'json')
    assert.match(excel, /^tg_account_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.xlsx$/)
    assert.match(json, /^tg_account_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.json$/)
})
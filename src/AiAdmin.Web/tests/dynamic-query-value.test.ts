import assert from 'node:assert/strict'
import { test } from 'node:test'
import { convertDynamicQueryValue, resolveBasicOperator } from '../src/utils/json/dynamic-query-value'

test('preserves integer identifiers beyond JavaScript precision', () => {
    assert.equal(convertDynamicQueryValue('9007199254740993', 'number'), '9007199254740993')
    assert.equal(convertDynamicQueryValue('-9007199254740993', 'number'), '-9007199254740993')
    assert.equal(convertDynamicQueryValue('500', 'number'), 500)
    assert.equal(convertDynamicQueryValue('1.25', 'number'), 1.25)
    assert.equal(convertDynamicQueryValue('2', 'enum'), 2)
})

test('preserves booleans restored from JSON and converts form strings', () => {
    assert.equal(convertDynamicQueryValue(true, 'boolean'), true)
    assert.equal(convertDynamicQueryValue(false, 'boolean'), false)
    assert.equal(convertDynamicQueryValue('true', 'boolean'), true)
    assert.equal(convertDynamicQueryValue('false', 'boolean'), false)
})

test('leaves invalid and null values available for explicit backend validation', () => {
    assert.equal(convertDynamicQueryValue('wrong', 'number'), 'wrong')
    assert.equal(convertDynamicQueryValue(null, 'number'), null)
    assert.equal(convertDynamicQueryValue('false', 'string'), 'false')
    assert.equal(convertDynamicQueryValue('2026-01-01', 'date'), '2026-01-01')
})

test('uses equality for numeric identifiers instead of text operators', () => {
    // 账号 ID 等数值字段使用文本输入框，输入的是字符串，但必须按数值字段使用等于，否则后端会拒绝 Contains
    assert.equal(resolveBasicOperator({ control: 'input', valueType: 'number' }, '2323'), 'Equal')
    assert.equal(resolveBasicOperator({ control: 'input', valueType: 'enum' }, '2'), 'Equal')
    assert.equal(resolveBasicOperator({ control: 'input', valueType: 'boolean' }, 'true'), 'Equal')
    assert.equal(resolveBasicOperator({ control: 'select', valueType: 'string' }, 'a'), 'Equal')
})

test('keeps text and date defaults for other fields', () => {
    assert.equal(resolveBasicOperator({ control: 'input', valueType: 'string' }, 'abc'), 'Contains')
    assert.equal(resolveBasicOperator(undefined, 'abc'), 'Contains')
    assert.equal(resolveBasicOperator({ control: 'date', valueType: 'date' }, '2026-01-01'), 'DateRange')
    assert.equal(resolveBasicOperator({ control: 'date', valueType: 'date' }, ['a', 'b']), 'DateRange')
    assert.equal(resolveBasicOperator({ control: 'input', valueType: 'string' }, ['a', 'b']), 'Any')
})
import assert from 'node:assert/strict'
import { test } from 'node:test'
import { convertDynamicQueryValue } from '../src/utils/json/dynamic-query-value'

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
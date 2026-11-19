import assert from 'node:assert/strict'
import test from 'node:test'

import { formatCompactNumber, formatFullNumber } from '../src/utils/number'

test('小于 1000 的数原样显示', () => {
    assert.equal(formatCompactNumber(0), '0')
    assert.equal(formatCompactNumber(7), '7')
    assert.equal(formatCompactNumber(999), '999')
    assert.equal(formatCompactNumber(12.5), '12.5')
})

test('1000 到不足一百万用 K，保留一位小数并去掉多余的 .0', () => {
    assert.equal(formatCompactNumber(1000), '1K')
    assert.equal(formatCompactNumber(1500), '1.5K')
    assert.equal(formatCompactNumber(12000), '12K')
    assert.equal(formatCompactNumber(12345), '12.3K')
    assert.equal(formatCompactNumber(999_900), '999.9K')
})

test('一百万及以上用 M', () => {
    assert.equal(formatCompactNumber(1_000_000), '1M')
    assert.equal(formatCompactNumber(2_641_872), '2.6M')
    assert.equal(formatCompactNumber(12_000_000), '12M')
    assert.equal(formatCompactNumber(1_234_567_890), '1234.6M')
})

test('四舍五入后进位到下一档时升档，不出现 1000K', () => {
    assert.equal(formatCompactNumber(999_950), '1M')
    assert.equal(formatCompactNumber(999_999), '1M')
    assert.equal(formatCompactNumber(-999_999), '-1M')
})

test('负数保留符号，非有限数值显示 0', () => {
    assert.equal(formatCompactNumber(-1500), '-1.5K')
    assert.equal(formatCompactNumber(-12), '-12')
    assert.equal(formatCompactNumber(Number.NaN), '0')
    assert.equal(formatCompactNumber(Number.POSITIVE_INFINITY), '0')
})

test('完整数字加千分位且不丢小数', () => {
    assert.equal(formatFullNumber(2_641_872), '2,641,872')
    assert.equal(formatFullNumber(1234.56), '1,234.56')
    assert.equal(formatFullNumber(0), '0')
    assert.equal(formatFullNumber(Number.NaN), '0')
})

test('后端 long 字段到前端是数字字符串，缩写和千分位都要能正确处理，不能当成 0', () => {
    assert.equal(formatFullNumber('1234'), '1,234')
    assert.equal(formatFullNumber('56'), '56')
    assert.equal(formatFullNumber('0'), '0')
    assert.equal(formatFullNumber('2641872'), '2,641,872')
    assert.equal(formatCompactNumber('1500'), '1.5K')
    assert.equal(formatCompactNumber('2641872'), '2.6M')
})

test('空值、空串和无法解析的内容显示 0', () => {
    for (const value of [undefined, null, '', '  ', 'abc']) {
        assert.equal(formatFullNumber(value), '0')
        assert.equal(formatCompactNumber(value), '0')
    }
})
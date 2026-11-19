import assert from 'node:assert/strict'
import test from 'node:test'

import { getRecentHourRange } from '../src/utils/date'

/** 区间的两端换算为毫秒时间戳，便于比较 */
const toMillis = (range: string[]): [number, number] => [new Date(range[0]).getTime(), new Date(range[1]).getTime()]

test('区间右端取到下一整分钟，左端比右端早整一小时', () => {
    const [start, end] = toMillis(getRecentHourRange(new Date(2026, 10, 18, 9, 30, 45, 123)))
    assert.equal(end, new Date(2026, 10, 18, 9, 31, 0, 0).getTime())
    assert.equal(end - start, 60 * 60 * 1000)
})

test('当前时刻必定落在区间内，刚产生的记录不会被排除', () => {
    for (const now of [new Date(2026, 10, 18, 9, 30, 0, 0), new Date(2026, 10, 18, 9, 30, 59, 999), new Date(2026, 10, 18, 23, 59, 59, 999)]) {
        const [start, end] = toMillis(getRecentHourRange(now))
        assert.ok(start <= now.getTime() && now.getTime() < end, `${now.toISOString()} 应落在区间内`)
    }
})

test('跨整点和跨午夜时左右端日期与小时正确', () => {
    const [start, end] = toMillis(getRecentHourRange(new Date(2026, 10, 18, 0, 20, 10, 0)))
    assert.equal(start, new Date(2026, 10, 17, 23, 21, 0, 0).getTime())
    assert.equal(end, new Date(2026, 10, 18, 0, 21, 0, 0).getTime())
})

test('输出格式与日期选择器一致：带毫秒和本地时区偏移', () => {
    for (const value of getRecentHourRange(new Date(2026, 10, 18, 9, 30, 45, 123))) {
        assert.match(value, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2}$/)
    }
})
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

import { describeCron, type CronTranslate } from '../src/utils/cron'

const zh = JSON.parse(readFileSync(new URL('../src/locales/langs/zh.json', import.meta.url), 'utf8')) as Record<string, unknown>

/** 用中文语言包构造翻译函数，缺失的键直接抛错，避免漏配文案 */
const t: CronTranslate = (key, params) => {
    let node: unknown = zh
    for (const part of key.split('.')) node = (node as Record<string, unknown>)?.[part]
    if (typeof node !== 'string') throw new Error(`missing i18n key: ${key}`)
    return node.replace(/\{(\w+)\}/g, (_, name: string) => String(params?.[name] ?? ''))
}

test('用户反馈的 0 10 * * * * 描述为每小时第 10 分 0 秒', () => {
    assert.equal(describeCron('0 10 * * * *', t), '每小时第 10 分 0 秒执行')
})

test('常见简洁规则', () => {
    assert.equal(describeCron('* * * * * *', t), '每秒执行一次')
    assert.equal(describeCron('0 * * * * *', t), '每分钟执行一次')
    assert.equal(describeCron('0 0 * * * *', t), '每小时整点执行一次')
    assert.equal(describeCron('0 0 */2 * * *', t), '每 2 小时整点执行一次')
    assert.equal(describeCron('*/5 * * * * *', t), '每 5 秒执行一次')
    assert.equal(describeCron('0 */10 * * * *', t), '每 10 分钟执行一次')
    assert.equal(describeCron('0 30 9 * * *', t), '每天 09:30:00 执行')
    assert.equal(describeCron('0 0 0 * * 0', t), '每周日 00:00:00 执行')
    assert.equal(describeCron('30 15 8 * * 5', t), '每周五 08:15:30 执行')
})

test('5 位表达式补秒位后描述', () => {
    assert.equal(describeCron('*/5 * * * *', t), '每 5 分钟执行一次')
    assert.equal(describeCron('30 9 * * *', t), '每天 09:30:00 执行')
})

test('范围、列表与步长的组合都有描述，不再落到兜底', () => {
    const cases = [
        '0 0 9-18 * * *',
        '0 0 9-18/2 * * *',
        '0 0 8,12,18 * * *',
        '0 0,30 * * * *',
        '0 0 9 * * 1-5',
        '0 0 9 * * 1,3,5',
        '0 0 9 1 * *',
        '0 0 9 1,15 * *',
        '0 0 0 1 1 *',
        '0 0 9 */2 * *',
        '0 15-45/10 * * * *',
        '10/20 * * * * *',
        '0 0 0 * 1-6 *',
        '0 0 12 * * 0,6',
        '0 5 4 * * *',
    ]
    for (const expression of cases) {
        const text = describeCron(expression, t)
        assert.ok(text && text !== '-', expression)
        assert.ok(!text.includes('等待完整'), `${expression} -> ${text}`)
        assert.ok(!text.includes('超出范围'), `${expression} -> ${text}`)
    }
})

test('具体描述内容', () => {
    assert.equal(describeCron('0 0 9-18 * * *', t), '9 至 18 点，第 0 分，第 0 秒执行')
    assert.equal(describeCron('0 0 12 * * 0,6', t), '周六、周日，12:00:00执行')
    assert.equal(describeCron('10/20 * * * * *', t), '从第 10 秒起每 20 秒执行')
    assert.match(describeCron('0 0 9 * * 1-5', t), /周一至周五/)
    assert.match(describeCron('0 0 9 * * 1,3,5', t), /周一、周三、周五/)
    assert.match(describeCron('0 0 9 1,15 * *', t), /每月 1、15 日/)
    assert.match(describeCron('0 0 0 1 1 *', t), /1 月/)
})

test('非法表达式给出提示', () => {
    assert.equal(describeCron('', t), '-')
    assert.equal(describeCron(undefined, t), '-')
    assert.equal(describeCron('0 10 * *', t), '等待完整的 Cron 表达式')
    assert.equal(describeCron('a b c d e f', t), '等待完整的 Cron 表达式')
    assert.equal(describeCron('0 0 0 0 * *', t), 'Cron 字段取值超出范围')
    assert.equal(describeCron('0 60 * * * *', t), 'Cron 字段取值超出范围')
    assert.equal(describeCron('0 0 0 * * 7', t), 'Cron 字段取值超出范围')
    assert.equal(describeCron('0 0 5-2 * * *', t), 'Cron 字段取值超出范围')
    assert.equal(describeCron('0 */0 * * * *', t), '等待完整的 Cron 表达式')
})
/**
 * 数字格式化工具
 *
 * 提供仪表盘等场景使用的大数字缩写：用 K（千）、M（百万）代替末尾的多个 0，不依赖 Vue，便于单独测试
 */

/** 缩写档位，按数值从大到小排列 */
const COMPACT_UNITS: ReadonlyArray<{ readonly size: number; readonly suffix: string }> = [
    { size: 1_000_000, suffix: 'M' },
    { size: 1_000, suffix: 'K' },
]

/**
 * 把数字或数字字符串转为数值：后端把 long 类型统一序列化为字符串（避免雪花 ID 在 JS 里丢精度），耗时、数量等 long 字段到前端是字符串，
 * 直接当数字处理会得到 NaN
 * @param value 数值或数字字符串
 * @returns 数值，空串、空值和无法解析的内容返回 NaN
 */
const toNumber = (value: number | string | null | undefined): number => {
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '') return Number(value)
    return Number.NaN
}

/**
 * 保留一位小数并去掉多余的 .0
 * @param value 数值
 * @returns 文本
 */
const trimFraction = (value: number): string => String(Number(value.toFixed(1)))

/**
 * 大数字缩写：小于 1000 原样显示；1000 到不足一百万用 K；一百万及以上用 M；缩写后保留一位小数并去掉多余的 .0。
 * 四舍五入后恰好进位到下一档时升档显示（999950 显示为 1M 而不是 1000K）
 * @param value 数值
 * @returns 缩写文本，非有限数值返回 0
 */
export function formatCompactNumber(input: number | string | null | undefined): string {
    const value = toNumber(input)
    if (!Number.isFinite(value)) return '0'
    const sign = value < 0 ? '-' : ''
    const abs = Math.abs(value)
    if (abs < 1000) return `${sign}${trimFraction(abs)}`

    for (let index = 0; index < COMPACT_UNITS.length; index++) {
        const { size, suffix } = COMPACT_UNITS[index]
        if (abs < size) continue
        const scaled = Number((abs / size).toFixed(1))
        // 四舍五入后达到上一档的进位阈值（如 999.95K 进位为 1000K）时，交给更大的档位重新计算
        if (index > 0 && scaled >= COMPACT_UNITS[index - 1].size / size)
            return formatCompactNumber(sign === '-' ? -COMPACT_UNITS[index - 1].size : COMPACT_UNITS[index - 1].size)
        return `${sign}${trimFraction(scaled)}${suffix}`
    }
    return `${sign}${trimFraction(abs)}`
}

/**
 * 完整数字加千分位分隔，用于鼠标提示、耗时等需要看准确值的场景
 * @param value 数值或数字字符串（后端 long 字段到前端是字符串）
 * @returns 带千分位的文本，保留原有小数位；无法解析时返回 0
 */
export function formatFullNumber(value: number | string | null | undefined): string {
    const number = toNumber(value)
    if (!Number.isFinite(number)) return '0'
    return number.toLocaleString('en-US', { maximumFractionDigits: 20 })
}
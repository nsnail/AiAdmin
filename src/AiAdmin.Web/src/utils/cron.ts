/**
 * Cron 表达式的人类可读描述，语法与后端 CronMatcher 保持一致：
 * 5 位（分 时 日 月 周）或 6 位（秒 分 时 日 月 周），字段支持 *、数字、a-b、a/s、a-b、a-b/s 和逗号列表，周取值 0 到 6（0 为周日）
 */

/** 翻译函数，兼容 vue-i18n 的 t */
export type CronTranslate = (key: string, params?: Record<string, string | number>) => string

type FieldKey = 'second' | 'minute' | 'hour' | 'day' | 'month' | 'week'

type CronPart =
    | { type: 'value'; value: number }
    | { type: 'range'; start: number; end: number }
    | { type: 'step'; start: number; end: number; step: number; full: boolean }

/** 解析后的字段：any 表示 *，否则由若干片段组成 */
interface ParsedField {
    key: FieldKey
    any: boolean
    parts: CronPart[]
}

const FIELD_SPECS: { key: FieldKey; min: number; max: number }[] = [
    { key: 'second', min: 0, max: 59 },
    { key: 'minute', min: 0, max: 59 },
    { key: 'hour', min: 0, max: 23 },
    { key: 'day', min: 1, max: 31 },
    { key: 'month', min: 1, max: 12 },
    { key: 'week', min: 0, max: 6 },
]

const DESC = 'cronEditor.description'
const DETAIL = `${DESC}.detail`

/** 解析单个逗号片段，语法非法返回 undefined，取值越界返回 'range' */
const parseItem = (item: string, min: number, max: number): CronPart | undefined | 'range' => {
    const [base, stepText, ...rest] = item.split('/')
    if (!base || rest.length > 0) return undefined
    let step = 1
    if (stepText !== undefined) {
        if (!/^\d+$/.test(stepText)) return undefined
        step = Number(stepText)
        if (step < 1) return undefined
    }
    let start = min
    let end = max
    if (base !== '*') {
        const rangeMatch = base.match(/^(\d+)-(\d+)$/)
        if (/^\d+$/.test(base)) {
            start = Number(base)
            end = stepText !== undefined ? max : start
        } else if (rangeMatch) {
            start = Number(rangeMatch[1])
            end = Number(rangeMatch[2])
        } else {
            return undefined
        }
    }
    if (start < min || end > max || start > end) return 'range'
    if (stepText !== undefined || base === '*') return { type: 'step', start, end, step, full: start === min && end === max }
    return start === end ? { type: 'value', value: start } : { type: 'range', start, end }
}

/** 解析整个字段，返回字段或错误类型 */
const parseField = (key: FieldKey, text: string, min: number, max: number): ParsedField | 'syntax' | 'range' => {
    if (text === '*') return { key, any: true, parts: [] }
    const parts: CronPart[] = []
    for (const item of text.split(',')) {
        const part = parseItem(item, min, max)
        if (part === undefined) return 'syntax'
        if (part === 'range') return 'range'
        parts.push(part)
    }
    return { key, any: false, parts }
}

/** 展开步长片段的全部取值 */
const expandStep = (part: Extract<CronPart, { type: 'step' }>): number[] => {
    const values: number[] = []
    for (let value = part.start; value <= part.end; value += part.step) values.push(value)
    return values
}

/** 去重并排序，周按周一到周日排列（周日取值 0 排最后） */
const sortValues = (values: number[], isWeek: boolean): number[] => [...new Set(values)].sort((a, b) => (isWeek ? (a || 7) - (b || 7) : a - b))

/** 描述一个已约束的字段，如“第 10 分”“每 5 秒”“周一至周五” */
const describeField = (field: ParsedField, t: CronTranslate): string => {
    const listSep = t(`${DESC}.listSeparator`)
    const base = `${DETAIL}.${field.key}`
    const isWeek = field.key === 'week'
    const name = (value: number): string => (isWeek ? t(`${DESC}.weekday${value}`) : String(value))
    const values: number[] = []
    const others: string[] = []
    for (const part of field.parts) {
        if (part.type === 'value') values.push(part.value)
        else if (part.type === 'range') others.push(t(`${base}.range`, { start: name(part.start), end: name(part.end) }))
        // 周的步长最多 7 个取值，展开为具体星期；其余字段保留“每 N”的表达
        else if (isWeek) values.push(...expandStep(part))
        else if (part.full) others.push(t(`${base}.every`, { step: part.step }))
        else if (part.end === FIELD_SPECS.find((spec) => spec.key === field.key)?.max)
            others.push(t(`${base}.everyFrom`, { start: part.start, step: part.step }))
        else others.push(t(`${base}.everyRange`, { start: part.start, end: part.end, step: part.step }))
    }
    const pieces: string[] = []
    if (values.length) pieces.push(t(`${base}.at`, { value: sortValues(values, isWeek).map(name).join(listSep) }))
    return [...pieces, ...others].join(listSep)
}

/** 时、分、秒是否都是单个数值，是则返回 HH:mm:ss */
const getSingleTime = (hour: ParsedField, minute: ParsedField, second: ParsedField): string | undefined => {
    const values = [hour, minute, second].map((field) =>
        field.parts.length === 1 && field.parts[0].type === 'value' ? String(field.parts[0].value).padStart(2, '0') : undefined,
    )
    return values.every(Boolean) ? values.join(':') : undefined
}

/** 生成时间部分的描述片段，较高位的通配符只在需要衔接时补“每小时/每分钟/每秒” */
const describeTime = (hour: ParsedField, minute: ParsedField, second: ParsedField, t: CronTranslate): string[] => {
    const time = getSingleTime(hour, minute, second)
    if (time) return [t(`${DETAIL}.time`, { time })]
    const order = [hour, minute, second]
    const constrained = order.map((field) => !field.any)
    const first = constrained.indexOf(true)
    if (first < 0) return [t(`${DETAIL}.second.everyAny`)]
    const last = constrained.lastIndexOf(true)
    const pieces: string[] = []
    order.forEach((field, index) => {
        if (!field.any) pieces.push(describeField(field, t))
        else if ((index === first - 1 && first === 1) || (index > first && (index < last || (index === 2 && last < 2))))
            pieces.push(t(`${DETAIL}.${field.key}.everyAny`))
    })
    return pieces
}

/** 常见的整齐规则使用简洁说法，返回 undefined 表示走通用描述 */
const describeCommon = (parts: string[], t: CronTranslate): string | undefined => {
    const [second, minute, hour, day, month, week] = parts
    const dateAny = day === '*' && month === '*' && week === '*'
    const stepOf = (value: string): string | undefined => value.match(/^(?:\*|0)\/(\d+)$/)?.[1]
    if (dateAny && hour === '*') {
        if (second === '*' && minute === '*') return t(`${DESC}.everySecond`)
        if (second === '0' && minute === '*') return t(`${DESC}.everyMinute`)
        if (second === '0' && minute === '0') return t(`${DESC}.hourly`)
        if (/^\d+$/.test(second) && /^\d+$/.test(minute)) return t(`${DESC}.hourlyAt`, { minute, second })
        if (stepOf(second) && minute === '*') return t(`${DESC}.everySeconds`, { value: stepOf(second)! })
        if (second === '0' && stepOf(minute)) return t(`${DESC}.everyMinutes`, { value: stepOf(minute)! })
    }
    const hourStep = hour.match(/^\*\/(\d+)$/)?.[1]
    if (dateAny && second === '0' && minute === '0' && hourStep) return t(`${DESC}.everyHours`, { value: hourStep })
    const time = [hour, minute, second].every((item) => /^\d+$/.test(item))
        ? [hour, minute, second].map((item) => item.padStart(2, '0')).join(':')
        : undefined
    if (time && day === '*' && month === '*' && week === '*') return t(`${DESC}.daily`, { time })
    if (time && day === '*' && month === '*' && /^\d$/.test(week)) return t(`${DESC}.weekly${week}`, { time })
    return undefined
}

/**
 * 生成 Cron 表达式的可读描述
 * @param expression Cron 表达式，5 位或 6 位
 * @param t 翻译函数
 * @returns 描述文案；空表达式返回 -，语法或取值不合法返回对应提示
 */
export const describeCron = (expression: string | undefined, t: CronTranslate): string => {
    if (!expression?.trim()) return '-'
    const parts = expression.trim().split(/\s+/)
    if (parts.length === 5) parts.unshift('0')
    if (parts.length !== 6 || parts.some((part) => !/^[\d*/,-]+$/.test(part))) return t(`${DESC}.invalid`)

    const fields: ParsedField[] = []
    for (const [index, spec] of FIELD_SPECS.entries()) {
        const field = parseField(spec.key, parts[index], spec.min, spec.max)
        if (field === 'syntax') return t(`${DESC}.invalid`)
        if (field === 'range') return t(`${DESC}.outOfRange`)
        fields.push(field)
    }

    const common = describeCommon(parts, t)
    if (common) return common
    const [second, minute, hour, day, month, week] = fields
    const pieces = [month, day, week].filter((field) => !field.any).map((field) => describeField(field, t))
    pieces.push(...describeTime(hour, minute, second, t))
    return t(`${DESC}.generic`, { value: pieces.join(t(`${DESC}.separator`)) })
}
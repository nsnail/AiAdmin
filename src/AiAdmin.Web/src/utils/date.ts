interface DateTimeFormatOptions {
    fractionalSecondDigits?: 3
}

/**
 * 将接口返回的带时区时间格式化为客户端本地时间，日期和时间使用空格分隔
 */
export function formatDateTime(value: string | Date | null | undefined, locale = 'zh-CN', options: DateTimeFormatOptions = {}): string {
    if (!value) return ''
    const date = value instanceof Date ? value : new Date(value)
    if (Number.isNaN(date.getTime())) return String(value).replace('T', ' ')
    const parts = new Intl.DateTimeFormat(locale, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hourCycle: 'h23',
        ...options,
    })
        .formatToParts(date)
        .reduce<Record<string, string>>((result, part) => {
            result[part.type] = part.value
            return result
        }, {})
    const milliseconds = options.fractionalSecondDigits ? `.${parts.fractionalSecond}` : ''
    return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}:${parts.second}${milliseconds}`
}

/**
 * 生成带毫秒和本地时区偏移的 ISO 时间文本，格式需与日期选择器的 valueFormat（YYYY-MM-DDTHH:mm:ss.SSSZ）一致，否则选择器无法回显默认值
 */
export function toLocalIsoString(date: Date): string {
    const pad = (value: number, length = 2) => String(value).padStart(length, '0')
    const offsetMinutes = -date.getTimezoneOffset()
    const offset = `${offsetMinutes >= 0 ? '+' : '-'}${pad(Math.floor(Math.abs(offsetMinutes) / 60))}:${pad(Math.abs(offsetMinutes) % 60)}`
    return (
        `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}${offset}`
    )
}

/**
 * 今日的日期范围筛选值：本地今天零点到明天零点，右开区间，用作列表默认的 CreatedAt 筛选
 */
export function getTodayRange(now = new Date()): string[] {
    const start = new Date(now)
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(end.getDate() + 1)
    return [toLocalIsoString(start), toLocalIsoString(end)]
}
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
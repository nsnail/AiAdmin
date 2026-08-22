/**
 * 将接口返回的带时区时间格式化为客户端所在地区时间
 */
export function formatDateTime(value: string | Date | null | undefined, locale = 'zh-CN'): string {
    if (!value) return ''
    const date = value instanceof Date ? value : new Date(value)
    if (Number.isNaN(date.getTime())) return String(value)
    return new Intl.DateTimeFormat(locale, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    }).format(date)
}
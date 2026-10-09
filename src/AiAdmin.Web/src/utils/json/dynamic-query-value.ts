/** 按字段类型转换查询值，保留超出安全整数范围的 ID，交由后端精确转换 */
export const convertDynamicQueryValue = (value: unknown, type?: string): unknown => {
    if (value === null || value === undefined) return value
    if (type === 'boolean') {
        if (value === 'true') return true
        if (value === 'false') return false
        return value
    }
    if (type !== 'number' && type !== 'enum') return value
    if (typeof value !== 'string' || value.trim() === '') return value
    const number = Number(value)
    if (!Number.isFinite(number) || (Number.isInteger(number) && !Number.isSafeInteger(number))) return value
    return number
}
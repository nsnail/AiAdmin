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

/** 基础筛选字段的元数据，仅包含选择操作符所需的部分 */
export interface BasicFilterField {
    control?: string
    valueType?: string
}

/**
 * 按字段元数据选择基础筛选的默认操作符：数组为集合或日期范围，下拉、布尔、数值和枚举字段为等于，日期为日期范围，其余文本字段为包含。
 * 数值与枚举字段不能使用文本操作符，否则后端会因字段类型不匹配而拒绝。
 */
export const resolveBasicOperator = (field: BasicFilterField | undefined, value: unknown): string => {
    if (Array.isArray(value)) return field?.valueType === 'date' ? 'DateRange' : 'Any'
    if (field?.control === 'select' || field?.valueType === 'number' || field?.valueType === 'enum' || field?.valueType === 'boolean') return 'Equal'
    if (field?.valueType === 'date') return 'DateRange'
    return typeof value === 'string' ? 'Contains' : 'Equal'
}
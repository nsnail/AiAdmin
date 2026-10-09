/** 动态查询 JSON 的结构字段使用统一大小写，业务字段名和条件值保持原样 */
const queryKeys = ['current', 'size', 'parentId', 'sortField', 'sortOrder', 'dynamicFilter']
const filterKeys = ['logic', 'filters', 'field', 'operator', 'value', 'dynamicFilter']

/** 按已知结构字段规范化属性名，重复属性采用最后一个值，与后端反序列化一致 */
const normalizeObject = (value: unknown, keys: string[]): unknown => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return value
    return Object.fromEntries(
        Object.entries(value).map(([key, item]) => {
            const name = keys.find((candidate) => candidate.toLowerCase() === key.toLowerCase()) ?? key
            if (name === 'dynamicFilter') return [name, normalizeObject(item, filterKeys)]
            if (name === 'filters' && Array.isArray(item)) {
                return [name, item.map((filter) => normalizeObject(filter, filterKeys))]
            }
            return [name, item]
        }),
    )
}

/** 解析手工输入的查询 JSON，仅规范化查询结构，保留原有 JSON 语法错误 */
export const parseDynamicQueryJson = (text: string): unknown => normalizeObject(JSON.parse(text), queryKeys)
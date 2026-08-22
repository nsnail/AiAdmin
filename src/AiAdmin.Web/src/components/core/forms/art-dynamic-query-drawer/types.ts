export interface DynamicQueryField {
    field: string
    label: string
    type?: 'string' | 'number' | 'boolean' | 'date'
}

export interface DynamicFilter {
    field?: string
    operator?: string
    value?: unknown
    logic?: 'And' | 'Or'
    filters?: DynamicFilter[]
}

export interface QueryGroup {
    logic: 'And' | 'Or'
    filters: QueryNode[]
}

export type QueryNode =
    { id: string; kind: 'condition'; field: string; operator: string; value: unknown } | { id: string; kind: 'group'; group: QueryGroup }

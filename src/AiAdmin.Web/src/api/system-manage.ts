import request from '@/utils/http'
import { AppRouteRecord } from '@/types/router'

export type DynamicFilter = {
    field?: string
    operator?: string
    value?: unknown
    logic?: 'And' | 'Or'
    filters?: DynamicFilter[]
}

export interface SavedQuery {
    id: string
    name: string
    isGlobal: boolean
    dynamicFilter: DynamicFilter
}

export interface ListFilterField {
    field: string
    label: string
    control: 'input' | 'select' | 'date' | 'number'
    span: number
    sort: number
    placeholder: string
    options: Array<{ label: string; value: string }>
    valueType: 'string' | 'number' | 'boolean' | 'date'
}

export interface LoginLogRecord {
    id: string
    userId: string
    userName: string
    ownerId: string
    ownerDepartmentId: string
    clientIp: string
    region: string
    userAgent: string
    operatingSystem: string
    browser: string
    deviceType: string
    platform: string
    language: string
    timeZone: string
    screenResolution: string
    viewportSize: string
    colorDepth?: number
    pixelRatio?: number
    touchPoints?: number
    clientHints: string
    createdAt: string
}

export type EnabledStateResource = 'user' | 'role' | 'menu' | 'department' | 'dictionary-item'

export interface RedisServerInfo {
    endpoint: string
    version: string
    mode: string
    connectedClients: number
    usedMemory: string
    maxMemory: string
    databaseSize: number
    cpuUsagePercent: number
    uptimeSeconds: number
    cacheHitRatePercent: number
}

export interface RedisCacheKey {
    key: string
    type: string
    timeToLiveMilliseconds: number
    memoryBytes: number
    length: number
}

export interface RedisCacheValue extends RedisCacheKey {
    value: string
}

export interface WalletInfo {
    id: string
    createdAt: string
    userId: string
    userName: string
    userEmail: string
    userAvatar: string | null
    currency: string
    availableBalance: number
    frozenBalance: number
    totalIncome: number
    totalExpense: number
    lastTransactionAt: string | null
    version: number
}

export function fetchGetMyWallet() {
    return request.get<WalletInfo>({ url: '/api/wallet/me' })
}

export type WalletListParams = {
    current: number
    size: number
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
    [field: string]: unknown
}

export function fetchGetWalletList(data: WalletListParams) {
    const queryFields = new Set(['current', 'size', 'dynamicFilter', 'sortField', 'sortOrder'])
    const filters = Object.entries(data)
        .filter(([field, value]) => !queryFields.has(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: Array.isArray(value) ? (field === 'LastTransactionAt' ? 'DateRange' : 'Any') : getGeneratedFilterOperator(value),
            value: normalizeDateRange(value === 'true' ? true : value === 'false' ? false : value),
        }))
    if (data.dynamicFilter) filters.push(data.dynamicFilter)
    return request.post<Api.Common.PaginatedResponse<WalletInfo>>({
        url: '/api/wallet/list',
        data: createDynamicQuery(data.current, data.size, filters, data.sortField, data.sortOrder),
    })
}

export interface SaveRedisCacheParams {
    key: string
    value: string
    expireSeconds: number
}

export function fetchGetRedisServerInfo() {
    return request.get<RedisServerInfo>({ url: '/api/redis-cache/server-info' })
}

export function fetchGetRedisKeys(pattern?: string, limit = 100) {
    return request.get<RedisCacheKey[]>({ url: '/api/redis-cache/keys', params: { pattern, limit } })
}

export function fetchGetRedisValue(key: string) {
    return request.get<RedisCacheValue>({ url: '/api/redis-cache/value', params: { key } })
}

export function fetchSaveRedisValue(data: SaveRedisCacheParams) {
    return request.put<RedisCacheValue>({ url: '/api/redis-cache/value', data })
}

export function fetchDeleteRedisValue(key: string) {
    return request.del<void>({ url: '/api/redis-cache/value', params: { key } })
}

export function fetchUpdateEnabledState(resource: EnabledStateResource, id: string, isEnabled: boolean) {
    return request.put<void>({ url: `/api/enabled-state/${resource}/${id}`, data: { isEnabled } })
}

export function fetchGetListFilterFields(
    resource: 'user' | 'role' | 'menu' | 'department' | 'api-endpoint' | 'scheduled-job' | 'login-log' | 'wallet',
) {
    return request.get<ListFilterField[]>({ url: `/api/${resource}/filter-fields` })
}

export function fetchGetLoginLogList(data: {
    current: number
    size: number
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
    [field: string]: unknown
}) {
    // 筛选栏字段名由元数据返回，不应依赖 PascalCase 命名约定，否则字段被序列化为 camelCase 时会被忽略。
    const queryFields = new Set(['current', 'size', 'dynamicFilter', 'sortField', 'sortOrder'])
    const filters = Object.entries(data)
        .filter(([field, value]) => !queryFields.has(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: Array.isArray(value) ? (field === 'CreatedAt' ? 'DateRange' : 'Any') : getGeneratedFilterOperator(value),
            value: normalizeDateRange(value === 'true' ? true : value === 'false' ? false : value),
        }))
    if (data.dynamicFilter) filters.push(data.dynamicFilter)
    return request.post<Api.Common.PaginatedResponse<LoginLogRecord>>({
        url: '/api/login-log/list',
        data: createDynamicQuery(data.current, data.size, filters, data.sortField, data.sortOrder),
    })
}

export function fetchGetSavedQueries(route: string) {
    return request.get<SavedQuery[]>({ url: '/api/saved-query', params: { route } })
}

export function fetchSaveQuery(data: { name: string; route: string; dynamicFilter: DynamicFilter; isGlobal: boolean }) {
    return request.post<SavedQuery>({ url: '/api/saved-query', data })
}

export function fetchDeleteSavedQuery(id: string) {
    return request.del<void>({ url: `/api/saved-query/${id}` })
}

export interface SystemLogSearchParams extends Api.Common.CommonSearchParams {
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
}

export function fetchGetSystemLogs(params: SystemLogSearchParams) {
    return request.post<Api.Common.PaginatedResponse<Api.SystemManage.SystemLogItem>>({
        url: '/api/system-log/list',
        data: {
            current: params.current,
            size: params.size,
            dynamicFilter: params.dynamicFilter,
            sortField: params.sortField || undefined,
            sortOrder: params.sortOrder || undefined,
        },
    })
}

type DynamicQuery = {
    current?: number
    size?: number
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
}

function createDynamicQuery(
    current: number | undefined,
    size: number | undefined,
    filters: DynamicFilter[],
    sortField?: string,
    sortOrder?: 'asc' | 'desc',
): DynamicQuery {
    return {
        current,
        size,
        sortField,
        sortOrder,
        ...(filters.length > 0 ? { dynamicFilter: { logic: 'And', filters } } : {}),
    }
}

function getTextFilter(field: string, value: string | undefined): DynamicFilter | undefined {
    const text = value?.trim()
    return text ? { field, operator: 'Contains', value: text } : undefined
}

function getGeneratedFilterOperator(value: unknown): 'Equal' | 'Contains' {
    return typeof value === 'boolean' || typeof value === 'number' || value === 'true' || value === 'false' ? 'Equal' : 'Contains'
}

function normalizeDateRange(value: unknown): unknown {
    if (!Array.isArray(value) || value.length !== 2 || !value[0] || !value[1]) return value
    const start = new Date(String(value[0]))
    const end = new Date(String(value[1]))
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) return value
    if (
        start.getFullYear() === end.getFullYear() &&
        start.getMonth() === end.getMonth() &&
        start.getDate() === end.getDate() &&
        end.getHours() === 0 &&
        end.getMinutes() === 0 &&
        end.getSeconds() === 0
    ) {
        end.setDate(end.getDate() + 1)
        return [value[0], end.toISOString()]
    }
    return value
}

// 获取用户列表
export function fetchGetUserList(params: Api.SystemManage.UserSearchParams) {
    const generatedFilters = Object.entries(params)
        .filter(([field, value]) => /^[A-Z]/.test(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: Array.isArray(value)
                ? ['createdat', 'updatedat'].includes(field.toLowerCase())
                    ? 'DateRange'
                    : 'Any'
                : getGeneratedFilterOperator(value),
            value: normalizeDateRange(value === 'true' ? true : value === 'false' ? false : value),
        }))
    const baseFilters = generatedFilters.length
        ? generatedFilters
        : [
              getTextFilter('UserName', params.userName),
              getTextFilter('Phone', params.userPhone),
              getTextFilter('Email', params.userEmail),
              params.userGender ? { field: 'Gender', operator: 'Equal', value: params.userGender } : undefined,
              params.status === '1'
                  ? { field: 'IsEnabled', operator: 'Equal', value: true }
                  : params.status === '2'
                    ? { field: 'IsEnabled', operator: 'Equal', value: false }
                    : undefined,
          ].filter((filter): filter is DynamicFilter => Boolean(filter))
    const filters = params.dynamicFilter ? [...baseFilters, params.dynamicFilter] : baseFilters

    return request.post<Api.SystemManage.UserList>({
        url: '/api/user/list',
        data: createDynamicQuery(params.current, params.size, filters, params.sortField, params.sortOrder),
    })
}

export function fetchCreateUser(data: Api.SystemManage.SaveUserParams) {
    return request.post<Api.SystemManage.UserListItem>({ url: '/api/user', data })
}

export function fetchUpdateUser(id: string, data: Api.SystemManage.UpdateUserParams) {
    return request.put<Api.SystemManage.UserListItem>({ url: `/api/user/${id}`, data })
}

export function fetchUploadUserAvatar(id: string, file: File) {
    const data = new FormData()
    data.append('file', file)
    return request.post<Api.SystemManage.UserListItem>({ url: `/api/user/${id}/avatar`, data })
}

export function fetchDeleteUserAvatar(id: string) {
    return request.del<Api.SystemManage.UserListItem>({ url: `/api/user/${id}/avatar` })
}

export function fetchGetUserRoles() {
    return request.get<Api.SystemManage.RoleListItem[]>({ url: '/api/user/roles' })
}

// 获取当前用户的全部下级邀请关系树
export function fetchGetReferralTree() {
    return request.get<Api.SystemManage.ReferralTreeResult>({ url: '/api/user/referrals' })
}

export function fetchGetDepartmentTree() {
    return request.get<Api.SystemManage.DepartmentTreeItem[]>({ url: '/api/department/tree' })
}

export function fetchCreateDepartment(data: Api.SystemManage.SaveDepartmentParams) {
    return request.post<Api.SystemManage.DepartmentTreeItem>({ url: '/api/department', data })
}

export function fetchUpdateDepartment(id: string, data: Api.SystemManage.SaveDepartmentParams) {
    return request.put<Api.SystemManage.DepartmentTreeItem>({ url: `/api/department/${id}`, data })
}

export function fetchDeleteDepartment(id: string) {
    return request.del<void>({ url: `/api/department/${id}`, showSuccessMessage: true })
}

// 获取角色列表
export function fetchGetRoleList(params: Api.SystemManage.RoleSearchParams) {
    const generatedFilters = Object.entries(params)
        .filter(([field, value]) => /^[A-Z]/.test(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: Array.isArray(value)
                ? ['createdat', 'updatedat'].includes(field.toLowerCase())
                    ? 'DateRange'
                    : 'Any'
                : getGeneratedFilterOperator(value),
            value: normalizeDateRange(value === 'true' ? true : value === 'false' ? false : value),
        }))
    const baseFilters = generatedFilters.length
        ? generatedFilters
        : [
              getTextFilter('Name', params.roleName),
              getTextFilter('Code', params.roleCode),
              getTextFilter('Description', params.description),
              typeof params.enabled === 'boolean' ? { field: 'IsEnabled', operator: 'Equal', value: params.enabled } : undefined,
              params.startTime && params.endTime
                  ? { field: 'CreatedAt', operator: 'Range', value: [params.startTime, params.endTime] }
                  : params.startTime
                    ? { field: 'CreatedAt', operator: 'GreaterThanOrEqual', value: params.startTime }
                    : params.endTime
                      ? { field: 'CreatedAt', operator: 'LessThan', value: params.endTime }
                      : undefined,
          ].filter((filter): filter is DynamicFilter => Boolean(filter))
    const filters = params.dynamicFilter ? [...baseFilters, params.dynamicFilter] : baseFilters

    return request.post<Api.SystemManage.RoleList>({
        url: '/api/role/list',
        data: createDynamicQuery(params.current, params.size, filters, params.sortField, params.sortOrder),
    })
}

export function fetchCreateRole(data: Api.SystemManage.SaveRoleParams) {
    return request.post<Api.SystemManage.RoleListItem>({ url: '/api/role', data })
}

export function fetchUpdateRole(id: string, data: Api.SystemManage.SaveRoleParams) {
    return request.put<Api.SystemManage.RoleListItem>({ url: `/api/role/${id}`, data })
}

export function fetchDeleteRole(id: string) {
    return request.del<void>({ url: `/api/role/${id}`, showSuccessMessage: true })
}

export function fetchGetRoleMenus(id: string) {
    return request.get<AppRouteRecord[]>({ url: `/api/role/${id}/menus` })
}

export function fetchSaveRoleMenus(id: string, menuIds: string[]) {
    return request.put<void>({ url: `/api/role/${id}/menus`, data: { menuIds } })
}

export function fetchGetRoleApis(id: string) {
    return request.get<string[]>({ url: `/api/role/${id}/apis` })
}

export function fetchSaveRoleApis(id: string, apiIds: string[]) {
    return request.put<void>({ url: `/api/role/${id}/apis`, data: { apiIds } })
}

export function fetchGetApiEndpointList(dynamicFilter?: DynamicFilter, sortField?: string, sortOrder?: 'asc' | 'desc') {
    return request.post<Api.SystemManage.ApiEndpointItem[]>({
        url: '/api/api-endpoint/list',
        data: { dynamicFilter, sortField, sortOrder },
    })
}

export function fetchGetApiDocumentation() {
    return request.get<Api.SystemManage.ApiDocumentationResult>({ url: '/api/api-endpoint/documentation' })
}

export function fetchSyncApiEndpoints() {
    return request.post<Api.SystemManage.ApiSyncResult>({ url: '/api/api-endpoint/sync' })
}

export function fetchGetCurrentMenuNames() {
    return request.get<AppRouteRecord[]>({ url: '/api/menu/current' })
}

// 获取菜单列表
export function fetchGetMenuList(dynamicFilter?: DynamicFilter, sortField?: string, sortOrder?: 'asc' | 'desc') {
    return request.post<AppRouteRecord[]>({
        url: '/api/menu/list',
        data: { dynamicFilter, sortField, sortOrder },
    })
}

export function fetchCreateMenu(data: Record<string, any>) {
    return request.post<AppRouteRecord>({ url: '/api/menu', data })
}

export function fetchUpdateMenu(id: string, data: Record<string, any>) {
    return request.put<AppRouteRecord>({ url: `/api/menu/${id}`, data })
}

export function fetchDeleteMenu(id: string) {
    return request.del<void>({ url: `/api/menu/${id}`, showSuccessMessage: true })
}

export function fetchGetDictionaryCategories() {
    return request.get<Api.SystemManage.DictionaryCategory[]>({ url: '/api/dictionary/categories' })
}

export function fetchGetDictionaryFilterFields() {
    return request.get<ListFilterField[]>({ url: '/api/dictionary/filter-fields' })
}

export function fetchCreateDictionaryCategory(data: Api.SystemManage.SaveDictionaryCategoryParams) {
    return request.post<Api.SystemManage.DictionaryCategory>({
        url: '/api/dictionary/categories',
        data,
    })
}

export function fetchUpdateDictionaryCategory(id: string, data: Api.SystemManage.SaveDictionaryCategoryParams) {
    return request.put<Api.SystemManage.DictionaryCategory>({
        url: `/api/dictionary/categories/${id}`,
        data,
    })
}

export function fetchDeleteDictionaryCategory(id: string) {
    return request.del<void>({ url: `/api/dictionary/categories/${id}`, showSuccessMessage: true })
}

export function fetchGetDictionaryItems(categoryId: string) {
    return request.get<Api.SystemManage.DictionaryItem[]>({
        url: `/api/dictionary/categories/${categoryId}/items`,
    })
}

export function fetchCreateDictionaryItem(categoryId: string, data: Api.SystemManage.SaveDictionaryItemParams) {
    return request.post<Api.SystemManage.DictionaryItem>({
        url: `/api/dictionary/categories/${categoryId}/items`,
        data,
    })
}

export function fetchUpdateDictionaryItem(id: string, data: Api.SystemManage.SaveDictionaryItemParams) {
    return request.put<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/items/${id}`, data })
}

export function fetchDeleteDictionaryItem(id: string) {
    return request.del<void>({ url: `/api/dictionary/items/${id}`, showSuccessMessage: true })
}

export interface ScheduledJob {
    id: string
    createdAt: string
    name: string
    cronExpression: string
    requestUrl: string
    requestMethod: string
    requestHeadersJson?: string
    requestBody?: string
    timeoutSeconds: number
    isEnabled: boolean
    status: number
    lastTriggeredAt: string | null
    lastFinishedAt: string | null
    lastError: string
}

export interface ScheduledJobExecution {
    id: string
    scheduledJobId: string
    createdAt: string
    updatedAt: string | null
    startedAt: string
    finishedAt: string | null
    requestUrl: string
    requestMethod: string
    requestHeaders: string
    requestBody: string
    responseStatusCode: number | null
    responseHeaders: string
    responseBody: string
    status: number
    errorMessage: string
}

export type ScheduledJobExecutionSearchParams = DynamicQuery & {
    RequestUrl?: string
    RequestMethod?: string
    ResponseStatusCode?: number
    Status?: number
    ErrorMessage?: string
}

export type SaveScheduledJob = Pick<
    ScheduledJob,
    'name' | 'cronExpression' | 'requestUrl' | 'requestMethod' | 'requestHeadersJson' | 'requestBody' | 'timeoutSeconds' | 'isEnabled'
>

export type ScheduledJobSearchParams = DynamicQuery & {
    Name?: string
    CronExpression?: string
    RequestUrl?: string
    RequestMethod?: string
    IsEnabled?: boolean
    Status?: number
}

export function fetchGetScheduledJobs(params: ScheduledJobSearchParams) {
    const filters: DynamicFilter[] = Object.entries(params)
        .filter(([field, value]) => /^[A-Z]/.test(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: typeof value === 'string' && !['RequestMethod'].includes(field) ? 'Contains' : 'Equal',
            value,
        }))
    if (params.dynamicFilter) filters.push(params.dynamicFilter)
    return request.post<Api.Common.PaginatedResponse<ScheduledJob>>({
        url: '/api/scheduled-job/list',
        data: createDynamicQuery(params.current, params.size, filters, params.sortField, params.sortOrder),
    })
}

export function fetchCreateScheduledJob(data: SaveScheduledJob) {
    return request.post<ScheduledJob>({ url: '/api/scheduled-job', data })
}

export function fetchUpdateScheduledJob(id: string, data: SaveScheduledJob) {
    return request.put<ScheduledJob>({ url: `/api/scheduled-job/${id}`, data })
}

export function fetchDeleteScheduledJob(id: string) {
    return request.del<void>({ url: `/api/scheduled-job/${id}`, showSuccessMessage: true })
}

export function fetchRunScheduledJob(id: string) {
    return request.post<void>({ url: `/api/scheduled-job/${id}/run` })
}

export function fetchScheduledJobExecutions(id: string, params: ScheduledJobExecutionSearchParams) {
    const filters: DynamicFilter[] = Object.entries(params)
        .filter(([field, value]) => /^[A-Z]/.test(field) && value !== undefined && value !== null && value !== '')
        .map(([field, value]) => ({
            field,
            operator: typeof value === 'number' ? 'Equal' : 'Contains',
            value,
        }))
    return request.post<Api.Common.PaginatedResponse<ScheduledJobExecution>>({
        url: `/api/scheduled-job/${id}/executions/list`,
        data: createDynamicQuery(params.current, params.size, filters, params.sortField, params.sortOrder),
    })
}

export function fetchScheduledJobExecutionFilterFields(id: string) {
    return request.get<ListFilterField[]>({ url: `/api/scheduled-job/${id}/executions/filter-fields` })
}

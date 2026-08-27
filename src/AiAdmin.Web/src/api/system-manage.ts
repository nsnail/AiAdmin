import request from '@/utils/http'
import { AppRouteRecord } from '@/types/router'

export type DynamicFilter = {
    field?: string
    operator?: string
    value?: unknown
    logic?: 'And' | 'Or'
    filters?: DynamicFilter[]
}

export type ListFilterResource =
    'api-endpoint' | 'department' | 'dictionary' | 'login-log' | 'menu' | 'message' | 'role' | 'scheduled-job' | 'user' | 'wallet'

export interface DynamicTableQuery {
    current?: number
    size?: number
    dynamicFilter?: DynamicFilter
    sortField?: string
    sortOrder?: 'asc' | 'desc'
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
    control: 'input' | 'select' | 'date' | 'number' | 'user-select'
    span: number
    sort: number
    placeholder: string
    options: Array<{ label: string; value: string }>
    valueType: 'string' | 'number' | 'boolean' | 'date'
    groupCount: boolean
}

export interface ListFilterGroupOption {
    value: unknown
    label: string
    count: number
}

export interface ListFilterGroup {
    field: string
    label: string
    valueType: 'string' | 'number' | 'boolean' | 'date'
    total: number
    options: ListFilterGroupOption[]
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
    const filters: DynamicFilter[] = Object.entries(data)
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
    return request.post<RedisCacheValue>({ url: '/api/redis-cache/value', data })
}

export function fetchDeleteRedisValue(key: string) {
    return request.post<void>({ url: '/api/redis-cache/value/delete', params: { key } })
}

export function fetchUpdateEnabledState(resource: EnabledStateResource, id: string, isEnabled: boolean) {
    return request.post<void>({ url: `/api/enabled-state/${resource}/${id}`, data: { isEnabled } })
}

export function fetchGetListFilterFields(resource: ListFilterResource) {
    return request.get<ListFilterField[]>({ url: `/api/${resource}/filter-fields` })
}

export function fetchGetListFilterGroups(resource: ListFilterResource, dynamicFilter?: DynamicFilter) {
    return request.post<ListFilterGroup[]>({ url: `/api/${resource}/filter-groups`, data: { dynamicFilter } })
}

export function fetchGetDictionaryFilterGroups(categoryId: string, dynamicFilter?: DynamicFilter) {
    return request.post<ListFilterGroup[]>({ url: '/api/dictionary/filter-groups', params: { categoryId }, data: { dynamicFilter } })
}

export function fetchGetLoginLogList(params: DynamicTableQuery) {
    return request.post<Api.Common.PaginatedResponse<LoginLogRecord>>({ url: '/api/login-log/list', data: params })
}

export function fetchGetSavedQueries(route: string) {
    return request.get<SavedQuery[]>({ url: '/api/saved-query', params: { route } })
}

export function fetchSaveQuery(data: { name: string; route: string; dynamicFilter: DynamicFilter; isGlobal: boolean }) {
    return request.post<SavedQuery>({ url: '/api/saved-query', data })
}

export function fetchDeleteSavedQuery(id: string) {
    return request.post<void>({ url: `/api/saved-query/${id}/delete` })
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

type DynamicQuery = DynamicTableQuery

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
export function fetchGetUserList(params: DynamicTableQuery) {
    return request.post<Api.SystemManage.UserList>({
        url: '/api/user/list',
        data: params,
    })
}

export function fetchGetUser(id: string) {
    return request.get<Api.SystemManage.UserListItem>({ url: `/api/user/${id}` })
}

export interface UserExportResult {
    records: Api.SystemManage.UserListItem[]
    limit: number
    total: number
}

export function fetchExportUsers(data: Omit<DynamicTableQuery, 'current' | 'size'>) {
    return request.post<UserExportResult>({ url: '/api/user/export', data })
}

export function fetchCreateUser(data: Api.SystemManage.SaveUserParams) {
    return request.post<Api.SystemManage.UserListItem>({ url: '/api/user', data })
}

export function fetchUpdateUser(id: string, data: Api.SystemManage.UpdateUserParams) {
    return request.post<Api.SystemManage.UserListItem>({ url: `/api/user/${id}`, data })
}

export function fetchUploadUserAvatar(id: string, file: File) {
    const data = new FormData()
    data.append('file', file)
    return request.post<Api.SystemManage.UserListItem>({ url: `/api/user/${id}/avatar`, data })
}

export function fetchDeleteUserAvatar(id: string) {
    return request.post<Api.SystemManage.UserListItem>({ url: `/api/user/${id}/avatar/delete` })
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

export function fetchGetDepartmentList(params: DynamicTableQuery) {
    return request.post<Api.Common.PaginatedResponse<Api.SystemManage.DepartmentTreeItem>>({ url: '/api/department/list', data: params })
}

export function fetchGetSystemMessages(params: DynamicTableQuery) {
    return request.post<Api.Common.PaginatedResponse<Api.SystemManage.SystemMessageListItem>>({ url: '/api/message/list', data: params })
}

export function fetchSendSystemMessage(data: Api.SystemManage.SendSystemMessageParams) {
    return request.post<void>({ url: '/api/message', data, showSuccessMessage: true })
}

export function fetchUpdateSystemMessage(id: string, data: { title: string; content: string }) {
    return request.post<void>({ url: `/api/message/${id}`, data, showSuccessMessage: true })
}

export function fetchDeleteSystemMessage(id: string) {
    return request.post<void>({ url: `/api/message/${id}/delete`, showSuccessMessage: true })
}

export function fetchBatchDeleteSystemMessages(ids: string[]) {
    return request.post<void>({ url: '/api/message/delete', data: ids, showSuccessMessage: true })
}

export function fetchGetSystemMessageRecipients(id: string) {
    return request.get<Api.SystemManage.SystemMessageRecipientItem[]>({ url: `/api/message/${id}/recipients` })
}

export function fetchGetNotifications(current = 1, size = 20) {
    return request.get<Api.SystemManage.UserMessagePageResult>({ url: '/api/notifications', params: { current, size } })
}

export function fetchMarkNotificationRead(id: number) {
    return request.post<void>({ url: `/api/notifications/${id}/read`, data: {} })
}

export function fetchMarkAllNotificationsRead() {
    return request.post<void>({ url: '/api/notifications/read-all', data: {} })
}

export function fetchDeleteNotification(id: number) {
    return request.post<void>({ url: `/api/notifications/${id}/delete` })
}

export function fetchClearNotifications() {
    return request.post<void>({ url: '/api/notifications/clear' })
}

export function fetchCreateDepartment(data: Api.SystemManage.SaveDepartmentParams) {
    return request.post<Api.SystemManage.DepartmentTreeItem>({ url: '/api/department', data })
}

export function fetchUpdateDepartment(id: string, data: Api.SystemManage.SaveDepartmentParams) {
    return request.post<Api.SystemManage.DepartmentTreeItem>({ url: `/api/department/${id}`, data })
}

export function fetchDeleteDepartment(id: string) {
    return request.post<void>({ url: `/api/department/${id}/delete`, showSuccessMessage: true })
}

// 获取角色列表
export function fetchGetRoleList(params: DynamicTableQuery) {
    return request.post<Api.SystemManage.RoleList>({
        url: '/api/role/list',
        data: params,
    })
}

export interface RoleExportResult {
    records: Api.SystemManage.RoleListItem[]
    limit: number
    total: number
}

export function fetchExportRoles(data: Omit<DynamicTableQuery, 'current' | 'size'>) {
    return request.post<RoleExportResult>({ url: '/api/role/export', data })
}

export function fetchCreateRole(data: Api.SystemManage.SaveRoleParams) {
    return request.post<Api.SystemManage.RoleListItem>({ url: '/api/role', data })
}

export function fetchUpdateRole(id: string, data: Api.SystemManage.SaveRoleParams) {
    return request.post<Api.SystemManage.RoleListItem>({ url: `/api/role/${id}`, data })
}

export function fetchDeleteRole(id: string) {
    return request.post<void>({ url: `/api/role/${id}/delete`, showSuccessMessage: true })
}

export function fetchCopyRole(id: string) {
    return request.post<Api.SystemManage.RoleListItem>({ url: `/api/role/${id}/copy` })
}

export function fetchGetRoleMenus(id: string) {
    return request.get<AppRouteRecord[]>({ url: `/api/role/${id}/menus` })
}

export function fetchSaveRoleMenus(id: string, menuIds: string[]) {
    return request.post<void>({ url: `/api/role/${id}/menus`, data: { menuIds } })
}

export function fetchGetRoleApis(id: string) {
    return request.get<string[]>({ url: `/api/role/${id}/apis` })
}

export function fetchSaveRoleApis(id: string, apiIds: string[]) {
    return request.post<void>({ url: `/api/role/${id}/apis`, data: { apiIds } })
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
    return request.post<AppRouteRecord>({ url: `/api/menu/${id}`, data })
}

export function fetchDeleteMenu(id: string) {
    return request.post<void>({ url: `/api/menu/${id}/delete`, showSuccessMessage: true })
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
    return request.post<Api.SystemManage.DictionaryCategory>({
        url: `/api/dictionary/categories/${id}`,
        data,
    })
}

export function fetchDeleteDictionaryCategory(id: string) {
    return request.post<void>({ url: `/api/dictionary/categories/${id}/delete`, showSuccessMessage: true })
}

export function fetchGetDictionaryItems(categoryId: string) {
    return request.get<Api.SystemManage.DictionaryItem[]>({
        url: `/api/dictionary/categories/${categoryId}/items`,
    })
}

export function fetchGetDictionaryItemList(
    categoryId: string,
    data: { current: number; size: number; dynamicFilter?: DynamicFilter; sortField?: string; sortOrder?: 'asc' | 'desc' },
) {
    return request.post<Api.Common.PaginatedResponse<Api.SystemManage.DictionaryItem>>({
        url: `/api/dictionary/categories/${categoryId}/items/list`,
        data,
    })
}

export function fetchCreateDictionaryItem(categoryId: string, data: Api.SystemManage.SaveDictionaryItemParams) {
    return request.post<Api.SystemManage.DictionaryItem>({
        url: `/api/dictionary/categories/${categoryId}/items`,
        data,
    })
}

export function fetchUpdateDictionaryItem(id: string, data: Api.SystemManage.SaveDictionaryItemParams) {
    return request.post<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/items/${id}`, data })
}

export function fetchDeleteDictionaryItem(id: string) {
    return request.post<void>({ url: `/api/dictionary/items/${id}/delete`, showSuccessMessage: true })
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
    startedAt: string
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

export function fetchGetScheduledJobs(params: DynamicTableQuery) {
    return request.post<Api.Common.PaginatedResponse<ScheduledJob>>({ url: '/api/scheduled-job/list', data: params })
}

export function fetchCreateScheduledJob(data: SaveScheduledJob) {
    return request.post<ScheduledJob>({ url: '/api/scheduled-job', data })
}

export function fetchUpdateScheduledJob(id: string, data: SaveScheduledJob) {
    return request.post<ScheduledJob>({ url: `/api/scheduled-job/${id}`, data })
}

export function fetchDeleteScheduledJob(id: string) {
    return request.post<void>({ url: `/api/scheduled-job/${id}/delete`, showSuccessMessage: true })
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

export function fetchScheduledJobExecutionFilterGroups(id: string, dynamicFilter?: DynamicFilter) {
    return request.post<ListFilterGroup[]>({ url: `/api/scheduled-job/${id}/executions/filter-groups`, data: { dynamicFilter } })
}
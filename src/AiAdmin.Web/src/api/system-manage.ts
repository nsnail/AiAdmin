import request from '@/utils/http'
import { AppRouteRecord } from '@/types/router'

type DynamicFilter = {
  field?: string
  operator?: string
  value?: unknown
  logic?: 'And' | 'Or'
  filters?: DynamicFilter[]
}

type DynamicQuery = {
  current?: number
  size?: number
  dynamicFilter?: DynamicFilter
}

function createDynamicQuery(current: number | undefined, size: number | undefined, filters: DynamicFilter[]): DynamicQuery {
  return {
    current,
    size,
    ...(filters.length > 0 ? { dynamicFilter: { logic: 'And', filters } } : {})
  }
}

function getTextFilter(field: string, value: string | undefined): DynamicFilter | undefined {
  const text = value?.trim()
  return text ? { field, operator: 'Contains', value: text } : undefined
}

// 获取用户列表
export function fetchGetUserList(params: Api.SystemManage.UserSearchParams) {
  const filters = [
    getTextFilter('UserName', params.userName),
    getTextFilter('Phone', params.userPhone),
    getTextFilter('Email', params.userEmail),
    params.userGender ? { field: 'Gender', operator: 'Equal', value: params.userGender } : undefined,
    params.status === '1'
      ? { field: 'IsEnabled', operator: 'Equal', value: true }
      : params.status === '2'
        ? { field: 'IsEnabled', operator: 'Equal', value: false }
        : undefined
  ].filter((filter): filter is DynamicFilter => Boolean(filter))

  return request.post<Api.SystemManage.UserList>({
    url: '/api/user/list',
    data: createDynamicQuery(params.current, params.size, filters)
  })
}

export function fetchCreateUser(data: Api.SystemManage.SaveUserParams) {
  return request.post<Api.SystemManage.UserListItem>({ url: '/api/user', data })
}

export function fetchUpdateUser(id: string, data: Api.SystemManage.SaveUserParams) {
  return request.put<Api.SystemManage.UserListItem>({ url: `/api/user/${id}`, data })
}

export function fetchDeleteUser(id: string) {
  return request.del<void>({ url: `/api/user/${id}`, showSuccessMessage: true })
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

export function fetchUpdateDepartment(
  id: string,
  data: Api.SystemManage.SaveDepartmentParams
) {
  return request.put<Api.SystemManage.DepartmentTreeItem>({ url: `/api/department/${id}`, data })
}

export function fetchDeleteDepartment(id: string) {
  return request.del<void>({ url: `/api/department/${id}`, showSuccessMessage: true })
}

// 获取角色列表
export function fetchGetRoleList(params: Api.SystemManage.RoleSearchParams) {
  const filters = [
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
          : undefined
  ].filter((filter): filter is DynamicFilter => Boolean(filter))

  return request.post<Api.SystemManage.RoleList>({
    url: '/api/role/list',
    data: createDynamicQuery(params.current, params.size, filters)
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

export function fetchGetApiEndpointList(dynamicFilter?: DynamicFilter) {
  return request.post<Api.SystemManage.ApiEndpointItem[]>({
    url: '/api/api-endpoint/list',
    data: dynamicFilter ? { dynamicFilter } : {}
  })
}

export function fetchSyncApiEndpoints() {
  return request.post<Api.SystemManage.ApiSyncResult>({ url: '/api/api-endpoint/sync' })
}

export function fetchGetCurrentMenuNames() {
  return request.get<AppRouteRecord[]>({ url: '/api/menu/current' })
}

// 获取菜单列表
export function fetchGetMenuList(dynamicFilter?: DynamicFilter) {
  return request.post<AppRouteRecord[]>({
    url: '/api/menu/list',
    data: dynamicFilter ? { dynamicFilter } : {}
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

export function fetchCreateDictionaryCategory(data: Api.SystemManage.SaveDictionaryCategoryParams) {
  return request.post<Api.SystemManage.DictionaryCategory>({ url: '/api/dictionary/categories', data })
}

export function fetchUpdateDictionaryCategory(id: string, data: Api.SystemManage.SaveDictionaryCategoryParams) {
  return request.put<Api.SystemManage.DictionaryCategory>({ url: `/api/dictionary/categories/${id}`, data })
}

export function fetchDeleteDictionaryCategory(id: string) {
  return request.del<void>({ url: `/api/dictionary/categories/${id}`, showSuccessMessage: true })
}

export function fetchGetDictionaryItems(categoryId: string) {
  return request.get<Api.SystemManage.DictionaryItem[]>({ url: `/api/dictionary/categories/${categoryId}/items` })
}

export function fetchCreateDictionaryItem(categoryId: string, data: Api.SystemManage.SaveDictionaryItemParams) {
  return request.post<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/categories/${categoryId}/items`, data })
}

export function fetchUpdateDictionaryItem(id: string, data: Api.SystemManage.SaveDictionaryItemParams) {
  return request.put<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/items/${id}`, data })
}

export function fetchDeleteDictionaryItem(id: string) {
  return request.del<void>({ url: `/api/dictionary/items/${id}`, showSuccessMessage: true })
}

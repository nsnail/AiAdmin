import request from '@/utils/http'
import { AppRouteRecord } from '@/types/router'

// 获取用户列表
export function fetchGetUserList(params: Api.SystemManage.UserSearchParams) {
  return request.get<Api.SystemManage.UserList>({
    url: '/api/user/list',
    params
  })
}

export function fetchCreateUser(data: Api.SystemManage.SaveUserParams) {
  return request.post<Api.SystemManage.UserListItem>({ url: '/api/user', data })
}

export function fetchUpdateUser(id: number, data: Api.SystemManage.SaveUserParams) {
  return request.put<Api.SystemManage.UserListItem>({ url: `/api/user/${id}`, data })
}

export function fetchDeleteUser(id: number) {
  return request.del<void>({ url: `/api/user/${id}`, showSuccessMessage: true })
}

export function fetchGetUserRoles() {
  return request.get<Api.SystemManage.RoleListItem[]>({ url: '/api/user/roles' })
}

export function fetchGetDepartmentTree() {
  return request.get<Api.SystemManage.DepartmentTreeItem[]>({ url: '/api/department/tree' })
}

export function fetchCreateDepartment(data: Api.SystemManage.SaveDepartmentParams) {
  return request.post<Api.SystemManage.DepartmentTreeItem>({ url: '/api/department', data })
}

export function fetchUpdateDepartment(
  id: number,
  data: Api.SystemManage.SaveDepartmentParams
) {
  return request.put<Api.SystemManage.DepartmentTreeItem>({ url: `/api/department/${id}`, data })
}

export function fetchDeleteDepartment(id: number) {
  return request.del<void>({ url: `/api/department/${id}`, showSuccessMessage: true })
}

// 获取角色列表
export function fetchGetRoleList(params: Api.SystemManage.RoleSearchParams) {
  return request.get<Api.SystemManage.RoleList>({
    url: '/api/role/list',
    params
  })
}

export function fetchCreateRole(data: Api.SystemManage.SaveRoleParams) {
  return request.post<Api.SystemManage.RoleListItem>({ url: '/api/role', data })
}

export function fetchUpdateRole(id: number, data: Api.SystemManage.SaveRoleParams) {
  return request.put<Api.SystemManage.RoleListItem>({ url: `/api/role/${id}`, data })
}

export function fetchDeleteRole(id: number) {
  return request.del<void>({ url: `/api/role/${id}`, showSuccessMessage: true })
}

export function fetchGetRoleMenus(id: number) {
  return request.get<AppRouteRecord[]>({ url: `/api/role/${id}/menus` })
}

export function fetchSaveRoleMenus(id: number, menuIds: number[]) {
  return request.put<void>({ url: `/api/role/${id}/menus`, data: { menuIds } })
}

export function fetchGetRoleApis(id: number) {
  return request.get<number[]>({ url: `/api/role/${id}/apis` })
}

export function fetchSaveRoleApis(id: number, apiIds: number[]) {
  return request.put<void>({ url: `/api/role/${id}/apis`, data: { apiIds } })
}

export function fetchGetApiEndpointList() {
  return request.get<Api.SystemManage.ApiEndpointItem[]>({ url: '/api/api-endpoint/list' })
}

export function fetchSyncApiEndpoints() {
  return request.post<Api.SystemManage.ApiSyncResult>({ url: '/api/api-endpoint/sync' })
}

export function fetchUpdateApiAnonymous(id: number, allowAnonymous: boolean) {
  return request.put<void>({ url: `/api/api-endpoint/${id}/anonymous`, data: { allowAnonymous } })
}

export function fetchGetCurrentMenuNames() {
  return request.get<AppRouteRecord[]>({ url: '/api/menu/current' })
}

// 获取菜单列表
export function fetchGetMenuList() {
  return request.get<AppRouteRecord[]>({
    url: '/api/menu/list'
  })
}

export function fetchCreateMenu(data: Record<string, any>) {
  return request.post<AppRouteRecord>({ url: '/api/menu', data })
}

export function fetchUpdateMenu(id: number, data: Record<string, any>) {
  return request.put<AppRouteRecord>({ url: `/api/menu/${id}`, data })
}

export function fetchDeleteMenu(id: number) {
  return request.del<void>({ url: `/api/menu/${id}`, showSuccessMessage: true })
}

export function fetchGetDictionaryCategories() {
  return request.get<Api.SystemManage.DictionaryCategory[]>({ url: '/api/dictionary/categories' })
}

export function fetchCreateDictionaryCategory(data: Api.SystemManage.SaveDictionaryCategoryParams) {
  return request.post<Api.SystemManage.DictionaryCategory>({ url: '/api/dictionary/categories', data })
}

export function fetchUpdateDictionaryCategory(id: number, data: Api.SystemManage.SaveDictionaryCategoryParams) {
  return request.put<Api.SystemManage.DictionaryCategory>({ url: `/api/dictionary/categories/${id}`, data })
}

export function fetchDeleteDictionaryCategory(id: number) {
  return request.del<void>({ url: `/api/dictionary/categories/${id}`, showSuccessMessage: true })
}

export function fetchGetDictionaryItems(categoryId: number) {
  return request.get<Api.SystemManage.DictionaryItem[]>({ url: `/api/dictionary/categories/${categoryId}/items` })
}

export function fetchCreateDictionaryItem(categoryId: number, data: Api.SystemManage.SaveDictionaryItemParams) {
  return request.post<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/categories/${categoryId}/items`, data })
}

export function fetchUpdateDictionaryItem(id: number, data: Api.SystemManage.SaveDictionaryItemParams) {
  return request.put<Api.SystemManage.DictionaryItem>({ url: `/api/dictionary/items/${id}`, data })
}

export function fetchDeleteDictionaryItem(id: number) {
  return request.del<void>({ url: `/api/dictionary/items/${id}`, showSuccessMessage: true })
}

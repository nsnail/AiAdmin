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

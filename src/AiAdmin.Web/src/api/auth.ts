import request from '@/utils/http'

/**
 * 登录
 * @param params 登录参数
 * @returns 登录响应
 */
export function fetchLogin(params: Api.Auth.LoginParams) {
  return request.post<Api.Auth.LoginResponse>({
    url: '/api/auth/login',
    params
    // showSuccessMessage: true // 显示成功消息
    // showErrorMessage: false // 不显示错误消息
  })
}

/**
 * 获取用户信息
 * @returns 用户信息
 */
export function fetchGetUserInfo() {
  return request.get<Api.Auth.UserInfo>({
    url: '/api/user/info'
    // 自定义请求头
    // headers: {
    //   'X-Custom-Header': 'your-custom-value'
    // }
  })
}

/**
 * 更新当前用户资料
 * @param data 个人资料参数
 * @returns 更新后的用户信息
 */
export function fetchUpdateUserProfile(data: Api.Auth.UpdateProfileParams) {
  return request.put<Api.Auth.UserInfo>({
    url: '/api/user/profile',
    data
  })
}

/**
 * 修改当前用户密码
 * @param data 密码修改参数
 * @returns 修改结果
 */
export function fetchChangeUserPassword(data: Api.Auth.ChangePasswordParams) {
  return request.put<Record<string, never>>({
    url: '/api/user/password',
    data
  })
}

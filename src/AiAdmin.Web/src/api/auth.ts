import request from '@/utils/http'

/**
 * 登录
 * @param params 登录参数
 * @returns 登录响应
 */
export function fetchLogin(params: Api.Auth.LoginParams) {
    return request.post<Api.Auth.LoginResponse>({
        url: '/api/auth/login',
        params,
        // showSuccessMessage: true // 显示成功消息
        // showErrorMessage: false // 不显示错误消息
    })
}

export function fetchLoginConfig() {
    return request.get<Api.Auth.LoginConfig>({ url: '/api/auth/config' })
}

export function fetchLoginChallenge() {
    return request.get<Api.Auth.LoginChallenge>({ url: '/api/auth/challenge' })
}

export function fetchRegister(params: Api.Auth.RegisterParams) {
    return request.post<Record<string, never>>({ url: '/api/auth/register', data: params })
}

export function fetchRegisterCode(email: string, puzzleTicket: string) {
    return request.post<Record<string, never>>({
        url: '/api/auth/register-code',
        data: { email, puzzleTicket },
    })
}

export function fetchForgotPasswordCode(email: string) {
    return request.post<Record<string, never>>({ url: '/api/auth/forgot-password/code', data: { email } })
}

export function fetchResetPassword(data: Api.Auth.ResetPasswordParams) {
    return request.post<Record<string, never>>({ url: '/api/auth/forgot-password/reset', data })
}

export function fetchRegisterPuzzle() {
    return request.get<Api.Auth.RegisterPuzzle>({ url: '/api/auth/register-puzzle' })
}

export function fetchVerifyRegisterPuzzle(challengeId: string, offsetX: number, email: string) {
    return request.post<Api.Auth.VerifyRegisterPuzzleResult>({
        url: '/api/auth/register-puzzle/verify',
        data: { challengeId, offsetX, email },
    })
}

/**
 * 获取用户信息
 * @returns 用户信息
 */
export function fetchGetUserInfo() {
    return request.get<Api.Auth.UserInfo>({
        url: '/api/user/info',
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
        data,
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
        data,
    })
}
/**
 * 令牌续签的应用层：收到响应头中的新令牌后替换本地存储，续签令牌平时不传输，降低泄露风险
 */
import { useUserStore } from '@/store/modules/user'
import { resetRefreshTokenCarried } from './renewal-policy'

export { markRefreshTokenCarried, readTokenExpiry, shouldCarryRefreshToken } from './renewal-policy'

/**
 * 读取响应头中的新令牌并替换本地存储，签名密钥随会话不变，保持原值
 * @param headers 响应头
 */
export function applyRenewedTokens(headers: Record<string, unknown> | undefined): void {
    const token = headers?.['x-new-token']
    const refreshToken = headers?.['x-new-refresh-token']
    if (typeof token !== 'string' || !token || typeof refreshToken !== 'string' || !refreshToken) return
    const userStore = useUserStore()
    userStore.setToken(token, refreshToken, userStore.signKey)
    resetRefreshTokenCarried()
}
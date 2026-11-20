/**
 * 令牌续签的纯逻辑（不依赖 store，便于测试）：判断访问令牌是否临近到期、本次请求是否需要携带续签令牌。
 * 续签令牌平时不传输，只在临近到期时携带，降低泄露风险
 */
/** 临近到期的阈值，应小于服务端访问令牌有效期（默认 120 分钟） */
const RENEW_BEFORE_MS = 30 * 60 * 1000
/** 续签请求的最小间隔，避免服务端尚未应答时每个并发请求都携带续签令牌 */
const CARRY_INTERVAL_MS = 15 * 1000

let lastCarriedAt = 0

/**
 * 解析 JWT 的到期时间
 * @param token 访问令牌
 * @returns 到期时间戳（毫秒），无法解析时为 undefined
 */
export function readTokenExpiry(token: string): number | undefined {
    try {
        const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
        const exp = JSON.parse(atob(payload)).exp
        return typeof exp === 'number' ? exp * 1000 : undefined
    } catch {
        return undefined
    }
}

/**
 * 判断本次请求是否需要携带续签令牌
 * @param accessToken 当前访问令牌
 * @param refreshToken 当前续签令牌
 * @param now 当前时间戳（毫秒）
 * @returns 临近到期且未在短时间内携带过时为 true
 */
export function shouldCarryRefreshToken(accessToken: string, refreshToken: string, now: number = Date.now()): boolean {
    if (!accessToken || !refreshToken || now - lastCarriedAt < CARRY_INTERVAL_MS) return false
    const expiry = readTokenExpiry(accessToken)
    return expiry !== undefined && expiry - now <= RENEW_BEFORE_MS
}

/** 重置携带间隔，收到新令牌后立即允许下一次续签 */
export function resetRefreshTokenCarried(): void {
    lastCarriedAt = 0
}

/** 记录本次已携带续签令牌 */
export function markRefreshTokenCarried(now: number = Date.now()): void {
    lastCarriedAt = now
}
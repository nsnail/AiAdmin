/**
 * 锁屏凭证保管：锁定时把访问令牌、刷新令牌和登录状态从持久化的用户 store 中移走，只保存在内存里，
 * 解锁密码校验通过后再还原。锁定期间刷新或关闭页面，内存中的凭证随之丢失，只能重新登录。
 *
 * - 解锁密码只以加盐 SHA-256 摘要的形式保存在内存中，不写入任何存储
 * - 锁定状态同时写入 sessionStorage 标记，仅用于刷新后提示“已因锁屏退出”，不包含任何凭证
 */
import { useUserStore } from '@/store/modules/user'
import { isScreenLocked } from './state'
import { startRealtime, stopRealtime } from '@/utils/realtime'

export { isScreenLocked }

/** 被保管的凭证，仅存在于内存 */
interface LockVault {
    accessToken: string
    refreshToken: string
    salt: string
    digest: string
}

let vault: LockVault | null = null

/**
 * 计算加盐摘要
 * @param password 明文密码
 * @param salt 随机盐
 * @returns 十六进制摘要
 */
async function digestOf(password: string, salt: string): Promise<string> {
    const bytes = new TextEncoder().encode(`${salt}:${password}`)
    const hash = await crypto.subtle.digest('SHA-256', bytes)
    return Array.from(new Uint8Array(hash), (x) => x.toString(16).padStart(2, '0')).join('')
}

/**
 * 锁定屏幕：移走令牌和登录状态并断开实时连接
 * @param password 解锁密码
 */
export async function lockScreen(password: string): Promise<void> {
    const userStore = useUserStore()
    if (isScreenLocked.value || !userStore.accessToken) return
    const salt = crypto.randomUUID()
    vault = {
        accessToken: userStore.accessToken,
        refreshToken: userStore.refreshToken,
        salt,
        digest: await digestOf(password, salt),
    }
    // 先断开实时连接，避免令牌被清掉后断线重连触发 401
    stopRealtime()
    userStore.accessToken = ''
    userStore.refreshToken = ''
    userStore.isLogin = false
    isScreenLocked.value = true
}

/**
 * 校验解锁密码，通过后还原令牌和登录状态并恢复实时连接
 * @param password 用户输入的解锁密码
 * @returns 密码正确且已解锁时为 true
 */
export async function unlockScreen(password: string): Promise<boolean> {
    if (!vault) return false
    if ((await digestOf(password, vault.salt)) !== vault.digest) return false
    const userStore = useUserStore()
    userStore.accessToken = vault.accessToken
    userStore.refreshToken = vault.refreshToken
    userStore.isLogin = true
    vault = null
    isScreenLocked.value = false
    startRealtime()
    return true
}

/** 放弃解锁并退出登录：丢弃内存中的凭证后走正常退出流程 */
export function abandonLock(): void {
    vault = null
    isScreenLocked.value = false
    useUserStore().logOut()
}
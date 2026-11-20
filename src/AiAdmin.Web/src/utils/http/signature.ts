/**
 * 请求签名：为已登录请求生成 X-Timestamp、X-Nonce、X-Signature，与后端 RequestSignature 的算法保持一致
 *
 * 待签名串：METHOD \n 原始请求目标(路径+查询) \n 毫秒时间戳 \n 随机数 \n 请求体 SHA-256（multipart 为 UNSIGNED-PAYLOAD）
 * 签名：HMAC-SHA256(登录时下发的 signKey, 待签名串)，小写十六进制
 */
import CryptoJS from 'crypto-js'

/** 无法计算摘要的请求体使用的占位摘要 */
const UNSIGNED_PAYLOAD = 'UNSIGNED-PAYLOAD'

/** 生成 32 位十六进制随机数 */
function createNonce(): string {
    const bytes = new Uint8Array(16)
    crypto.getRandomValues(bytes)
    return Array.from(bytes, (x) => x.toString(16).padStart(2, '0')).join('')
}

/**
 * 计算请求体摘要
 * @param body 已序列化的请求体
 * @returns 小写十六进制摘要或占位摘要
 */
function hashBody(body: unknown): string {
    if (body === undefined || body === null || body === '') return CryptoJS.SHA256('').toString(CryptoJS.enc.Hex)
    if (typeof body === 'string') return CryptoJS.SHA256(body).toString(CryptoJS.enc.Hex)
    return UNSIGNED_PAYLOAD
}

/**
 * 生成签名请求头
 * @param signKey 登录时下发的签名密钥，为空时不签名
 * @param method HTTP 方法
 * @param uri 请求地址，可为相对地址或绝对地址
 * @param body 已序列化的请求体
 * @returns 签名请求头，无签名密钥时为空对象
 */
export function createSignatureHeaders(signKey: string, method: string, uri: string, body: unknown): Record<string, string> {
    if (!signKey) return {}
    const url = new URL(uri, window.location.href)
    const timestamp = String(Date.now())
    const nonce = createNonce()
    const content = [method.toUpperCase(), url.pathname + url.search, timestamp, nonce, hashBody(body)].join('\n')
    const signature = CryptoJS.HmacSHA256(content, signKey).toString(CryptoJS.enc.Hex)
    return { 'X-Timestamp': timestamp, 'X-Nonce': nonce, 'X-Signature': signature }
}
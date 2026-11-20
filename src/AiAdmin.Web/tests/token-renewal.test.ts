import assert from 'node:assert/strict'
import { test } from 'node:test'
import { readTokenExpiry, shouldCarryRefreshToken } from '../src/utils/http/renewal-policy'

/** 构造只含 exp 的测试令牌 */
const tokenExpiringAt = (expMs: number) => `h.${Buffer.from(JSON.stringify({ exp: Math.floor(expMs / 1000) })).toString('base64url')}.s`

test('reads the expiry of a token', () => {
    assert.equal(readTokenExpiry(tokenExpiringAt(1_700_000_000_000)), 1_700_000_000_000)
    assert.equal(readTokenExpiry('not-a-token'), undefined)
})

test('carries the refresh token only close to expiry', () => {
    const now = 1_700_000_000_000
    assert.equal(shouldCarryRefreshToken(tokenExpiringAt(now + 60 * 60 * 1000), 'r', now), false)
    assert.equal(shouldCarryRefreshToken(tokenExpiringAt(now + 10 * 60 * 1000), 'r', now), true)
    assert.equal(shouldCarryRefreshToken(tokenExpiringAt(now + 10 * 60 * 1000), '', now), false)
    assert.equal(shouldCarryRefreshToken('', 'r', now), false)
})
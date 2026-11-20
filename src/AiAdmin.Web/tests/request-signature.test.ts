import assert from 'node:assert/strict'
import { test } from 'node:test'
import CryptoJS from 'crypto-js'

// 与后端 RequestSignature 使用同一固定向量，保证前后端算法一致
test('request signature matches backend vector', () => {
    const bodyHash = CryptoJS.SHA256('{"a":1}').toString(CryptoJS.enc.Hex)
    const content = ['POST', '/api/x?y=1', '1700000000000', 'n'.repeat(16), bodyHash].join('\n')
    assert.equal(CryptoJS.HmacSHA256(content, 'key').toString(CryptoJS.enc.Hex), 'd856c5c8fb358b385d24a5189356ed19dfdd7f5ed05d646d6dbad197baefa344')
})
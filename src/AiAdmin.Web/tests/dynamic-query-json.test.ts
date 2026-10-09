import assert from 'node:assert/strict'
import { test } from 'node:test'
import { parseDynamicQueryJson } from '../src/utils/json/dynamic-query'

test('normalizes nested query keys and preserves business values', () => {
    assert.deepEqual(
        parseDynamicQueryJson('{"CURRENT":2,"DYNAMICFILTER":{"LoGiC":"Or","FILTERS":[{"FIELD":"Name","OPERATOR":"Equal","VALUE":{"AbC":false}}]}}'),
        {
            current: 2,
            dynamicFilter: { logic: 'Or', filters: [{ field: 'Name', operator: 'Equal', value: { AbC: false } }] },
        },
    )
})

test('uses the last duplicate structure key', () => {
    assert.deepEqual(parseDynamicQueryJson('{"dynamicFilter":{"FIELD":"Old"},"DYNAMICFILTER":{"Field":"New"}}'), {
        dynamicFilter: { field: 'New' },
    })
})

test('preserves basic filter field names and invalid structures for validation', () => {
    assert.deepEqual(parseDynamicQueryJson('{"IsEnabled":false,"Name":"AbC"}'), { IsEnabled: false, Name: 'AbC' })
    assert.deepEqual(parseDynamicQueryJson('{"DYNAMICFILTER":{"FILTERS":"invalid"}}'), { dynamicFilter: { filters: 'invalid' } })
    assert.equal(parseDynamicQueryJson('null'), null)
    assert.throws(() => parseDynamicQueryJson('{'), SyntaxError)
})
import assert from 'node:assert/strict'
import test from 'node:test'

import { existsInMenu } from '../src/utils/navigation/route'
import type { AppRouteRecord } from '../src/types/router'

type Item = Partial<AppRouteRecord>

const menus: Item[] = [
    { path: '/dashboard', children: [{ path: 'console' }, { path: 'business' }] },
    { path: '/business', children: [{ path: 'tg-device' }] },
    { path: '/outside' },
]

test('绝对路径的一级菜单可以匹配', () => {
    assert.equal(existsInMenu('/outside', menus as AppRouteRecord[]), true)
    assert.equal(existsInMenu('/dashboard', menus as AppRouteRecord[]), true)
})

test('相对路径的子菜单按父级拼接后匹配', () => {
    assert.equal(existsInMenu('/dashboard/business', menus as AppRouteRecord[]), true)
    assert.equal(existsInMenu('/business/tg-device', menus as AppRouteRecord[]), true)
})

test('同名子路径不会串到别的父级下', () => {
    assert.equal(existsInMenu('/dashboard/tg-device', menus as AppRouteRecord[]), false)
    assert.equal(existsInMenu('/business/console', menus as AppRouteRecord[]), false)
})

test('不在菜单中的路径不匹配，空菜单不匹配', () => {
    assert.equal(existsInMenu('/not-exists', menus as AppRouteRecord[]), false)
    assert.equal(existsInMenu('/dashboard/console', [] as AppRouteRecord[]), false)
})
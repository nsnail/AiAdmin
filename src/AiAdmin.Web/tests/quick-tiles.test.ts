import assert from 'node:assert/strict'
import test from 'node:test'

import { collectTileCandidates, MAX_QUICK_TILES, sanitizeTiles } from '../src/utils/navigation/quickTiles'
import type { AppRouteRecord } from '../src/types/router'

/** 模拟后端下发的菜单树：目录、普通页面、隐藏页面、外链和 iframe 页面 */
const menus = [
    {
        path: '/dashboard',
        name: 'Dashboard',
        meta: { title: 'menus.dashboard.title' },
        children: [
            { path: 'tiles', name: 'QuickTiles', component: '/dashboard/tiles', meta: { title: 'menus.dashboard.tiles', icon: 'ri:apps-2-line' } },
        ],
    },
    {
        path: '/permission',
        name: 'Permission',
        meta: { title: 'menus.permission.title' },
        children: [
            {
                path: 'user',
                name: 'User',
                component: '/permission/user',
                meta: { title: 'menus.permission.user', icon: 'ri:user-line' },
            },
            { path: '/permission/role', name: 'Role', component: '/permission/role', meta: { title: 'menus.permission.role' } },
        ],
    },
    {
        path: '/system-management',
        name: 'System',
        meta: { title: 'menus.system.title' },
        children: [
            { path: 'user-center', name: 'UserCenter', component: '/system/user-center', meta: { title: 'menus.system.userCenter', isHide: true } },
            { path: 'docs', name: 'Docs', meta: { title: 'docs', link: 'https://example.com' } },
            { path: 'frame', name: 'Frame', component: '/frame', meta: { title: 'frame', isIframe: true } },
        ],
    },
] as unknown as AppRouteRecord[]

test('展平菜单树，目录、隐藏页面、外链和 iframe 不会成为候选', () => {
    const paths = collectTileCandidates(menus).map((x) => x.path)
    assert.deepEqual(paths, ['/dashboard/tiles', '/permission/user', '/permission/role'])
})

test('候选页面携带标题、图标和所属目录标题', () => {
    const device = collectTileCandidates(menus).find((x) => x.path === '/permission/user')
    assert.deepEqual(device, {
        path: '/permission/user',
        title: 'menus.permission.user',
        icon: 'ri:user-line',
        groupTitle: 'menus.permission.title',
    })
})

test('子菜单写成绝对路径时不会重复拼接父级', () => {
    assert.ok(collectTileCandidates(menus).some((x) => x.path === '/permission/role'))
})

test('清洗已保存的磁贴：去掉无权限页面和重复项，保持顺序', () => {
    const candidates = collectTileCandidates(menus)
    const saved = ['/permission/user', '/dashboard/console', '/permission/user', '/dashboard/tiles']
    assert.deepEqual(sanitizeTiles(saved, candidates), ['/permission/user', '/dashboard/tiles'])
})

test('已保存的内容不是数组或含非字符串时安全忽略', () => {
    const candidates = collectTileCandidates(menus)
    assert.deepEqual(sanitizeTiles(null, candidates), [])
    assert.deepEqual(sanitizeTiles({ a: 1 }, candidates), [])
    assert.deepEqual(sanitizeTiles([1, null, '/dashboard/tiles'], candidates), ['/dashboard/tiles'])
})

test('磁贴数量不超过上限', () => {
    const many = Array.from({ length: MAX_QUICK_TILES + 5 }, (_, i) => ({
        path: `/p${i}`,
        name: `P${i}`,
        component: '/x',
        meta: { title: `t${i}` },
    })) as unknown as AppRouteRecord[]
    const candidates = collectTileCandidates(many)
    assert.equal(
        sanitizeTiles(
            candidates.map((x) => x.path),
            candidates,
        ).length,
        MAX_QUICK_TILES,
    )
})
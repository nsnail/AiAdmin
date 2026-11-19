import assert from 'node:assert/strict'
import test from 'node:test'

import fastEnterConfig from '../src/config/modules/fastEnter'
import { isFastEnterVisible } from '../src/utils/navigation/fastEnter'
import type { AppRouteRecord } from '../src/types/router'

/** 模拟后端按角色下发的菜单树：管理员有客服工作台和系统设置，普通用户没有 */
const adminMenu = [
    { path: '/dashboard', name: 'Dashboard', children: [{ path: 'console', name: 'Console' }] },
    {
        path: '/system-management',
        name: 'System',
        children: [
            { path: 'support-chat', name: 'SupportChat' },
            { path: 'settings', name: 'SystemSettings' },
            { path: 'user-center', name: 'UserCenter' },
        ],
    },
    { path: '/development', name: 'Development', children: [{ path: 'change-log', name: 'ChangeLog' }] },
] as unknown as AppRouteRecord[]
const userMenu = [
    { path: '/dashboard', name: 'Dashboard', children: [{ path: 'console', name: 'Console' }] },
    { path: '/system-management', name: 'System', children: [{ path: 'user-center', name: 'UserCenter' }] },
] as unknown as AppRouteRecord[]

const visibleNames = (menu: AppRouteRecord[]) =>
    [...fastEnterConfig.applications, ...fastEnterConfig.quickLinks]
        .filter((item) => isFastEnterVisible(item, menu))
        .map((item) => item.routeName ?? item.action)

test('没有声明 menuPath 的动作条目（站内信通知、在线客服）对所有账号可见', () => {
    for (const menu of [adminMenu, userMenu, []]) {
        assert.ok(visibleNames(menu).includes('openNotice'))
        assert.ok(visibleNames(menu).includes('openChat'))
    }
})

test('声明了 menuPath 的条目只对拥有该菜单的账号可见', () => {
    assert.ok(visibleNames(adminMenu).includes('SystemSettings'))
    assert.ok(!visibleNames(userMenu).includes('SystemSettings'))
})

test('菜单尚未加载时，需要菜单权限的条目都隐藏', () => {
    // 只剩不需要菜单权限的条目：两个动作入口，以及登录、注册、忘记密码三个静态路由链接
    assert.deepEqual(visibleNames([]), ['openNotice', 'openChat', 'Login', 'Register', 'ForgetPassword'])
})

test('快速入口只保留本系统存在的入口，不含模板遗留的无效路由和外部链接', () => {
    const all = [...fastEnterConfig.applications, ...fastEnterConfig.quickLinks]
    for (const removed of ['Fireworks', 'Chat', 'Pricing', 'ArticleComment']) {
        assert.ok(!all.some((item) => item.routeName === removed), `${removed} 应已移除`)
    }
    assert.ok(!all.some((item) => item.link), '不应保留外部链接')
})

test('新增的三个入口都已配置，且有动作或路由', () => {
    const find = (key: string) => fastEnterConfig.applications.find((item) => item.nameKey === key)
    assert.equal(find('fastEnter.notice.title')?.action, 'openNotice')
    // 在线客服是客户视角的咨询入口，打开聊天窗口，而不是跳转客服工作台页面
    assert.equal(find('fastEnter.supportChat.title')?.action, 'openChat')
    assert.equal(find('fastEnter.supportChat.title')?.routeName, undefined)
    assert.equal(find('fastEnter.settings.title')?.routeName, 'SystemSettings')
})

test('每个条目都有 i18n 键', () => {
    for (const item of [...fastEnterConfig.applications, ...fastEnterConfig.quickLinks]) {
        assert.ok(item.nameKey, `${item.name} 缺少 nameKey`)
    }
})

test('快速链接保留登录、注册、忘记密码，且无需菜单权限、对所有账号可见', () => {
    for (const routeName of ['Login', 'Register', 'ForgetPassword']) {
        const link = fastEnterConfig.quickLinks.find((item) => item.routeName === routeName)
        assert.ok(link, `${routeName} 应保留在快速链接中`)
        assert.equal(link.menuPath, undefined)
        assert.ok(isFastEnterVisible(link, []))
    }
})

test('快速链接顺序为登录、注册、忘记密码、个人中心', () => {
    const order = [...fastEnterConfig.quickLinks].sort((a, b) => (a.order ?? 0) - (b.order ?? 0)).map((item) => item.routeName)
    assert.deepEqual(order, ['Login', 'Register', 'ForgetPassword', 'UserCenter'])
})
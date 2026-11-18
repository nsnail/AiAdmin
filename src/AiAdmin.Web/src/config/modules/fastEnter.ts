/**
 * 快速入口配置
 * 包含：应用列表、快速链接等配置
 *
 * 只保留本系统实际存在的页面；需要菜单权限的入口通过 menuPath 声明，没有对应菜单权限的账号不显示
 */
import type { FastEnterConfig } from '@/types/config'

const fastEnterConfig: FastEnterConfig = {
    // 显示条件（屏幕宽度）
    minWidth: 1200,
    // 应用列表
    applications: [
        {
            name: '工作台',
            nameKey: 'fastEnter.console.title',
            description: '系统概览与数据统计',
            descriptionKey: 'fastEnter.console.description',
            icon: 'ri:pie-chart-line',
            iconColor: '#377dff',
            enabled: true,
            order: 1,
            routeName: 'Console',
            menuPath: '/dashboard/console',
        },
        {
            name: '站内信通知',
            nameKey: 'fastEnter.notice.title',
            description: '查看系统通知与站内信',
            descriptionKey: 'fastEnter.notice.description',
            icon: 'ri:notification-2-line',
            iconColor: '#ff6b6b',
            enabled: true,
            order: 2,
            action: 'openNotice',
        },
        {
            name: '在线客服',
            nameKey: 'fastEnter.supportChat.title',
            description: '咨询问题，客服在线解答',
            descriptionKey: 'fastEnter.supportChat.description',
            icon: 'ri:customer-service-2-line',
            iconColor: '#13DEB9',
            enabled: true,
            order: 3,
            action: 'openChat',
        },
        {
            name: '系统设置',
            nameKey: 'fastEnter.settings.title',
            description: '系统参数与基础配置',
            descriptionKey: 'fastEnter.settings.description',
            icon: 'ri:settings-3-line',
            iconColor: '#7A7FFF',
            enabled: true,
            order: 4,
            routeName: 'SystemSettings',
            menuPath: '/system-management/settings',
        },
        {
            name: '更新日志',
            nameKey: 'fastEnter.changeLog.title',
            description: '版本更新与变更记录',
            descriptionKey: 'fastEnter.changeLog.description',
            icon: 'ri:gamepad-line',
            iconColor: '#38C0FC',
            enabled: true,
            order: 5,
            routeName: 'ChangeLog',
            menuPath: '/development/change-log',
        },
    ],
    // 快速链接
    quickLinks: [
        {
            name: '登录',
            nameKey: 'fastEnter.login.title',
            enabled: true,
            order: 1,
            routeName: 'Login',
        },
        {
            name: '注册',
            nameKey: 'fastEnter.register.title',
            enabled: true,
            order: 2,
            routeName: 'Register',
        },
        {
            name: '忘记密码',
            nameKey: 'fastEnter.forgetPassword.title',
            enabled: true,
            order: 3,
            routeName: 'ForgetPassword',
        },
        {
            name: '个人中心',
            nameKey: 'fastEnter.userCenter.title',
            enabled: true,
            order: 4,
            routeName: 'UserCenter',
            menuPath: '/system-management/user-center',
        },
    ],
}

export default Object.freeze(fastEnterConfig)
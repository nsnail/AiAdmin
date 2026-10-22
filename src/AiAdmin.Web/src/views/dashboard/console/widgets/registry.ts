/**
 * 仪表盘卡片注册表：声明每个卡片的组件、默认宽度（12 栅格）、高度和默认是否显示
 * 新增卡片只需新增一个组件并在此登记，布局持久化和自定义编辑自动生效
 */
import { defineAsyncComponent, type Component } from 'vue'

export interface DashboardWidgetDefinition {
    id: string
    /** 卡片名称的 i18n 键 */
    titleKey: string
    component: Component
    /** 默认占用栅格数（1-12） */
    defaultSpan: number
    /** 卡片高度 */
    height: string
    /** 默认是否显示 */
    defaultVisible: boolean
}

/** 可选宽度（栅格数） */
export const widgetSpanOptions = [3, 4, 6, 8, 12]

/**
 * 创建卡片定义
 * @param id 卡片标识
 * @param loader 组件异步加载函数
 * @param defaultSpan 默认栅格宽度
 * @param height 卡片高度
 * @param defaultVisible 默认是否显示
 */
const define = (
    id: string,
    loader: () => Promise<Component>,
    defaultSpan: number,
    height = '20rem',
    defaultVisible = true,
): DashboardWidgetDefinition => ({
    id,
    titleKey: `dashboard.widgets.${id}.title`,
    component: defineAsyncComponent(loader),
    defaultSpan,
    height,
    defaultVisible,
})

export const dashboardWidgets: DashboardWidgetDefinition[] = [
    define('statUsers', () => import('./stat-users.vue'), 3, '8rem'),
    define('statLogins', () => import('./stat-logins.vue'), 3, '8rem'),
    define('statJobs', () => import('./stat-jobs.vue'), 3, '8rem'),
    define('statSuccessRate', () => import('./stat-success-rate.vue'), 3, '8rem'),
    define('statWallet', () => import('./stat-wallet.vue'), 3, '8rem', false),
    define('loginTrend', () => import('./login-trend.vue'), 8),
    define('deviceDistribution', () => import('./device-distribution.vue'), 4),
    define('executionTrend', () => import('./execution-trend.vue'), 6),
    define('newUserTrend', () => import('./new-user-trend.vue'), 6),
    define('regionRanking', () => import('./region-ranking.vue'), 4),
    define('jobStatus', () => import('./job-status.vue'), 4),
    define('browserDistribution', () => import('./browser-distribution.vue'), 4, '20rem', false),
    define('recentLogins', () => import('./recent-logins.vue'), 6),
    define('recentFailures', () => import('./recent-failures.vue'), 6),
]
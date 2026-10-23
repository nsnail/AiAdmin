/**
 * 仪表盘卡片注册表：声明每个卡片的组件、默认宽度（60 栅格）、高度和默认是否显示
 * 新增卡片只需新增一个组件并在此登记，布局持久化和自定义编辑自动生效
 */
import { defineAsyncComponent, type Component } from 'vue'

export interface DashboardWidgetDefinition {
    id: string
    /** 卡片名称的 i18n 键 */
    titleKey: string
    component: Component
    /** 默认占用栅格数（12-60，最小 20%） */
    defaultSpan: number
    /** 卡片高度 */
    height: string
    /** 默认是否显示 */
    defaultVisible: boolean
}

/** 可选宽度（栅格数） */
export const widgetSpanOptions = [12, 15, 20, 24, 30, 40, 60]

/** 可选高度（rem） */
export const widgetHeightOptions = ['8rem', '12rem', '16rem', '20rem', '24rem', '30rem']

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
    define('systemInfo', () => import('./system-info.vue'), 20, '16rem', false),
    define('statUsers', () => import('./stat-users.vue'), 15, '8rem'),
    define('statLogins', () => import('./stat-logins.vue'), 15, '8rem'),
    define('statJobs', () => import('./stat-jobs.vue'), 15, '8rem'),
    define('statSuccessRate', () => import('./stat-success-rate.vue'), 15, '8rem'),
    define('statWallet', () => import('./stat-wallet.vue'), 15, '8rem', false),
    define('loginTrend', () => import('./login-trend.vue'), 40),
    define('deviceDistribution', () => import('./device-distribution.vue'), 20),
    define('executionTrend', () => import('./execution-trend.vue'), 30),
    define('newUserTrend', () => import('./new-user-trend.vue'), 30),
    define('regionRanking', () => import('./region-ranking.vue'), 20),
    define('jobStatus', () => import('./job-status.vue'), 20),
    define('browserDistribution', () => import('./browser-distribution.vue'), 20, '20rem', false),
    define('recentLogins', () => import('./recent-logins.vue'), 30),
    define('recentFailures', () => import('./recent-failures.vue'), 30),
]
/** 默认布局预设：决定卡片的默认顺序、宽度、高度和显隐，未列出的新卡片按注册定义追加在末尾 */
export const defaultDashboardLayout: { id: string; span: number; height: string; visible: boolean }[] = [
    { id: 'systemInfo', span: 12, height: '8rem', visible: true },
    { id: 'statUsers', span: 12, height: '8rem', visible: true },
    { id: 'statLogins', span: 12, height: '8rem', visible: true },
    { id: 'statJobs', span: 12, height: '8rem', visible: true },
    { id: 'statSuccessRate', span: 12, height: '8rem', visible: true },
    { id: 'loginTrend', span: 15, height: '20rem', visible: true },
    { id: 'regionRanking', span: 15, height: '20rem', visible: true },
    { id: 'deviceDistribution', span: 15, height: '20rem', visible: true },
    { id: 'recentLogins', span: 15, height: '20rem', visible: true },
    { id: 'executionTrend', span: 20, height: '20rem', visible: true },
    { id: 'jobStatus', span: 20, height: '20rem', visible: true },
    { id: 'recentFailures', span: 20, height: '20rem', visible: true },
    { id: 'browserDistribution', span: 20, height: '20rem', visible: false },
    { id: 'newUserTrend', span: 60, height: '20rem', visible: true },
    { id: 'statWallet', span: 30, height: '8rem', visible: false },
]
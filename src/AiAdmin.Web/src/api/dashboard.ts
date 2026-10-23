/**
 * 仪表盘接口封装，一次请求返回大屏所有卡片所需的统计数据
 */
import request from '@/utils/http'

/** 按日趋势数据点 */
export interface DashboardTrendPoint {
    date: string
    primary: number
    secondary: number
}

/** 名称与数量键值 */
export interface DashboardNameValue {
    name: string
    value: number
}

/** 核心指标汇总 */
export interface DashboardSummary {
    totalUsers: number
    enabledUsers: number
    newUsersToday: number
    newUsersInRange: number
    loginsToday: number
    activeUsersToday: number
    totalJobs: number
    enabledJobs: number
    runningJobs: number
    failedJobs: number
    executionsToday: number
    executionSuccessRate: number
    totalRoles: number
    totalDepartments: number
    availableBalance: number
    frozenBalance: number
}

/** 最近登录记录 */
export interface DashboardRecentLogin {
    userName: string
    clientIp: string
    region: string
    browser: string
    createdAt: string
}

/** 最近异常的作业执行记录 */
export interface DashboardFailedExecution {
    id: string
    scheduledJobId: string
    jobName: string
    status: number
    errorMessage: string
    startedAt: string
}

/** 仪表盘总览数据 */
export interface DashboardOverview {
    summary: DashboardSummary
    loginTrend: DashboardTrendPoint[]
    newUserTrend: DashboardTrendPoint[]
    executionTrend: DashboardTrendPoint[]
    deviceDistribution: DashboardNameValue[]
    browserDistribution: DashboardNameValue[]
    regionRanking: DashboardNameValue[]
    jobStatusDistribution: DashboardNameValue[]
    recentLogins: DashboardRecentLogin[]
    recentFailures: DashboardFailedExecution[]
    generatedAt: string
}

/**
 * 查询仪表盘总览
 * @param days 统计天数
 */
export function fetchGetDashboardOverview(days: number) {
    return request.get<DashboardOverview>({
        url: '/api/dashboard/overview',
        params: { days, offsetMinutes: -new Date().getTimezoneOffset() },
    })
}

/** 系统信息，来自后端程序集元数据 */
export interface DashboardSystemInfo {
    name: string
    version: string
    /** 最近一次版本发布日期 */
    releaseDate: string | null
    /** 最近一次版本的更新内容分组 */
    releaseNotes: { title: string; items: string[] }[]
}

/**
 * 查询系统信息（名称、版本号）
 */
export function fetchGetDashboardSystemInfo() {
    return request.get<DashboardSystemInfo>({ url: '/api/dashboard/system-info' })
}
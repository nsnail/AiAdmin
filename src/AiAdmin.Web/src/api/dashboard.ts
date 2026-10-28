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

/** 接口请求占比的一个扇区 */
export interface DashboardApiShare {
    /** 接口路径，接口表里找不到时为路径 CRC32 的数值文本，合并的其他项为空 */
    path: string
    /** 接口说明的英文原文，需要按当前语言翻译，找不到接口或合并的其他项为空 */
    name: string
    value: number
}

/** 操作日志统计：今日与昨日按小时的访问量对比，以及今日各接口的请求占比 */
export interface DashboardRequestStats {
    /** 按本地小时（00 到 23）的访问量，primary 为今日，secondary 为昨日 */
    hourlyTrend: DashboardTrendPoint[]
    /** 今日按接口统计的请求数，路径和说明都为空表示合并的其他接口 */
    apiDistribution: DashboardApiShare[]
    todayTotal: number
    yesterdayTotal: number
    generatedAt: string
}

/**
 * 查询操作日志统计，只扫描今天和昨天的日表
 */
export function fetchGetDashboardRequestStats() {
    return request.get<DashboardRequestStats>({
        url: '/api/dashboard/request-stats',
        params: { offsetMinutes: -new Date().getTimezoneOffset() },
    })
}
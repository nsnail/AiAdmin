import { AppRouteRecord } from '@/types/router'
import { dashboardRoutes } from './dashboard'
import { permissionRoutes, systemManagementRoute, systemRoutes } from './system'
import { developmentRoutes } from './development'
import { financeRoutes } from './finance'

/**
 * 导出所有模块化路由
 */
export const routeModules: AppRouteRecord[] = [
    dashboardRoutes,
    permissionRoutes,
    systemManagementRoute,
    systemRoutes,
    developmentRoutes,
    financeRoutes,
]
import { AppRouteRecord } from '@/types/router'

export const dashboardRoutes: AppRouteRecord = {
    name: 'Dashboard',
    path: '/dashboard',
    component: '/index/index',
    meta: {
        title: 'menus.dashboard.title',
        icon: 'ri:pie-chart-line',
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER'],
    },
    children: [
        {
            path: 'console',
            name: 'Console',
            component: '/dashboard/console',
            meta: {
                title: 'menus.dashboard.console',
                icon: 'ri:home-smile-2-line',
                keepAlive: false,
                fixedTab: true,
            },
        },
        {
            path: 'tiles',
            name: 'QuickTiles',
            component: '/dashboard/tiles',
            meta: {
                title: 'menus.dashboard.tiles',
                icon: 'ri:apps-2-line',
                keepAlive: true,
            },
        },
        {
            path: 'business',
            name: 'BusinessConsole',
            component: '/dashboard/business',
            meta: {
                title: 'menus.dashboard.business',
                icon: 'ri:bar-chart-box-line',
                keepAlive: false,
                roles: ['R_SUPER'],
            },
        },
    ],
}
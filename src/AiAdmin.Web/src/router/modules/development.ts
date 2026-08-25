import type { AppRouteRecord } from '@/types/router'
export const developmentRoutes: AppRouteRecord = {
    path: '/development',
    name: 'Development',
    component: '/index/index',
    meta: {
        title: 'menus.development.title',
        icon: 'ri:code-box-line',
    },
    children: [
        {
            path: 'api-docs',
            name: 'ApiDocumentation',
            component: '/development/api-docs',
            meta: { title: 'menus.development.apiDocs', icon: 'ri:file-text-line', keepAlive: true },
        },
        {
            path: 'change-log',
            name: 'ChangeLog',
            component: '/change/log',
            meta: { title: 'menus.plan.log', icon: 'ri:gamepad-line', keepAlive: false },
        },
    ],
}
import { AppRouteRecord } from '@/types/router'

export const financeRoutes: AppRouteRecord = {
    path: '/finance',
    name: 'FinanceManagement',
    component: '/index/index',
    meta: {
        title: 'menus.finance.title',
        icon: 'ri:wallet-3-line',
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER'],
    },
    children: [
        {
            path: 'wallet',
            name: 'MyWallet',
            component: '/finance/wallet',
            meta: {
                title: 'menus.finance.wallet',
                icon: 'ri:wallet-3-line',
                keepAlive: true,
                roles: ['R_SUPER', 'R_ADMIN', 'R_USER'],
            },
        },
    ],
}
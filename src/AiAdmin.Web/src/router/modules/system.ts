import { AppRouteRecord } from '@/types/router'

export const permissionRoutes: AppRouteRecord = {
  path: '/permission',
  name: 'PermissionManagement',
  component: '/index/index',
  meta: {
    title: 'menus.system.permission',
    icon: 'ri:shield-user-line',
    roles: ['R_SUPER', 'R_ADMIN']
  },
  children: [
    {
      path: 'user',
      name: 'User',
      component: '/system/user',
      meta: {
        title: 'menus.system.user',
        icon: 'ri:user-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN']
      }
    },
    {
      path: 'role',
      name: 'Role',
      component: '/system/role',
      meta: {
        title: 'menus.system.role',
        icon: 'ri:user-settings-line',
        keepAlive: true,
        roles: ['R_SUPER']
      }
    },
    {
      path: 'department',
      name: 'Department',
      component: '/system/department',
      meta: {
        title: 'menus.system.department',
        icon: 'ri:organization-chart',
        keepAlive: true,
        roles: ['R_SUPER'],
        authList: [
          { title: '新增', authMark: 'add' },
          { title: '编辑', authMark: 'edit' },
          { title: '删除', authMark: 'delete' }
        ]
      }
    },
    {
      path: 'menu',
      name: 'Menus',
      component: '/system/menu',
      meta: {
        title: 'menus.system.menu',
        icon: 'ri:menu-line',
        keepAlive: true,
        roles: ['R_SUPER'],
        authList: [
          { title: '新增', authMark: 'add' },
          { title: '编辑', authMark: 'edit' },
          { title: '删除', authMark: 'delete' }
        ]
      }
    },
    {
      path: 'api',
      name: 'ApiManagement',
      component: '/system/api',
      meta: {
        title: 'menus.system.api',
        icon: 'ri:route-line',
        keepAlive: true,
        roles: ['R_SUPER']
      }
    }
  ]
}

export const systemManagementRoute: AppRouteRecord = {
  path: '/system-management',
  name: 'SystemManagement',
  component: '/index/index',
  meta: { title: 'menus.system.title', icon: 'ri:settings-3-line', roles: ['R_SUPER'] },
  children: [
    {
      path: 'dictionary',
      name: 'DictionaryManagement',
      component: '/system/dictionary',
      meta: {
        title: 'menus.system.dictionary',
        icon: 'ri:book-shelf-line',
        keepAlive: true,
        roles: ['R_SUPER'],
        authList: [
          { title: '新增', authMark: 'add' },
          { title: '编辑', authMark: 'edit' },
          { title: '删除', authMark: 'delete' }
        ]
      }
    },
    {
      path: 'files',
      name: 'FileManagement',
      component: '/system/files',
      meta: {
        title: 'menus.system.files',
        icon: 'ri:file-list-3-line',
        keepAlive: true,
        roles: ['R_SUPER']
      }
    },
    {
      path: 'scheduled-job',
      name: 'ScheduledJobManagement',
      component: '/system/scheduled-job',
      meta: {
        title: 'menus.system.scheduledJob',
        icon: 'ri:timer-line',
        keepAlive: true,
        roles: ['R_SUPER']
      }
    }
  ]
}

export const systemRoutes: AppRouteRecord = {
  path: '/system',
  name: 'System',
  component: '/index/index',
  meta: {
    title: 'menus.system.title',
    icon: 'ri:user-3-line',
    isHide: true
  },
  children: [
    {
      path: 'user-center',
      name: 'UserCenter',
      component: '/system/user-center',
      meta: {
        title: 'menus.system.userCenter',
        icon: 'ri:user-line',
        isHide: true,
        keepAlive: true,
        isHideTab: true
      }
    }
  ]
}

import type { AppRouteRecord } from '@/types/router'
import { articleRoutes } from './article'
import { examplesRoutes } from './examples'
import { exceptionRoutes } from './exception'
import { helpRoutes } from './help'
import { resultRoutes } from './result'
import { safeguardRoutes } from './safeguard'
import { templateRoutes } from './template'
import { widgetsRoutes } from './widgets'

const exampleRoutes = [
  templateRoutes,
  widgetsRoutes,
  examplesRoutes,
  articleRoutes,
  resultRoutes,
  exceptionRoutes,
  safeguardRoutes,
  ...helpRoutes
].map((route) => ({
  ...route,
  path: route.path.replace(/^\//, '')
}))

export const developmentRoutes: AppRouteRecord = {
  path: '/development',
  name: 'Development',
  component: '/index/index',
  meta: {
    title: 'menus.development.title',
    icon: 'ri:code-box-line'
  },
  children: [
    {
      path: 'examples',
      name: 'ExampleCenter',
      component: '',
      meta: {
        title: 'menus.development.exampleCenter',
        icon: 'ri:apps-2-line'
      },
      children: exampleRoutes
    }
  ]
}

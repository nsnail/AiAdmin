<!-- 仪表盘指标卡片：主数值、图标和辅助说明 -->
<template>
    <div
        v-loading="loading"
        :class="[
            'art-card relative flex h-full flex-col justify-center px-5 py-4',
            linkable() ? 'cursor-pointer transition-shadow hover:shadow-md' : '',
        ]"
        @click="go(query)">
        <span class="text-sm text-g-700">{{ title }}</span>
        <ArtCountTo
            :compact="decimals === 0"
            :decimals="decimals"
            :duration="1000"
            :target="value"
            :title="decimals === 0 ? formatFullNumber(value) : undefined"
            class="mt-2 text-[26px] font-medium" />
        <span class="mt-1 truncate text-xs text-g-600">{{ description }}</span>
        <div class="flex-cc absolute bottom-0 right-5 top-0 m-auto size-12 rounded-xl bg-theme/10">
            <ArtSvgIcon :icon="icon" class="text-xl text-theme" />
        </div>
    </div>
</template>

<script lang="ts" setup>
import { formatFullNumber } from '@/utils/number'
import { useWidgetLink } from './context'

defineOptions({ name: 'DashboardStatCard' })

const props = withDefaults(
    defineProps<{
        title: string
        value: number
        description: string
        icon: string
        decimals?: number
        loading?: boolean
        /** 点击后跳转的页面路径 */
        to?: string
        /** 跳转时附带的路由查询参数，由目标页面据此预设标签页或筛选 */
        query?: Record<string, string>
    }>(),
    { decimals: 0, loading: false },
)
const { linkable, go } = useWidgetLink(() => props.to)
</script>
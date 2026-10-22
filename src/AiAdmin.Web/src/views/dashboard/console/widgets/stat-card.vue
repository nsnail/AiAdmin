<!-- 仪表盘指标卡片：主数值、图标和辅助说明 -->
<template>
    <div v-loading="loading" class="art-card relative flex h-full flex-col justify-center px-5 py-4">
        <span class="text-sm text-g-700">{{ title }}</span>
        <ArtCountTo :decimals="decimals" :duration="1000" :target="value" class="mt-2 text-[26px] font-medium" />
        <span class="mt-1 truncate text-xs text-g-600">{{ description }}</span>
        <div class="flex-cc absolute bottom-0 right-5 top-0 m-auto size-12 rounded-xl bg-theme/10">
            <ArtSvgIcon :icon="icon" class="text-xl text-theme" />
        </div>
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'DashboardStatCard' })

withDefaults(
    defineProps<{
        title: string
        value: number
        description: string
        icon: string
        decimals?: number
        loading?: boolean
    }>(),
    { decimals: 0, loading: false },
)
</script>
<!-- 仪表盘卡片外框：统一标题、右侧说明与撑满高度的内容区 -->
<template>
    <div class="art-card flex h-full min-h-0 flex-col p-5">
        <div v-if="title" class="mb-3 flex items-center justify-between">
            <h4 class="text-base font-medium text-g-900">{{ title }}</h4>
            <span v-if="extra" class="text-xs text-g-500">{{ extra }}</span>
        </div>
        <div class="min-h-0 flex-1">
            <slot />
        </div>
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'DashboardWidgetCard' })

defineProps<{
    title?: string
    extra?: string
}>()
</script>
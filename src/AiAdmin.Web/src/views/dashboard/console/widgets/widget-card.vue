<!-- 仪表盘卡片外框：统一标题、右侧说明与撑满高度的内容区 -->
<template>
    <div class="art-card flex h-full min-h-0 flex-col p-5">
        <div v-if="title" class="mb-3 flex items-center justify-between">
            <h4 :class="['flex items-center text-base font-medium text-g-900', linkable() ? 'cursor-pointer hover:text-theme' : '']" @click="go()">
                {{ title }}
                <ArtSvgIcon v-if="linkable()" class="ml-0.5 text-base" icon="ri:arrow-right-s-line" />
            </h4>
            <span v-if="extra" class="text-xs text-g-500">{{ extra }}</span>
        </div>
        <div class="min-h-0 flex-1">
            <slot />
        </div>
    </div>
</template>

<script lang="ts" setup>
import { useWidgetLink } from './context'

defineOptions({ name: 'DashboardWidgetCard' })

const props = defineProps<{
    title?: string
    extra?: string
    /** 点击标题后跳转的页面路径 */
    to?: string
}>()
const { linkable, go } = useWidgetLink(() => props.to)
</script>
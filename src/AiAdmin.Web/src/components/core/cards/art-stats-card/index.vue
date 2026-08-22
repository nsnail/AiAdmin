<!-- 统计卡片 -->
<template>
    <div :class="boxStyle" class="art-card h-32 flex-c px-5 transition-transform duration-200 hover:-translate-y-0.5">
        <div v-if="icon" :class="iconStyle" class="mr-4 size-11 flex-cc rounded-lg text-xl text-white">
            <ArtSvgIcon :icon="icon"></ArtSvgIcon>
        </div>
        <div class="flex-1">
            <p v-if="title" :style="{ color: textColor }" class="m-0 text-lg font-medium">
                {{ title }}
            </p>
            <ArtCountTo
                v-if="count !== undefined"
                :decimals="decimals"
                :duration="2000"
                :separator="separator"
                :target="count"
                class="m-0 text-2xl font-medium" />
            <p v-if="description" :style="{ color: textColor }" class="mt-1 text-sm text-g-500 opacity-90">{{ description }}</p>
        </div>
        <div v-if="showArrow">
            <ArtSvgIcon class="text-xl text-g-500" icon="ri:arrow-right-s-line" />
        </div>
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'ArtStatsCard' })

interface StatsCardProps {
    /** 盒子样式 */
    boxStyle?: string
    /** 图标 */
    icon?: string
    /** 图标样式 */
    iconStyle?: string
    /** 标题 */
    title?: string
    /** 数值 */
    count?: number
    /** 小数位 */
    decimals?: number
    /** 分隔符 */
    separator?: string
    /** 描述 */
    description: string
    /** 文本颜色 */
    textColor?: string
    /** 是否显示箭头 */
    showArrow?: boolean
}

withDefaults(defineProps<StatsCardProps>(), {
    iconSize: 30,
    iconBgRadius: 50,
    decimals: 0,
    separator: ',',
})
</script>

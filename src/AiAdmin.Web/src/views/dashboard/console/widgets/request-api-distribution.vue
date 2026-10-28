<!-- 今日请求的接口占比饼图：请求数最多的若干个接口，其余合并为“其他” -->
<template>
    <WidgetCard :extra="extra" :title="t('dashboard.widgets.requestApiDistribution.title')">
        <ArtRingChart
            :data="data"
            :is-empty="data.length === 0 && !!stats"
            :loading="loading && !stats"
            :radius="['45%', '65%']"
            :show-label="true"
            height="100%" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { translateServerMessage } from '@/utils/i18n/server-message'
import { useRequestStats } from './request-stats'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRequestApiDistribution' })

const { t, locale } = useI18n()
const { stats, loading } = useRequestStats()
/** 扇区名称：优先显示按当前语言翻译的接口说明，接口表里找不到说明时显示路径，合并项显示“其他” */
const data = computed(() => {
    // 显式依赖当前语言，切换语言后接口说明立即按新语言重新翻译
    void locale.value
    return (stats.value?.apiDistribution ?? []).map((x) => ({
        name: x.name ? (translateServerMessage(x.name) ?? x.name) : x.path || t('dashboard.others'),
        value: x.value,
        // 提示框额外显示接口路径；没有接口说明时名称已经是路径，不重复显示
        tip: x.name && x.path ? x.path : undefined,
    }))
})
const extra = computed(() => (stats.value ? t('dashboard.requestToday', { total: stats.value.todayTotal }) : ''))
</script>
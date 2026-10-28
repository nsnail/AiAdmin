<!-- 访问量趋势：今日与昨日按小时的访问量两条折线，便于对比 -->
<template>
    <WidgetCard :extra="extra" :title="t('dashboard.widgets.requestTrend.title')">
        <ArtLineChart
            :data="series"
            :is-empty="isEmpty"
            :loading="loading && !stats"
            :show-legend="true"
            :x-axis-data="labels"
            height="100%"
            legend-position="top" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRequestStats } from './request-stats'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRequestTrend' })

const { t } = useI18n()
const { stats, loading } = useRequestStats()
const trend = computed(() => stats.value?.hourlyTrend ?? [])
const labels = computed(() => trend.value.map((x) => `${x.date}:00`))
const series = computed(() => [
    { name: t('dashboard.series.today'), data: trend.value.map((x) => x.primary), showAreaColor: true },
    { name: t('dashboard.series.yesterday'), data: trend.value.map((x) => x.secondary) },
])
const isEmpty = computed(() => !!stats.value && stats.value.todayTotal === 0 && stats.value.yesterdayTotal === 0)
const extra = computed(() =>
    stats.value ? t('dashboard.requestTotals', { today: stats.value.todayTotal, yesterday: stats.value.yesterdayTotal }) : '',
)
</script>
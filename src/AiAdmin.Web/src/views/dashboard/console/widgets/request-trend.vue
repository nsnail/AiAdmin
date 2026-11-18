<!-- 访问量趋势：今日与昨日按小时的访问量两条折线，便于对比 -->
<template>
    <WidgetCard :extra="extra" :title="t('dashboard.widgets.requestTrend.title')" to="/log-management/api-request-log">
        <ArtLineChart
            :clickable="linkable()"
            :data="series"
            :is-empty="isEmpty"
            :loading="loading && !stats"
            :show-legend="true"
            :x-axis-data="labels"
            @point-click="handlePointClick"
            height="100%"
            legend-position="top" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { formatFullNumber } from '@/utils/number'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useWidgetLink } from './context'
import { useRequestStats } from './request-stats'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRequestTrend' })

const { t } = useI18n()
const { stats, loading } = useRequestStats()
const { linkable, go } = useWidgetLink(() => '/log-management/api-request-log')
const trend = computed(() => stats.value?.hourlyTrend ?? [])
const labels = computed(() => trend.value.map((x) => `${x.date}:00`))
const series = computed(() => [
    { name: t('dashboard.series.today'), data: trend.value.map((x) => x.primary), showAreaColor: true },
    { name: t('dashboard.series.yesterday'), data: trend.value.map((x) => x.secondary) },
])
/**
 * 点击某个小时跳转操作日志并筛选该小时：第一条线是今日，第二条线是昨日
 * @param payload 点击位置对应的小时序号和线序号
 */
function handlePointClick(payload: { dataIndex: number; seriesIndex: number }) {
    const point = trend.value[payload.dataIndex]
    if (!point) return
    go({ hour: String(Number(point.date)), dayOffset: payload.seriesIndex === 1 ? '-1' : '0' })
}
const isEmpty = computed(() => !!stats.value && stats.value.todayTotal === 0 && stats.value.yesterdayTotal === 0)
const extra = computed(() =>
    stats.value
        ? t('dashboard.requestTotals', { today: formatFullNumber(stats.value.todayTotal), yesterday: formatFullNumber(stats.value.yesterdayTotal) })
        : '',
)
</script>
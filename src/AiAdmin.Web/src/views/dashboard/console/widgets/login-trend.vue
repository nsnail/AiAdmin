<!-- 登录趋势：登录次数与登录用户数折线 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.loginTrend.title')">
        <ArtLineChart :data="series" :loading="loading" :show-legend="true" :x-axis-data="labels" height="100%" legend-position="top" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardLoginTrend' })

const { t } = useI18n()
const { overview, loading, days } = useDashboardContext()
const trend = computed(() => overview.value?.loginTrend ?? [])
const labels = computed(() => trend.value.map((x) => x.date.slice(5)))
const series = computed(() => [
    { name: t('dashboard.series.logins'), data: trend.value.map((x) => x.primary), showAreaColor: true },
    { name: t('dashboard.series.loginUsers'), data: trend.value.map((x) => x.secondary) },
])
</script>
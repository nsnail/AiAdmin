<!-- 作业执行趋势：成功与失败次数堆叠柱状图 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.executionTrend.title')">
        <ArtBarChart
            :colors="['#67C23A', '#F56C6C']"
            :data="series"
            :loading="loading"
            :show-legend="true"
            :x-axis-data="labels"
            height="100%"
            legend-position="top"
            stack />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardExecutionTrend' })

const { t } = useI18n()
const { overview, loading, days } = useDashboardContext()
const trend = computed(() => overview.value?.executionTrend ?? [])
const labels = computed(() => trend.value.map((x) => x.date.slice(5)))
const series = computed(() => [
    { name: t('dashboard.series.success'), data: trend.value.map((x) => x.primary), stack: 'run' },
    { name: t('dashboard.series.failed'), data: trend.value.map((x) => x.secondary), stack: 'run' },
])
</script>
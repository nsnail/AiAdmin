<!-- 新增用户趋势 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.newUserTrend.title')">
        <ArtBarChart :data="data" :loading="loading" :x-axis-data="labels" height="100%" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardNewUserTrend' })

const { t } = useI18n()
const { overview, loading, days } = useDashboardContext()
const trend = computed(() => overview.value?.newUserTrend ?? [])
const labels = computed(() => trend.value.map((x) => x.date.slice(5)))
const data = computed(() => trend.value.map((x) => x.primary))
</script>
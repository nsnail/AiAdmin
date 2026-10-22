<!-- 登录浏览器分布 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.browserDistribution.title')">
        <ArtRingChart :data="data" :loading="loading" :show-legend="true" height="100%" legend-position="right" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardBrowserDistribution' })

const { t } = useI18n()
const { overview, loading, days } = useDashboardContext()
const data = computed(() => (overview.value?.browserDistribution ?? []).map((x) => ({ name: x.name, value: x.value })))
</script>
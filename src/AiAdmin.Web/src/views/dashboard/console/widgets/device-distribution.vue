<!-- 登录设备类型分布 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.deviceDistribution.title')">
        <ArtRingChart :data="data" :loading="loading" :show-legend="true" height="100%" legend-position="right" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardDeviceDistribution' })

const { t, te } = useI18n()
const { overview, loading, days } = useDashboardContext()
const data = computed(() =>
    (overview.value?.deviceDistribution ?? []).map((x) => {
        const key = `loginLog.devices.${x.name.toLowerCase()}`
        return { name: te(key) ? t(key) : x.name, value: x.value }
    }),
)
</script>
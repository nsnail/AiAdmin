<!-- 计划作业状态分布 -->
<template>
    <WidgetCard :title="t('dashboard.widgets.jobStatus.title')">
        <ArtRingChart :data="data" :loading="loading" :show-legend="true" height="100%" legend-position="right" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardJobStatus' })

const { t } = useI18n()
const { overview, loading } = useDashboardContext()
const data = computed(() =>
    (overview.value?.jobStatusDistribution ?? []).map((x) => ({ name: t(`scheduledJob.status.${x.name.toLowerCase()}`), value: x.value })),
)
</script>
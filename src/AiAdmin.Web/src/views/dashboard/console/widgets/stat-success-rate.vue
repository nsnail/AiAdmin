<!-- 指标卡片：作业执行成功率 -->
<template>
    <StatCard
        :decimals="1"
        :description="t('dashboard.widgets.statSuccessRate.desc', { days, count: summary?.executionsToday ?? 0 })"
        :loading="loading"
        :title="t('dashboard.widgets.statSuccessRate.title')"
        :value="summary?.executionSuccessRate ?? 0"
        icon="ri:checkbox-circle-line" />
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import StatCard from './stat-card.vue'

defineOptions({ name: 'DashboardStatSuccessRate' })

const { t } = useI18n()
const { overview, loading, days } = useDashboardContext()
const summary = computed(() => overview.value?.summary)
</script>
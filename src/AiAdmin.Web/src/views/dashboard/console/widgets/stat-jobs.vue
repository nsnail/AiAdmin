<!-- 指标卡片：计划作业 -->
<template>
    <StatCard
        :description="
            t('dashboard.widgets.statJobs.desc', {
                enabled: summary?.enabledJobs ?? 0,
                running: summary?.runningJobs ?? 0,
                failed: summary?.failedJobs ?? 0,
            })
        "
        :loading="loading"
        :title="t('dashboard.widgets.statJobs.title')"
        :value="summary?.totalJobs ?? 0"
        icon="ri:timer-line" />
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import StatCard from './stat-card.vue'

defineOptions({ name: 'DashboardStatJobs' })

const { t } = useI18n()
const { overview, loading } = useDashboardContext()
const summary = computed(() => overview.value?.summary)
</script>
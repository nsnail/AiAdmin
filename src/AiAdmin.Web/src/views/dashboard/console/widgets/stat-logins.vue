<!-- 指标卡片：今日登录 -->
<template>
    <StatCard
        :description="t('dashboard.widgets.statLogins.desc', { users: summary?.activeUsersToday ?? 0 })"
        :loading="loading"
        :title="t('dashboard.widgets.statLogins.title')"
        :value="summary?.loginsToday ?? 0"
        icon="ri:login-circle-line" />
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import StatCard from './stat-card.vue'

defineOptions({ name: 'DashboardStatLogins' })

const { t } = useI18n()
const { overview, loading } = useDashboardContext()
const summary = computed(() => overview.value?.summary)
</script>
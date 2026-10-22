<!-- 指标卡片：用户总数 -->
<template>
    <StatCard
        :description="t('dashboard.widgets.statUsers.desc', { enabled: summary?.enabledUsers ?? 0, added: summary?.newUsersToday ?? 0 })"
        :loading="loading"
        :title="t('dashboard.widgets.statUsers.title')"
        :value="summary?.totalUsers ?? 0"
        icon="ri:group-line" />
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import StatCard from './stat-card.vue'

defineOptions({ name: 'DashboardStatUsers' })

const { t } = useI18n()
const { overview, loading } = useDashboardContext()
const summary = computed(() => overview.value?.summary)
</script>
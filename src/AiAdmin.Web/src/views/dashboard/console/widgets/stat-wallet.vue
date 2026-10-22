<!-- 指标卡片：钱包可用余额 -->
<template>
    <StatCard
        :decimals="2"
        :description="t('dashboard.widgets.statWallet.desc', { frozen: (summary?.frozenBalance ?? 0).toFixed(2) })"
        :loading="loading"
        :title="t('dashboard.widgets.statWallet.title')"
        :value="summary?.availableBalance ?? 0"
        icon="ri:wallet-3-line" />
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import StatCard from './stat-card.vue'

defineOptions({ name: 'DashboardStatWallet' })

const { t } = useI18n()
const { overview, loading } = useDashboardContext()
const summary = computed(() => overview.value?.summary)
</script>
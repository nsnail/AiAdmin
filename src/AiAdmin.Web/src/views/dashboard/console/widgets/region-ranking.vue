<!-- 登录地区排行 -->
<template>
    <WidgetCard :extra="t('dashboard.lastDays', { days })" :title="t('dashboard.widgets.regionRanking.title')">
        <div v-if="!rows.length" class="flex-cc h-full text-sm text-g-500">{{ t('dashboard.empty') }}</div>
        <div v-else class="flex h-full flex-col justify-around gap-2">
            <div v-for="item in rows" :key="item.name">
                <div class="mb-1 flex justify-between text-sm">
                    <span class="truncate text-g-800">{{ item.name }}</span>
                    <span class="text-g-600">{{ item.value }}</span>
                </div>
                <ElProgress :percentage="item.percent" :show-text="false" :stroke-width="6" />
            </div>
        </div>
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRegionRanking' })

const { t } = useI18n()
const { overview, days } = useDashboardContext()
const rows = computed(() => {
    const list = overview.value?.regionRanking ?? []
    const max = Math.max(1, ...list.map((x) => x.value))
    return list.map((x) => ({
        name: x.name === 'Unknown' ? t('dashboard.unknownRegion') : x.name,
        value: x.value,
        percent: Math.round((x.value / max) * 100),
    }))
})
</script>
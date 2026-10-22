<!-- 最近异常的作业执行记录 -->
<template>
    <WidgetCard :title="t('dashboard.widgets.recentFailures.title')">
        <div v-if="!rows.length" class="flex-cc h-full text-sm text-g-500">{{ t('dashboard.noFailures') }}</div>
        <ElScrollbar v-else>
            <div v-for="item in rows" :key="item.id" class="border-b border-g-200 py-2 text-sm last:border-0">
                <div class="flex items-center justify-between">
                    <span class="truncate font-medium text-g-900">{{ item.jobName }}</span>
                    <span class="ml-3 shrink-0 text-xs text-g-500">{{ formatTime(item.startedAt) }}</span>
                </div>
                <p class="mt-1 truncate text-xs text-danger">
                    {{ t(`scheduledJob.status.${statusKey(item.status)}`) }}<template v-if="item.errorMessage"> · {{ item.errorMessage }}</template>
                </p>
            </div>
        </ElScrollbar>
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRecentFailures' })

const { t } = useI18n()
const { overview } = useDashboardContext()
const rows = computed(() => overview.value?.recentFailures ?? [])
const formatTime = (value: string) => new Date(value).toLocaleString()
const statusKey = (status: number) => (status === 4 ? 'timeout' : 'failed')
</script>
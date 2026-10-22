<!-- 最近登录记录 -->
<template>
    <WidgetCard :title="t('dashboard.widgets.recentLogins.title')">
        <div v-if="!rows.length" class="flex-cc h-full text-sm text-g-500">{{ t('dashboard.empty') }}</div>
        <ElScrollbar v-else>
            <div
                v-for="(item, index) in rows"
                :key="index"
                class="flex items-center justify-between border-b border-g-200 py-2 text-sm last:border-0">
                <div class="min-w-0">
                    <p class="truncate font-medium text-g-900">{{ item.userName }}</p>
                    <p class="truncate text-xs text-g-500">
                        {{ item.clientIp }} · {{ item.region || t('dashboard.unknownRegion') }} · {{ item.browser }}
                    </p>
                </div>
                <span class="ml-3 shrink-0 text-xs text-g-500">{{ formatTime(item.createdAt) }}</span>
            </div>
        </ElScrollbar>
    </WidgetCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useDashboardContext } from './context'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRecentLogins' })

const { t } = useI18n()
const { overview } = useDashboardContext()
const rows = computed(() => overview.value?.recentLogins ?? [])
const formatTime = (value: string) => new Date(value).toLocaleString()
</script>
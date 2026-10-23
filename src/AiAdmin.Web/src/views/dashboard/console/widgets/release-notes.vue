<!-- 版本更新内容列表：供系统信息卡片及其溢出气泡复用 -->
<template>
    <template v-if="info?.releaseNotes.length">
        <div v-for="section in info.releaseNotes" :key="section.title" class="mb-2">
            <div class="mb-1 font-medium text-g-800">{{ section.title }}</div>
            <ul class="m-0 list-disc pl-4 text-g-700">
                <li v-for="item in section.items" :key="item" class="leading-5">{{ item }}</li>
            </ul>
        </div>
    </template>
    <span v-else class="text-g-600">{{ t('dashboard.widgets.systemInfo.empty') }}</span>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { DashboardSystemInfo } from '@/api/dashboard'

defineOptions({ name: 'DashboardReleaseNotes' })

defineProps<{ info: DashboardSystemInfo | null }>()

const { t } = useI18n()
</script>
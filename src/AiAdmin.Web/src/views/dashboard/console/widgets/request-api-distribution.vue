<!-- 今日请求的接口占比饼图：请求数最多的若干个接口，其余合并为“其他” -->
<template>
    <WidgetCard :extra="extra" :title="t('dashboard.widgets.requestApiDistribution.title')" to="/log-management/api-request-log">
        <ArtRingChart
            :clickable="linkable()"
            :data="data"
            :is-empty="data.length === 0 && !!stats"
            :loading="loading && !stats"
            :radius="['45%', '65%']"
            :show-label="true"
            @item-click="handleItemClick"
            height="100%" />
    </WidgetCard>
</template>

<script lang="ts" setup>
import { formatFullNumber } from '@/utils/number'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { translateServerMessage } from '@/utils/i18n/server-message'
import { useWidgetLink } from './context'
import { useRequestStats } from './request-stats'
import WidgetCard from './widget-card.vue'

defineOptions({ name: 'DashboardRequestApiDistribution' })

const { t, locale } = useI18n()
const { stats, loading } = useRequestStats()
const { linkable, go } = useWidgetLink(() => '/log-management/api-request-log')
/** 扇区名称：优先显示按当前语言翻译的接口说明，接口表里找不到说明时显示路径，合并项显示“其他” */
const data = computed(() => {
    // 显式依赖当前语言，切换语言后接口说明立即按新语言重新翻译
    void locale.value
    return (stats.value?.apiDistribution ?? []).map((x) => ({
        name: x.name ? (translateServerMessage(x.name) ?? x.name) : x.path || t('dashboard.others'),
        value: x.value,
        // 提示框额外显示接口路径；没有接口说明时名称已经是路径，不重复显示
        tip: x.name && x.path ? x.path : undefined,
        pathCrc32: x.pathCrc32,
    }))
})
/**
 * 点击扇区跳转操作日志并按该接口筛选；合并的其他项没有对应接口，只跳转不筛选
 * @param item 被点击的扇区数据
 */
function handleItemClick(item: { pathCrc32?: unknown }) {
    const crc = Number(item.pathCrc32)
    go(crc ? { pathCrc32: String(crc) } : undefined)
}
const extra = computed(() => (stats.value ? t('dashboard.requestToday', { total: formatFullNumber(stats.value.todayTotal) }) : ''))
</script>
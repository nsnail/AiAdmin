<template>
    <div class="art-card h-100 p-5 mb-5 max-sm:mb-4">
        <div class="art-card-header">
            <div class="title">
                <h4>总收入</h4>
            </div>
        </div>
        <ArtBarChart :data="revenueData" :showAxisLine="false" :showLegend="true" :xAxisData="weekDays" barWidth="18%" height="calc(100% - 30px)" />
    </div>
</template>

<script lang="ts" setup>
interface RevenueDataItem {
    name: string
    data: number[]
}

/**
 * 一周的日期标签
 */
const weekDays = ref(['周一', '周二', '周三', '周四', '周五', '周六', '周日'])

/**
 * 总收入数据
 * 对比线上和线下销售的一周收入情况
 */
const revenueData = ref<RevenueDataItem[]>([
    {
        name: '线上销售',
        data: [12, 13, 5, 15, 10, 15, 18],
    },
    {
        name: '线下销售',
        data: [10, 11, 20, 5, 11, 13, 10],
    },
])
</script>
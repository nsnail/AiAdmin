<!-- 柱线混合图：柱子使用左侧数值轴，折线使用右侧百分比轴，折线中的空值断开显示 -->
<template>
    <div v-loading="props.loading" :style="{ height: props.height }" ref="chartRef"></div>
</template>

<script lang="ts" setup>
import { useChartOps, useChartComponent } from '@/hooks/core/useChart'
import type { EChartsOption } from '@/plugins/echarts'

defineOptions({ name: 'ArtBarLineChart' })

interface ArtBarLineChartProps {
    height?: string
    loading?: boolean
    isEmpty?: boolean
    colors?: string[]
    xAxisData?: string[]
    /** 柱子系列名称 */
    barName: string
    /** 柱子数据 */
    barData: number[]
    /** 折线系列名称 */
    lineName: string
    /** 折线数据（百分比），null 表示当日无数据 */
    lineData: Array<number | null>
    legendPosition?: 'bottom' | 'top'
}

const props = withDefaults(defineProps<ArtBarLineChartProps>(), {
    height: useChartOps().chartHeight,
    loading: false,
    isEmpty: false,
    colors: () => useChartOps().colors,
    xAxisData: () => [],
    legendPosition: 'top',
})

const {
    chartRef,
    getAxisLineStyle,
    getAxisLabelStyle,
    getCompactAxisLabelStyle,
    formatTooltipValue,
    getAxisTickStyle,
    getSplitLineStyle,
    getAnimationConfig,
    getTooltipStyle,
    getLegendStyle,
    getGridWithLegend,
} = useChartComponent({
    props,
    checkEmpty: () => !props.barData.length || (props.barData.every((v) => v === 0) && props.lineData.every((v) => !v)),
    watchSources: [() => props.barData, () => props.lineData, () => props.xAxisData, () => props.colors],
    generateOptions: (): EChartsOption => ({
        color: props.colors,
        grid: getGridWithLegend(true, props.legendPosition, { top: 15, right: 0, left: 0 }),
        legend: getLegendStyle(props.legendPosition),
        // 提示框保留完整数字；百分比系列在各自的 tooltip 里单独格式化，优先于这里的全局格式化
        tooltip: getTooltipStyle('axis', { valueFormatter: formatTooltipValue }),
        xAxis: {
            type: 'category',
            data: props.xAxisData,
            axisTick: getAxisTickStyle(),
            axisLine: getAxisLineStyle(true),
            axisLabel: getAxisLabelStyle(true),
        },
        yAxis: [
            {
                type: 'value',
                minInterval: 1,
                axisLabel: getCompactAxisLabelStyle(true),
                axisLine: getAxisLineStyle(true),
                splitLine: getSplitLineStyle(true),
            },
            {
                type: 'value',
                min: 0,
                max: 100,
                axisLabel: { ...getAxisLabelStyle(true), formatter: '{value}%' },
                axisLine: getAxisLineStyle(false),
                splitLine: { show: false },
            },
        ],
        series: [
            {
                name: props.barName,
                type: 'bar',
                data: props.barData,
                barWidth: '40%',
                itemStyle: { borderRadius: [4, 4, 0, 0] },
                ...getAnimationConfig(),
            },
            {
                name: props.lineName,
                type: 'line',
                yAxisIndex: 1,
                data: props.lineData,
                smooth: true,
                connectNulls: false,
                symbol: 'circle',
                symbolSize: 6,
                lineStyle: { width: 2.5 },
                tooltip: { valueFormatter: (v: unknown) => (v == null ? '-' : `${v}%`) },
            },
        ],
    }),
})
</script>
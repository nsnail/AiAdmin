<!-- 环形图 -->
<template>
    <div v-loading="props.loading" :style="{ height: props.height }" class="relative w-full" ref="chartRef"></div>
</template>

<script lang="ts" setup>
import type { EChartsOption } from '@/plugins/echarts'
import { useChartOps, useChartComponent } from '@/hooks/core/useChart'

defineOptions({ name: 'ArtRingChart' })

/** 转义提示框中的文本，名称和说明来自接口数据，不能当作 HTML 直接拼接 */
const escapeHtml = (text: string): string =>
    text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;')

interface ArtRingChartProps {
    height?: string
    loading?: boolean
    isEmpty?: boolean
    colors?: string[]
    /** 数据项，tip 为可选的提示框附加说明，显示在名称和数量下面一行 */
    data: Array<{ name: string; value: number; tip?: string }>
    radius?: string[]
    borderRadius?: number
    centerText?: string
    showLabel?: boolean
    showTooltip?: boolean
    showLegend?: boolean
    legendPosition?: 'bottom' | 'top' | 'left' | 'right'
}

const props = withDefaults(defineProps<ArtRingChartProps>(), {
    // 基础配置
    height: useChartOps().chartHeight,
    loading: false,
    isEmpty: false,
    colors: () => useChartOps().colors,

    // 数据配置
    data: () => [],
    radius: () => ['50%', '80%'],
    borderRadius: 10,
    centerText: '',
    showLabel: false,

    // 交互配置
    showTooltip: true,
    showLegend: false,
    legendPosition: 'right',
})

// 使用新的图表组件抽象
const { chartRef, isDark, getAnimationConfig, getTooltipStyle, getLegendStyle } = useChartComponent({
    props,
    checkEmpty: () => {
        return !props.data?.length || props.data.every((item) => item.value === 0)
    },
    watchSources: [() => props.data, () => props.centerText],
    generateOptions: (): EChartsOption => {
        // 根据图例位置计算环形图中心位置
        const getCenterPosition = (): [string, string] => {
            if (!props.showLegend) return ['50%', '50%']

            switch (props.legendPosition) {
                case 'left':
                    return ['60%', '50%']
                case 'right':
                    return ['40%', '50%']
                case 'top':
                    return ['50%', '60%']
                case 'bottom':
                    return ['50%', '40%']
                default:
                    return ['50%', '50%']
            }
        }

        const option: EChartsOption = {
            tooltip: props.showTooltip
                ? getTooltipStyle('item', {
                      formatter: (params: { name: string; value: number; percent: number; data?: { tip?: string } }) => {
                          const tip = params.data?.tip
                          return `${escapeHtml(params.name)}: ${params.value} (${params.percent}%)${tip ? `<br/><span style="opacity:0.7">${escapeHtml(tip)}</span>` : ''}`
                      },
                  })
                : undefined,
            legend: props.showLegend ? getLegendStyle(props.legendPosition) : undefined,
            series: [
                {
                    name: '数据占比',
                    type: 'pie',
                    radius: props.radius,
                    center: getCenterPosition(),
                    avoidLabelOverlap: false,
                    itemStyle: {
                        borderRadius: props.borderRadius,
                        borderColor: isDark.value ? '#2c2c2c' : '#fff',
                        borderWidth: 0,
                    },
                    label: {
                        show: props.showLabel,
                        formatter: '{b}\n{d}%',
                        position: 'outside',
                        color: isDark.value ? '#ccc' : '#999',
                        fontSize: 12,
                        // 名称过长时截断，完整名称和路径在鼠标提示里，避免和图例、相邻标签重叠
                        width: 110,
                        overflow: 'truncate',
                    },
                    emphasis: {
                        label: {
                            show: false,
                            fontSize: 14,
                            fontWeight: 'bold',
                        },
                    },
                    labelLine: {
                        show: props.showLabel,
                        length: 15,
                        length2: 25,
                        smooth: true,
                    },
                    data: props.data,
                    color: props.colors,
                    ...getAnimationConfig(),
                    animationType: 'expansion',
                },
            ],
        }

        // 添加中心文字
        if (props.centerText) {
            const centerPos = getCenterPosition()
            option.title = {
                text: props.centerText,
                left: centerPos[0],
                top: centerPos[1],
                textAlign: 'center',
                textVerticalAlign: 'middle',
                textStyle: {
                    fontSize: 18,
                    fontWeight: 500,
                    color: isDark.value ? '#999' : '#ADB0BC',
                },
            }
        }

        return option
    },
})
</script>
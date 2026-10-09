<!-- 表格按钮 -->
<template>
    <div
        :aria-busy="isLoading"
        :class="[
            'inline-flex items-center justify-center min-w-8 h-8 px-2.5 mr-2.5 text-sm rounded-md align-middle',
            isLocked ? 'cursor-not-allowed opacity-60' : 'c-p',
            buttonClass,
        ]"
        :style="{ backgroundColor: buttonBgColor, color: iconColor }"
        @click="handleClick"
        data-api-button>
        <ArtSvgIcon :class="isLoading ? 'animate-spin' : ''" :icon="isLoading ? 'ri:loader-4-line' : iconContent" />
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'ArtButtonTable' })

interface Props {
    /** 按钮类型 */
    type?: 'add' | 'edit' | 'delete' | 'more' | 'view'
    /** 按钮图标 */
    icon?: string
    /** 按钮样式类 */
    iconClass?: string
    /** icon 颜色 */
    iconColor?: string
    /** 按钮背景色 */
    buttonBgColor?: string
    /** 外部异步状态，适用于气泡确认后才调用接口的场景 */
    loading?: boolean
    /** 点击处理函数，返回 Promise 时自动展示加载状态 */
    onClick?: () => void | Promise<void>
}

const props = withDefaults(defineProps<Props>(), { loading: false })
const internalLoading = ref(false)
const debounceLocked = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | undefined

// 默认按钮配置
const defaultButtons = {
    add: { icon: 'ri:add-fill', class: 'bg-theme/12 text-theme' },
    edit: { icon: 'ri:pencil-line', class: 'bg-secondary/12 text-secondary' },
    delete: { icon: 'ri:delete-bin-5-line', class: 'bg-error/12 text-error' },
    view: { icon: 'ri:eye-line', class: 'bg-info/12 text-info' },
    more: { icon: 'ri:more-2-fill', class: '' },
} as const

// 获取图标内容
const iconContent = computed(() => {
    return props.icon || (props.type ? defaultButtons[props.type]?.icon : '') || ''
})

// 获取按钮样式类
const buttonClass = computed(() => {
    return props.iconClass || (props.type ? defaultButtons[props.type]?.class : '') || ''
})

const isLoading = computed(() => props.loading || internalLoading.value)
const isLocked = computed(() => isLoading.value || debounceLocked.value)

// 同步操作限制连续点击；异步操作在 Promise 完成前持续锁定并显示加载图标。
const handleClick = async (): Promise<void> => {
    if (isLocked.value || !props.onClick) return
    debounceLocked.value = true
    if (debounceTimer) clearTimeout(debounceTimer)
    debounceTimer = setTimeout(() => {
        debounceLocked.value = false
    }, 300)

    const result = props.onClick()
    if (!(result instanceof Promise)) return
    internalLoading.value = true
    try {
        await result
    } finally {
        internalLoading.value = false
    }
}

onBeforeUnmount(() => {
    if (debounceTimer) clearTimeout(debounceTimer)
})
</script>
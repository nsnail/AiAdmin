<!-- 图标组件 -->
<template>
    <Icon v-bind="bindAttrs" v-if="icon" :icon="icon" class="art-svg-icon inline" />
</template>

<script lang="ts" setup>
import { Icon } from '@iconify/vue'

defineOptions({ name: 'ArtSvgIcon', inheritAttrs: false })

interface Props {
    /** Iconify icon name */
    icon?: string
}

defineProps<Props>()

const attrs = useAttrs()

const bindAttrs = computed<{ class: string; style: string }>(() => ({
    class: (attrs.class as string) || '',
    style: (attrs.style as string) || '',
}))
</script>

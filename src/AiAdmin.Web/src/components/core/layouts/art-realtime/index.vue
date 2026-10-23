<!-- 实时连接：随布局挂载建立 WebSocket 连接、卸载时关闭，不渲染任何内容 -->
<template>
    <span v-show="false" />
</template>

<script lang="ts" setup>
import { startRealtime, stopRealtime } from '@/utils/realtime'

defineOptions({ name: 'ArtRealtime' })

onMounted(startRealtime)
onBeforeUnmount(stopRealtime)
</script>
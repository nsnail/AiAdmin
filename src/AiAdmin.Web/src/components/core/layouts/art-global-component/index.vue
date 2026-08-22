<!-- 全局组件 -->
<template>
    <component v-for="componentConfig in enabledComponents" :is="componentConfig.component" :key="componentConfig.key" />
</template>

<script lang="ts" setup>
import { getEnabledGlobalComponents } from '@/config/modules/component'
defineOptions({ name: 'ArtGlobalComponent' })
const enabledComponents = computed(() => getEnabledGlobalComponents())
</script>
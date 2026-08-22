<template>
    <ElAvatar v-bind="$attrs" :src="src || undefined" :style="{ backgroundColor: color }">
        {{ initial }}
    </ElAvatar>
</template>
<script lang="ts" setup>
import { ElAvatar } from 'element-plus'
defineOptions({ inheritAttrs: false })
const props = defineProps<{ src?: string | null; name?: string | null }>()
const colors = ['#2563eb', '#0891b2', '#059669', '#d97706', '#dc2626', '#7c3aed', '#db2777']
const name = computed(() => props.name?.trim() || '')
const initial = computed(() => name.value.charAt(0).toLocaleUpperCase() || '?')
const color = computed(() => {
    const hash = [...name.value].reduce((total, char) => total + char.charCodeAt(0), 0)
    return colors[hash % colors.length]
})
</script>
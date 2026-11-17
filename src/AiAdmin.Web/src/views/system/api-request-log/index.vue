<!-- 操作日志页：路由入口，列表本体在 modules/api-request-log-list.vue，用户详情的操作日志页签复用同一个列表 -->
<template>
    <ApiRequestLogList />
</template>

<script lang="ts" setup>
import ApiRequestLogList from './modules/api-request-log-list.vue'

defineOptions({ name: 'ApiRequestLog' })
</script>
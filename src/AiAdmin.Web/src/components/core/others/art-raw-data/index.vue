<template>
  <ElScrollbar class="raw-data-scrollbar">
    <!-- highlight.js 对 JSON 文本完成转义后再输出，避免把数据当作 HTML 执行 -->
    <pre class="raw-data"><code class="hljs" v-html="formattedData" /></pre>
  </ElScrollbar>
</template>

<script setup lang="ts">
  import hljs from 'highlight.js/lib/core'
  import json from 'highlight.js/lib/languages/json'
  import 'highlight.js/styles/github.css'

  defineOptions({ name: 'ArtRawData' })

  hljs.registerLanguage('json', json)

  const props = defineProps<{
    data: unknown
  }>()

  const formattedData = computed(() => {
    const source = JSON.stringify(props.data, null, 2) ?? 'null'
    return hljs.highlight(source, { language: 'json' }).value
  })
</script>

<style scoped>
  .raw-data-scrollbar { max-height: 480px; border: 1px solid var(--el-border-color-lighter); border-radius: 4px; }
  .raw-data { margin: 0; padding: 16px; font-family: var(--el-font-family); font-size: 13px; line-height: 1.65; white-space: pre-wrap; overflow-wrap: anywhere; }
</style>
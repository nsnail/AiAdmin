<template>
  <div class="raw-data-editor">
    <ArtJsonEditor v-model="formattedData" readonly />
  </div>
</template>

<script setup lang="ts">
  import ArtJsonEditor from '@/components/core/forms/art-json-editor/index.vue'

  defineOptions({ name: 'ArtRawData' })

  const props = defineProps<{
    data: unknown
  }>()

  const formattedData = computed({
    get: () => JSON.stringify(props.data, null, 2) ?? 'null',
    set: () => undefined
  })
</script>

<style scoped>
  .raw-data-editor {
    width: 100%;
    height: 480px;
    border: 1px solid var(--el-border-color-lighter);
    border-radius: 4px;
  }
</style>

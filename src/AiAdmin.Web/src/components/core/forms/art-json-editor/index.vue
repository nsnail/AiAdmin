<template>
  <VAceEditor
    v-model:value="value"
    lang="json"
    theme="tomorrow"
    class="art-json-editor"
    :options="options"
    :readonly="readonly"
    @blur="$emit('blur')"
  />
</template>
<script setup lang="ts">
  import { VAceEditor } from 'vue3-ace-editor'
  import 'ace-builds/src-noconflict/mode-json'
  import 'ace-builds/src-noconflict/theme-tomorrow'
  defineProps<{ readonly?: boolean }>()
  defineEmits<{ blur: [] }>()
  const value = defineModel<string>({ default: '' })
  const options = { useWorker: false, tabSize: 2, useSoftTabs: true, showPrintMargin: false }
</script>
<style scoped>
  .art-json-editor {
    width: 100%;
    height: 100%;
    min-height: 220px;
  }
</style>

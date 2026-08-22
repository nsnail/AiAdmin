<template>
    <VAceEditor
        v-model:value="value"
        :options="options"
        :readonly="readonly"
        @blur="$emit('blur')"
        class="art-json-editor"
        lang="json"
        theme="tomorrow" />
</template>
<script lang="ts" setup>
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
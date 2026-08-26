<!-- 统一 JSON 编辑器，封装 Ace 编辑器的通用配置 -->
<template>
    <VAceEditor
        v-model:value="value"
        :options="options"
        :readonly="readonly"
        :style="{ height }"
        @blur="emit('blur')"
        class="art-json-editor"
        lang="json"
        theme="tomorrow" />
</template>

<script lang="ts" setup>
import { VAceEditor } from 'vue3-ace-editor'
import 'ace-builds/src-noconflict/mode-json'
import 'ace-builds/src-noconflict/theme-tomorrow'

defineOptions({ name: 'ArtJsonEditor' })

const props = withDefaults(
    defineProps<{
        readonly?: boolean
        height?: string
    }>(),
    { height: '100%', readonly: false },
)
const emit = defineEmits<{ blur: [] }>()
const value = defineModel<string>({ default: '' })
const { height, readonly } = toRefs(props)
const options = { useWorker: false, tabSize: 2, useSoftTabs: true, showPrintMargin: false }
</script>

<style scoped>
.art-json-editor {
    width: 100%;
    min-height: 220px;
}
</style>
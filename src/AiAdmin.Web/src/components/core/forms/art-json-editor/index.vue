<!-- 统一 JSON 编辑器，封装 Ace 编辑器的通用配置 -->
<template>
    <div class="art-json-editor">
        <VAceEditor
            v-model:value="value"
            :options="options"
            :readonly="readonly"
            :style="{ height }"
            :theme="editorTheme"
            @blur="emit('blur')"
            lang="json" />
        <ElButton v-if="!readonly" @click="formatJson" circle class="format-button" text type="primary">
            <ArtSvgIcon icon="ri:code-s-slash-line" />
        </ElButton>
    </div>
</template>

<script lang="ts" setup>
import { VAceEditor } from 'vue3-ace-editor'
import { ElMessage } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { useSettingStore } from '@/store/modules/setting'
import 'ace-builds/src-noconflict/mode-json'
import 'ace-builds/src-noconflict/theme-tomorrow'
import 'ace-builds/src-noconflict/theme-tomorrow_night'

defineOptions({ name: 'ArtJsonEditor' })

const props = withDefaults(
    defineProps<{
        readonly?: boolean
        height?: string
    }>(),
    { height: '300px', readonly: false },
)
const emit = defineEmits<{ blur: [] }>()
const value = defineModel<string>({ default: '' })
const { t } = useI18n()
const settingStore = useSettingStore()
// 跟随页面实际主题切换，统一适配只读预览和可编辑 JSON 输入框
const editorTheme = computed(() => (settingStore.isDark ? 'tomorrow_night' : 'tomorrow'))
const { height, readonly } = toRefs(props)
const options = {
    useWorker: false,
    tabSize: 2,
    useSoftTabs: true,
    showPrintMargin: false,
    wrap: 'free',
}
const formatJson = (): void => {
    try {
        value.value = JSON.stringify(JSON.parse(value.value), null, 2)
    } catch {
        ElMessage.warning(t('common.jsonFormatInvalid'))
    }
}
</script>

<style scoped>
.art-json-editor {
    position: relative;
    width: 100%;
    min-height: 220px;
}

.art-json-editor :deep(.ace_editor) {
    width: 100%;
}

.format-button {
    position: absolute;
    right: 6px;
    bottom: 6px;
    z-index: 2;
}

.art-json-editor :deep(.ace_line) {
    overflow-wrap: anywhere;
}
</style>
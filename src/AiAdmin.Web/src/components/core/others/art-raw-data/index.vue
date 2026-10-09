<template>
    <div :style="{ height }" class="raw-data-editor">
        <ArtJsonEditor v-model="formattedData" class="raw-data-content" height="100%" readonly />
    </div>
</template>

<script lang="ts" setup>
import ArtJsonEditor from '@/components/core/forms/art-json-editor/index.vue'

defineOptions({ name: 'ArtRawData' })

// 原始数据默认填满详情页签，也允许调用方显式指定高度
const props = withDefaults(
    defineProps<{
        data: unknown
        height?: string
    }>(),
    { height: '100%' },
)

const formattedData = computed({
    get: () => JSON.stringify(props.data, null, 2) ?? 'null',
    set: () => undefined,
})
</script>

<style scoped>
.raw-data-editor {
    width: 100%;
    box-sizing: border-box;
    border: 1px solid var(--el-border-color-lighter);
    border-radius: 4px;
}

.raw-data-content {
    height: 100%;
    min-height: 0;
}
</style>
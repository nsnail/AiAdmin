<!-- 更新日志页面：读取后端嵌入程序集的 CHANGELOG.md 并渲染 -->
<template>
    <div v-loading="loading" class="change-log page-content">
        <el-empty v-if="!loading && !blocks.length" :description="$t('changeLog.empty')" />
        <template v-for="(block, index) in blocks" :key="index">
            <component v-if="block.type === 'heading'" :is="`h${Math.min(block.level ?? 3, 6)}`" class="heading">
                <changelog-inline :inlines="block.inlines" />
            </component>
            <div v-else-if="block.type === 'item'" class="item">
                <span class="bullet">•</span>
                <span><changelog-inline :inlines="block.inlines" /></span>
            </div>
            <p v-else class="paragraph"><changelog-inline :inlines="block.inlines" /></p>
        </template>
    </div>
</template>

<script lang="ts" setup>
import { defineComponent, h, onMounted, ref, type PropType } from 'vue'
import { fetchGetChangeLog } from '@/api/system-manage'
import { parseChangeLog, type ChangeLogBlock, type ChangeLogInline } from './parse-changelog'

defineOptions({ name: 'ChangeLog' })

/** 渲染行内片段，全部通过 Vue 节点输出，不使用 v-html，内容中的标签不会被解析 */
const ChangelogInline = defineComponent({
    props: { inlines: { type: Array as PropType<ChangeLogInline[]>, required: true } },
    setup(props) {
        return () =>
            props.inlines.map((part) => {
                if (part.type === 'link') return h('a', { href: part.href, target: '_blank', rel: 'noopener noreferrer' }, part.text)
                if (part.type === 'bold') return h('strong', part.text)
                if (part.type === 'code') return h('code', part.text)
                return part.text
            })
    },
})

const loading = ref(false)
const blocks = ref<ChangeLogBlock[]>([])

/** 加载并解析更新日志 */
const load = async (): Promise<void> => {
    loading.value = true
    try {
        const result = await fetchGetChangeLog()
        blocks.value = parseChangeLog(result.content)
    } finally {
        loading.value = false
    }
}
onMounted(load)
</script>

<style scoped>
.change-log {
    padding: 20px;
    overflow-wrap: anywhere;
}
.heading {
    margin: 24px 0 10px;
    font-weight: 600;
    color: var(--el-text-color-primary);
}
.item {
    display: flex;
    gap: 8px;
    margin: 6px 0;
    color: var(--el-text-color-regular);
}
.bullet {
    color: var(--el-text-color-secondary);
}
.paragraph {
    margin: 8px 0;
    color: var(--el-text-color-secondary);
}
.change-log :deep(a) {
    color: var(--el-color-primary);
}
.change-log :deep(code) {
    padding: 1px 6px;
    background: var(--el-fill-color-light);
    border-radius: 4px;
}
</style>
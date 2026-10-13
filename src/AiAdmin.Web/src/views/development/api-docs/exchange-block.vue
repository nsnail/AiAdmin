<template>
    <div class="exchange-block">
        <div class="block-head">
            <span class="block-title">{{ title }}</span>
            <el-button :icon="CopyDocument" @click="handleCopy" link size="small" type="primary">{{ $t('apiDocs.copy') }}</el-button>
        </div>
        <pre class="block-body"><code v-html="highlighted" class="hljs"></code></pre>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { CopyDocument } from '@element-plus/icons-vue'
import { useClipboard } from '@vueuse/core'
import { ElMessage } from 'element-plus'
import hljs from 'highlight.js/lib/core'
import http from 'highlight.js/lib/languages/http'
import json from 'highlight.js/lib/languages/json'
import { $t } from '@/locales'

/** 请求与响应信息的单段展示块，内容自动换行、语法高亮并支持一键复制 */
const props = defineProps<{ title: string; text: string; language?: 'json' | 'http' | 'plain' }>()

// 只按需注册用到的语言，避免打入完整的 highlight.js
hljs.registerLanguage('json', json)
hljs.registerLanguage('http', http)

/** 转义 HTML 特殊字符，防止纯文本内容被当作标签解析 */
const escapeHtml = (value: string): string => value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

/** 高亮后的 HTML，指定 plain 或高亮失败时回退为转义后的纯文本 */
const highlighted = computed((): string => {
    if (props.language === 'plain' || !props.language) return escapeHtml(props.text)
    try {
        return hljs.highlight(props.text, { language: props.language, ignoreIllegals: true }).value
    } catch {
        return escapeHtml(props.text)
    }
})
const { copy } = useClipboard({ legacy: true })

/** 复制当前段落内容到剪贴板 */
const handleCopy = async (): Promise<void> => {
    try {
        await copy(props.text)
        ElMessage.success($t('apiDocs.copied'))
    } catch {
        ElMessage.error($t('apiDocs.copyFailed'))
    }
}
</script>

<style scoped>
.exchange-block {
    min-width: 0;
    margin-top: 10px;
}
.block-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 4px;
}
.block-title {
    font-weight: 600;
}
.block-body {
    margin: 0;
    padding: 12px;
    max-height: 360px;
    overflow: auto;
    background: var(--el-fill-color-light);
    white-space: pre-wrap;
    word-break: break-all;
    overflow-wrap: anywhere;
}
.block-body code {
    padding: 0;
    background: transparent;
    white-space: inherit;
    word-break: inherit;
    overflow-wrap: inherit;
    font-family: inherit;
}
</style>

<!-- highlight.js 生成的 span 不带 scoped 属性，主题色需放在非 scoped 样式中，颜色随深浅色主题切换 -->
<style>
.exchange-block {
    --hl-keyword: #d73a49;
    --hl-string: #0a7d3e;
    --hl-number: #b45309;
    --hl-attr: #1d4ed8;
    --hl-literal: #7c3aed;
    --hl-comment: #6b7280;
}
html.dark .exchange-block {
    --hl-keyword: #ff7b72;
    --hl-string: #7ee787;
    --hl-number: #ffa657;
    --hl-attr: #79c0ff;
    --hl-literal: #d2a8ff;
    --hl-comment: #8b949e;
}
.exchange-block .hljs-attr,
.exchange-block .hljs-attribute,
.exchange-block .hljs-title {
    color: var(--hl-attr);
}
.exchange-block .hljs-string {
    color: var(--hl-string);
}
.exchange-block .hljs-number {
    color: var(--hl-number);
}
.exchange-block .hljs-literal,
.exchange-block .hljs-keyword,
.exchange-block .hljs-built_in {
    color: var(--hl-literal);
}
.exchange-block .hljs-meta,
.exchange-block .hljs-comment {
    color: var(--hl-comment);
}
</style>
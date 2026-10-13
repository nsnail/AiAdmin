/** 更新日志解析结果：行内片段 */
export interface ChangeLogInline {
    type: 'text' | 'link' | 'bold' | 'code'
    text: string
    href?: string
}

/** 更新日志解析结果：块级节点 */
export interface ChangeLogBlock {
    type: 'heading' | 'item' | 'paragraph'
    level?: number
    inlines: ChangeLogInline[]
}

/** 匹配行内语法：[文本](链接)、**粗体**、`代码` */
const INLINE_PATTERN = /\[([^\]]+)\]\((https?:\/\/[^)\s]+)\)|\*\*([^*]+)\*\*|`([^`]+)`/g

/**
 * 解析单行文本中的行内语法，只识别 http(s) 链接，其余按纯文本处理
 * @param source 单行文本
 * @returns 行内片段列表
 */
export function parseInline(source: string): ChangeLogInline[] {
    const result: ChangeLogInline[] = []
    let last = 0
    for (const match of source.matchAll(INLINE_PATTERN)) {
        const index = match.index ?? 0
        if (index > last) result.push({ type: 'text', text: source.slice(last, index) })
        if (match[1] !== undefined) result.push({ type: 'link', text: match[1], href: match[2] })
        else if (match[3] !== undefined) result.push({ type: 'bold', text: match[3] })
        else result.push({ type: 'code', text: match[4] })
        last = index + match[0].length
    }
    if (last < source.length) result.push({ type: 'text', text: source.slice(last) })
    return result
}

/**
 * 解析 standard-version 生成的 Markdown 更新日志
 * 支持标题、无序列表和段落，空行被忽略
 * @param markdown Markdown 原文
 * @returns 块级节点列表
 */
export function parseChangeLog(markdown: string): ChangeLogBlock[] {
    const blocks: ChangeLogBlock[] = []
    for (const raw of markdown.split(/\r?\n/)) {
        const line = raw.trimEnd()
        if (!line.trim()) continue
        const heading = /^(#{1,6})\s+(.*)$/.exec(line)
        if (heading) {
            blocks.push({ type: 'heading', level: heading[1].length, inlines: parseInline(heading[2]) })
            continue
        }
        const item = /^\s*[*-]\s+(.*)$/.exec(line)
        if (item) {
            blocks.push({ type: 'item', inlines: parseInline(item[1]) })
            continue
        }
        blocks.push({ type: 'paragraph', inlines: parseInline(line.trim()) })
    }
    return blocks
}
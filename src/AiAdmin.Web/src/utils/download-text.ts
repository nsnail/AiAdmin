/**
 * 把文本内容保存为本地文件，供只显示一次的凭据等内容下载使用
 */

/**
 * 下载文本文件
 * @param fileName 保存的文件名
 * @param content 文件内容
 */
export function downloadTextFile(fileName: string, content: string): void {
    const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }))
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
}
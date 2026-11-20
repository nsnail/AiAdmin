/**
 * 列表导出文件生成：把行数据按导出列转换，并下载为 Excel 或 JSON
 * 只负责数据转换和下载，不关心接口和页面状态；导出列由 ArtTablePage 根据列定义推导
 */
import * as XLSX from 'xlsx'
import FileSaver from 'file-saver'
import { formatDateTime } from '@/utils/date'

/** 导出格式 */
export type ExportFormat = 'excel' | 'json'

/** 导出列：键为 JSON 字段名，title 为表头文案，value 取单元格值 */
export interface ExportColumn<T = any> {
    /** JSON 导出使用的字段名 */
    key: string
    /** Excel 表头，已按当前语言翻译 */
    title: string
    /** 取单元格值，返回原始值（字符串、数字、布尔、空） */
    value: (row: T) => unknown
}

/** 生成带时间戳的导出文件名 */
export function exportFileName(resource: string, format: ExportFormat): string {
    const timestamp = new Date().toISOString().replace('T', '_').replace(/:/g, '-').replace(/\..+$/, '')
    return `${resource.replace(/-/g, '_')}_${timestamp}.${format === 'excel' ? 'xlsx' : 'json'}`
}

/** 默认的单元格值转换：日期按列表同样的方式格式化，布尔和空值转为可读文本，其余原样 */
export function normalizeExportValue(value: unknown, locale: string, labels: { yes: string; no: string }): unknown {
    if (value === null || value === undefined) return ''
    if (typeof value === 'boolean') return value ? labels.yes : labels.no
    if (value instanceof Date) return formatDateTime(value, locale)
    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(value)) return formatDateTime(value, locale)
    if (Array.isArray(value)) return value.map((item) => String(item ?? '')).join(', ')
    if (typeof value === 'object') return JSON.stringify(value)
    return value
}

/** 把行转换为以表头为键的对象，供 Excel 使用 */
function toExcelRows<T>(rows: T[], columns: ExportColumn<T>[], locale: string, labels: { yes: string; no: string }): Record<string, unknown>[] {
    return rows.map((row) => Object.fromEntries(columns.map((column) => [column.title, normalizeExportValue(column.value(row), locale, labels)])))
}

/** 下载 Excel，列宽按内容自适应并限制最大宽度 */
export function downloadExcel<T>(
    rows: T[],
    columns: ExportColumn<T>[],
    fileName: string,
    sheetName: string,
    locale: string,
    labels: { yes: string; no: string },
): void {
    const exportRows = toExcelRows(rows, columns, locale, labels)
    const worksheet = XLSX.utils.json_to_sheet(exportRows)
    worksheet['!cols'] = columns.map((column) => ({
        wch: Math.min(Math.max(column.title.length + 2, ...exportRows.map((row) => String(row[column.title] ?? '').length + 2)), 50),
    }))
    const workbook = XLSX.utils.book_new()
    // Excel 工作表名最长 31 个字符且不能包含 : \ / ? * [ ]
    XLSX.utils.book_append_sheet(workbook, worksheet, sheetName.replace(/[:\\/?*[\]]/g, ' ').slice(0, 31) || 'Sheet1')
    const buffer = XLSX.write(workbook, { bookType: 'xlsx', type: 'array', compression: true })
    FileSaver.saveAs(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), fileName)
}

/** 下载 JSON，字段名使用 key，值与 Excel 保持一致 */
export function downloadJson<T>(rows: T[], columns: ExportColumn<T>[], fileName: string, locale: string, labels: { yes: string; no: string }): void {
    const exportRows = rows.map((row) =>
        Object.fromEntries(columns.map((column) => [column.key, normalizeExportValue(column.value(row), locale, labels)])),
    )
    FileSaver.saveAs(new Blob([JSON.stringify(exportRows, null, 2)], { type: 'application/json;charset=utf-8' }), fileName)
}
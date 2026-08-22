import request from '@/utils/http'

export interface ManagedFile {
    name: string
    size: number
    lastModified: string
    isDirectory: boolean
    isParent?: boolean
}

export function fetchFiles(path = '', keyword = '', sortField = 'Name', sortOrder = 'ascending') {
    return request.get<ManagedFile[]>({
        url: '/api/files',
        params: { path, keyword, sortField, sortOrder },
    })
}
export function createDirectory(path: string, name: string) {
    return request.post<void>({ url: '/api/files/directories', data: { path, name } })
}
export function uploadFile(file: File, path = '', onProgress?: (percent: number) => void) {
    const data = new FormData()
    data.append('file', file)
    return request.post<ManagedFile>({
        url: '/api/files/upload',
        params: { path },
        data,
        onUploadProgress: (event) => {
            if (event.total) onProgress?.(Math.round((event.loaded * 100) / event.total))
        },
    })
}
export function deleteFile(name: string, directory = false) {
    return request.post<void>({ url: '/api/files/delete', data: { name, directory } })
}
export function fetchFileDownloadUrl(name: string) {
    return request.get<string>({ url: '/api/files/download', params: { name } })
}

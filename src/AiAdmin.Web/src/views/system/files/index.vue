<template>
    <div class="art-full-height">
        <ElCard class="art-table-card">
            <div class="toolbar">
                <ElButton :icon="FolderAdd" @click="createFolder">新建目录</ElButton>
                <ElUpload :http-request="handleUpload" :show-file-list="false" multiple>
                    <ElButton type="primary"><ArtSvgIcon icon="ri:upload-2-line" />批量上传</ElButton>
                </ElUpload>
                <ElButton :icon="Refresh" @click="loadFiles">刷新</ElButton>
                <ElInput
                    v-model="searchKeyword"
                    @clear="loadFiles"
                    @keyup.enter="loadFiles"
                    class="file-search"
                    clearable
                    placeholder="搜索文件或目录">
                    <template #append><ElButton :icon="Search" @click="loadFiles" /></template>
                </ElInput>
                <ElButton v-if="selectedFiles.length" :icon="Delete" @click="removeSelected" type="danger"
                    >删除选中 ({{ selectedFiles.length }})</ElButton
                >
                <nav aria-label="breadcrumb" class="file-breadcrumb ml-2.5">
                    <ul class="flex-c h-7">
                        <li v-for="(item, index) in breadcrumbs" :key="item.path" class="box-border flex-c h-7 text-sm leading-7">
                            <div
                                :class="!isLastBreadcrumb(index) ? 'c-p py-1 rounded tad-200 hover:bg-active-color hover:[&_span]:text-g-600' : ''"
                                @click="!isLastBreadcrumb(index) && openPath(item.path)">
                                <span
                                    class="flex-c max-w-46 overflow-hidden text-ellipsis whitespace-nowrap px-1.5 text-sm text-g-600 dark:text-g-800">
                                    <ArtSvgIcon :icon="index === 0 ? 'ri:home-4-line' : 'ri:folder-line'" class="mr-1 shrink-0" />
                                    <span class="overflow-hidden text-ellipsis whitespace-nowrap">{{ item.name }}</span>
                                </span>
                            </div>
                            <div v-if="!isLastBreadcrumb(index)" aria-hidden="true" class="mx-1 text-sm not-italic text-g-500">/</div>
                        </li>
                    </ul>
                </nav>
            </div>
            <div v-if="uploadTasks.length" class="upload-progress-list">
                <div v-for="task in uploadTasks" :key="task.id" class="upload-progress-item">
                    <ArtSvgIcon class="shrink-0 text-g-600" icon="ri:file-upload-line" />
                    <span class="upload-file-name">{{ task.name }}</span>
                    <ElProgress
                        :percentage="task.percent"
                        :status="task.status === 'success' ? 'success' : task.status === 'error' ? 'exception' : undefined"
                        class="upload-progress" />
                </div>
            </div>
            <ArtTable
                :data="displayFiles"
                :loading="loading"
                @row-contextmenu="showContextMenu"
                @selection-change="handleSelectionChange"
                @sort-change="handleSortChange"
                stripe>
                <ElTableColumn :selectable="(row) => !row.isParent" type="selection" width="48" />
                <ElTableColumn label="名称" min-width="360" prop="name" show-overflow-tooltip sortable="custom">
                    <template #default="{ row }">
                        <ElButton
                            v-if="row.isDirectory"
                            @click="openPath(row.isParent ? parentPath : joinPath(currentPath, row.name))"
                            link
                            type="primary"
                            ><ArtSvgIcon class="file-icon" icon="ri:folder-line" />{{ trimSlash(row.name) }}</ElButton
                        >
                        <span v-else class="file-name"><ArtSvgIcon class="file-icon" icon="ri:file-line" />{{ row.name }}</span>
                    </template>
                </ElTableColumn>
                <ElTableColumn align="right" label="大小" prop="size" sortable="custom" width="120"
                    ><template #default="{ row }">{{ row.isDirectory ? '' : formatSize(row.size) }}</template></ElTableColumn
                >
                <ElTableColumn align="right" label="修改时间" prop="lastModified" sortable="custom" width="190"
                    ><template #default="{ row }">{{ formatDateTime(row.lastModified) }}</template></ElTableColumn
                >
                <ElTableColumn align="right" fixed="right" label="操作" width="160">
                    <template #default="{ row }">
                        <ArtButtonTable
                            v-if="!row.isDirectory"
                            @click="download(row)"
                            icon="ri:download-2-line"
                            icon-class="bg-theme/12 text-theme" />
                        <ArtButtonTable v-if="!row.isParent" @click="remove(row)" type="delete" />
                    </template>
                </ElTableColumn>
            </ArtTable>
            <ArtMenuRight :menu-items="contextMenuItems" @select="handleContextMenuSelect" ref="menuRef" />
        </ElCard>
    </div>
</template>
<script lang="ts" setup>
import { Delete, FolderAdd, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type UploadRequestOptions } from 'element-plus'
import { createDirectory, deleteFile, fetchFileDownloadUrl, fetchFiles, uploadFile, type ManagedFile } from '@/api/files'
import { formatDateTime } from '@/utils/date'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtMenuRight from '@/components/core/others/art-menu-right/index.vue'
import type { MenuItemType } from '@/components/core/others/art-menu-right/index.vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/store/modules/user'
const route = useRoute()
const router = useRouter()
const userStore = useUserStore()
const files = ref<ManagedFile[]>([])
const selectedFiles = ref<ManagedFile[]>([])
const menuRef = ref<InstanceType<typeof ArtMenuRight>>()
const contextFile = ref<ManagedFile | null>(null)
const uploadTasks = ref<Array<{ id: number; name: string; percent: number; status: 'uploading' | 'success' | 'error' }>>([])
const loading = ref(false)
const currentPath = ref(typeof route.query.path === 'string' ? route.query.path : '')
const isSuperAdmin = computed(() => userStore.getUserInfo.roles?.includes('R_SUPER') === true)
const sortField = ref('Name')
const sortOrder = ref('ascending')
const searchKeyword = ref('')
const breadcrumbs = computed(() => [
    { name: isSuperAdmin.value ? '根目录' : '个人目录', path: '' },
    ...currentPath.value
        .split('/')
        .filter(Boolean)
        .map((name, index, all) => ({ name, path: `${all.slice(0, index + 1).join('/')}/` })),
])
const parentPath = computed(() => {
    const segments = currentPath.value.split('/').filter(Boolean)
    return segments.length > 1 ? `${segments.slice(0, -1).join('/')}/` : ''
})
const displayFiles = computed(() =>
    currentPath.value ? [{ name: '..', size: 0, lastModified: '', isDirectory: true, isParent: true }, ...files.value] : files.value,
)
const contextMenuItems = computed<MenuItemType[]>(() => {
    const file = contextFile.value
    if (!file || file.isParent) return []
    return [
        file.isDirectory
            ? { key: 'open', label: '打开目录', icon: 'ri:folder-open-line' }
            : { key: 'download', label: '下载文件', icon: 'ri:download-2-line' },
        { key: 'copyPath', label: '复制路径', icon: 'ri:file-copy-line' },
        { key: 'delete', label: '删除', icon: 'ri:delete-bin-5-line', showLine: true },
        { key: 'refresh', label: '刷新', icon: 'ri:refresh-line' },
    ]
})
const loadFiles = async () => {
    loading.value = true
    try {
        files.value = await fetchFiles(currentPath.value, searchKeyword.value, sortField.value, sortOrder.value)
    } finally {
        loading.value = false
    }
}
const handleSelectionChange = (rows: ManagedFile[]) => {
    selectedFiles.value = rows.filter((row) => !row.isParent)
}
const showContextMenu = (row: ManagedFile, _column: unknown, event: MouseEvent) => {
    if (row.isParent) return
    contextFile.value = row
    event.preventDefault()
    menuRef.value?.show(event)
}
const handleContextMenuSelect = async (item: MenuItemType) => {
    const file = contextFile.value
    if (!file) return
    if (item.key === 'open') await openPath(joinPath(currentPath.value, file.name))
    else if (item.key === 'download') await download(file)
    else if (item.key === 'copyPath') await navigator.clipboard?.writeText(joinPath(currentPath.value, file.name))
    else if (item.key === 'delete') await remove(file)
    else if (item.key === 'refresh') await loadFiles()
}
const handleSortChange = async ({ prop, order }: { prop: string; order: 'ascending' | 'descending' | null }) => {
    sortField.value = prop || 'Name'
    sortOrder.value = order || 'ascending'
    await loadFiles()
}
const openPath = async (path: string) => {
    currentPath.value = path
    await router.replace({
        query: path ? { ...route.query, path } : { ...route.query, path: undefined },
    })
    await loadFiles()
}
watch(
    () => route.query.path,
    async (path) => {
        const nextPath = typeof path === 'string' ? path : ''
        if (nextPath !== currentPath.value) {
            currentPath.value = nextPath
            await loadFiles()
        }
    },
)
const isLastBreadcrumb = (index: number) => index === breadcrumbs.value.length - 1
const handleUpload = async (options: UploadRequestOptions) => {
    const task = reactive({
        id: options.file.uid,
        name: options.file.name,
        percent: 0,
        status: 'uploading' as 'uploading' | 'success' | 'error',
    })
    uploadTasks.value.push(task)
    try {
        const result = await uploadFile(options.file, currentPath.value, (percent) => {
            task.percent = percent
        })
        task.percent = 100
        task.status = 'success'
        options.onSuccess(result)
        await loadFiles()
        window.setTimeout(() => {
            uploadTasks.value = uploadTasks.value.filter((item) => item.id !== task.id)
        }, 2500)
    } catch (error) {
        task.status = 'error'
        options.onError(error as Error)
    }
}
const createFolder = async () => {
    const result = await ElMessageBox.prompt('请输入目录名称', '新建目录', {
        confirmButtonText: '创建',
        cancelButtonText: '取消',
        inputPattern: /[^\\/]+/,
        inputErrorMessage: '目录名称不能为空',
    })
    await createDirectory(currentPath.value, result.value)
    await loadFiles()
}
const download = async (file: ManagedFile) => {
    window.open(await fetchFileDownloadUrl(joinPath(currentPath.value, file.name)), '_blank')
}
const remove = async (file: ManagedFile) => {
    try {
        const label = file.isDirectory ? `目录“${trimSlash(file.name)}”及其全部内容` : `文件“${file.name}”`
        await ElMessageBox.confirm(`确定删除${label}？`, '删除确认')
        await deleteFile(joinPath(currentPath.value, file.name), file.isDirectory)
        ElMessage.success('删除成功')
        await loadFiles()
    } catch (error) {
        if (error !== 'cancel' && error !== 'close') throw error
    }
}
const removeSelected = async () => {
    try {
        await ElMessageBox.confirm(`确定删除选中的 ${selectedFiles.value.length} 项？目录将递归删除其全部内容。`, '批量删除确认')
        for (const file of selectedFiles.value) {
            await deleteFile(joinPath(currentPath.value, file.name), file.isDirectory)
        }
        ElMessage.success('删除成功')
        selectedFiles.value = []
        await loadFiles()
    } catch (error) {
        if (error !== 'cancel' && error !== 'close') throw error
    }
}
const joinPath = (base: string, name: string) => `${base}${name}`
const trimSlash = (value: string) => value.replace(/\/$/, '')
const formatSize = (size: number) =>
    size < 1024 ? `${size} B` : size < 1048576 ? `${(size / 1024).toFixed(1)} KB` : `${(size / 1048576).toFixed(1)} MB`
onMounted(loadFiles)
</script>
<style scoped>
.toolbar {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    margin-bottom: 12px;
}
.file-name {
    display: inline-flex;
    align-items: center;
}
.file-icon {
    margin-right: 8px;
}
.upload-progress-list {
    display: grid;
    gap: 8px;
    padding: 10px 12px;
    margin-bottom: 12px;
    background: var(--el-fill-color-lighter);
    border-radius: 4px;
}
.upload-progress-item {
    display: grid;
    grid-template-columns: auto minmax(120px, 240px) minmax(180px, 1fr);
    gap: 10px;
    align-items: center;
}
.upload-file-name {
    overflow: hidden;
    color: var(--el-text-color-regular);
    text-overflow: ellipsis;
    white-space: nowrap;
}
.upload-progress {
    min-width: 0;
}
.file-search {
    width: 240px;
}
@media (width <= 640px) {
    .upload-progress-item {
        grid-template-columns: auto minmax(0, 1fr);
    }
    .upload-progress {
        grid-column: 1 / -1;
    }
}
</style>

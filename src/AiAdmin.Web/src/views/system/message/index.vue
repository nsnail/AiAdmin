<template>
    <div class="message-page art-full-height">
        <MessageSearch v-model="searchForm" @reset="resetSearch" @search="search" />
        <ElCard class="art-table-card">
            <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData">
                <template #left
                    ><ElSpace
                        ><ElButton @click="openCreate" type="primary">{{ t('messageManagement.send') }}</ElButton
                        ><ElButton :disabled="!selectedRows.length" @click="batchDelete">{{ t('messageManagement.batchDelete') }}</ElButton></ElSpace
                    ></template
                >
            </ArtTableHeader>
            <ArtTable
                :columns="columns"
                :data="data"
                :loading="loading"
                :pagination="pagination"
                @cell-query="applyCellQuery"
                @pagination:current-change="handleCurrentChange"
                @pagination:size-change="handleSizeChange"
                @selection-change="selectedRows = $event" />
        </ElCard>
        <ElDialog v-model="editorVisible" :title="editingId ? t('messageManagement.edit') : t('messageManagement.send')" destroy-on-close fullscreen>
            <ElForm :model="form" class="editor-form" label-position="top">
                <ElFormItem :label="t('messageManagement.title')"><ElInput v-model="form.title" maxlength="200" show-word-limit /></ElFormItem>
                <ElFormItem
                    ><ElCheckbox v-model="form.isPopup">{{ t('messageManagement.popup') }}</ElCheckbox></ElFormItem
                >
                <ElFormItem :label="t('messageManagement.target')"
                    ><ElSelect v-model="form.targetType" class="w-full"
                        ><ElOption v-for="item in targetOptions" :key="item.value" :label="item.label" :value="item.value" /></ElSelect
                ></ElFormItem>
                <ElFormItem
                    v-if="form.targetType === 'department' || form.targetType === 'department_children'"
                    :label="t('messageManagement.department')"
                    ><ElSelect v-model="form.departmentIds" :placeholder="t('messageManagement.selectDepartment')" class="w-full" filterable multiple
                        ><ElOption v-for="item in departmentOptions" :key="item.id" :label="item.name" :value="Number(item.id)" /></ElSelect
                ></ElFormItem>
                <ElFormItem v-if="form.targetType === 'user'" :label="t('messageManagement.user')"
                    ><ElSelect
                        v-model="form.userIds"
                        :loading="userLoading"
                        :placeholder="t('messageManagement.selectUser')"
                        :remote-method="searchUsers"
                        class="w-full"
                        filterable
                        multiple
                        remote
                        ><ElOption
                            v-for="item in users"
                            :key="item.id"
                            :label="`${item.userName} (${item.userEmail})`"
                            :value="Number(item.id)" /></ElSelect
                ></ElFormItem>
                <ElFormItem :label="t('messageManagement.content')"><div class="editor-host" ref="editorElement" /></ElFormItem>
            </ElForm>
            <template #footer
                ><ElButton @click="preview">{{ t('messageManagement.preview') }}</ElButton
                ><ElButton @click="editorVisible = false">{{ t('common.cancel') }}</ElButton
                ><ElButton :loading="sending" @click="send" type="primary">{{ t('messageManagement.send') }}</ElButton></template
            >
        </ElDialog>
        <ElDialog v-model="previewVisible" :title="form.title" width="900px"><div v-html="previewHtml" class="message-content" /></ElDialog>
        <ElDialog v-model="viewVisible" :title="selected?.title" width="900px"><div v-html="selected?.content" class="message-content" /></ElDialog>
    </div>
</template>
<script lang="ts" setup>
import { h } from 'vue'
import { AiEditor } from 'aieditor'
import 'aieditor/dist/style.css'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchBatchDeleteSystemMessages,
    fetchDeleteSystemMessage,
    fetchGetDepartmentTree,
    fetchGetSystemMessages,
    fetchGetUserList,
    fetchSendSystemMessage,
    fetchUpdateSystemMessage,
    type DynamicFilter,
} from '@/api/system-manage'
import { formatDateTime } from '@/utils/date'
import { useI18n } from 'vue-i18n'
import mittBus from '@/utils/sys/mittBus'
import MessageSearch from './modules/message-search.vue'
const { t } = useI18n()
const searchForm = ref<Api.SystemManage.SystemMessageSearchParams>({})
const sending = ref(false)
const userLoading = ref(false)
const editorVisible = ref(false)
const previewVisible = ref(false)
const viewVisible = ref(false)
const users = ref<Api.SystemManage.UserListItem[]>([])
const departmentOptions = ref<{ id: string; name: string }[]>([])
const editorElement = ref<HTMLElement>()
let editor: AiEditor | undefined
const selected = ref<Api.SystemManage.SystemMessageListItem>()
const previewHtml = ref('')
const selectedRows = ref<Api.SystemManage.SystemMessageListItem[]>([])
const editingId = ref<number>()
const form = reactive<Api.SystemManage.SendSystemMessageParams>({
    title: '',
    content: '',
    targetType: 'all',
    departmentIds: [],
    userIds: [],
    isPopup: false,
})
const getQuery = () => {
    const query: { keyword?: string; startTime?: string; endTime?: string } = { keyword: searchForm.value.title }
    const filters = searchForm.value.dynamicFilter?.filters ?? ([searchForm.value.dynamicFilter].filter(Boolean) as DynamicFilter[])
    for (const filter of filters) {
        if (filter?.field === 'Title') query.keyword = String(filter.value ?? '')
        if (filter?.field === 'CreatedAt' && Array.isArray(filter.value)) {
            query.startTime = String(filter.value[0])
            query.endTime = String(filter.value[1])
        }
    }
    return query
}
const {
    data,
    columns,
    columnChecks,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    refreshData,
    resetColumns,
} = useTable({
    core: {
        apiFn: ({ current, size }) =>
            fetchGetSystemMessages({ current, size, ...getQuery() }).then((records) => ({ records, current, size, total: records.length })),
        apiParams: { current: 1, size: 20 },
        columnsFactory: () => [
            { type: 'selection', width: 50 },
            {
                prop: 'title',
                queryField: 'Title',
                queryValueField: 'title',
                queryValueType: 'string',
                label: t('messageManagement.title'),
                minWidth: 240,
            },
            { prop: 'recipientCount', label: t('messageManagement.recipientCount'), width: 120 },
            { prop: 'createdAt', label: t('messageManagement.createdAt'), width: 190, formatter: (row) => formatDateTime(row.createdAt) },
            {
                prop: 'actions',
                label: t('messageManagement.actions'),
                width: 150,
                fixed: 'right',
                formatter: (row) =>
                    h('div', { class: 'flex gap-1' }, [
                        h(ArtButtonTable, { type: 'view', onClick: () => view(row) }),
                        h(ArtButtonTable, { type: 'edit', onClick: () => openEdit(row) }),
                        h(ArtButtonTable, { type: 'delete', onClick: () => deleteOne(row.id) }),
                    ]),
            },
        ],
    },
})
const targetOptions = computed(() => [
    { value: 'all', label: t('messageManagement.allUsers') },
    { value: 'department', label: t('messageManagement.departmentOnly') },
    { value: 'department_children', label: t('messageManagement.departmentChildren') },
    { value: 'user', label: t('messageManagement.specificUsers') },
])
const flatten = (items: Api.SystemManage.DepartmentTreeItem[], prefix = '') =>
    items.flatMap((x) => [{ id: x.id, name: prefix + x.name }, ...flatten(x.children ?? [], `${prefix}${x.name} / `)])
const load = async () => {
    departmentOptions.value = flatten(await fetchGetDepartmentTree())
}
const searchUsers = async (query: string) => {
    userLoading.value = true
    try {
        const filter: DynamicFilter | undefined = query ? { field: 'UserName', operator: 'Contains', value: query } : undefined
        users.value = (await fetchGetUserList({ current: 1, size: 30, dynamicFilter: filter })).records
    } finally {
        userLoading.value = false
    }
}
const openCreate = async () => {
    editingId.value = undefined
    Object.assign(form, { title: '', content: '', targetType: 'all', departmentIds: [], userIds: [] })
    await openEditor()
}
const openEdit = async (row: Api.SystemManage.SystemMessageListItem) => {
    editingId.value = row.id
    form.title = row.title
    form.content = row.content
    await openEditor()
}
const openEditor = async () => {
    editorVisible.value = true
    await nextTick()
    editor?.destroy()
    editor = new AiEditor({
        element: editorElement.value!,
        placeholder: t('messageManagement.contentPlaceholder'),
        toolbarKeys: ['undo', 'redo', 'heading', 'bold', 'italic', 'underline', 'bulletList', 'orderedList', 'link', 'blockquote'],
        content: form.content,
    })
}
const preview = () => {
    if (editor) {
        previewHtml.value = editor.getHtml()
        form.content = previewHtml.value
    }
    previewVisible.value = true
}
const send = async () => {
    if (!form.title.trim() || !editor) return
    sending.value = true
    try {
        form.content = editor.getHtml()
        if (editingId.value) await fetchUpdateSystemMessage(editingId.value, { title: form.title, content: form.content })
        else await fetchSendSystemMessage(form)
        editorVisible.value = false
        mittBus.emit('refreshNotifications')
        await refreshData()
    } finally {
        sending.value = false
    }
}
const deleteOne = async (id: number) => {
    await ElMessageBox.confirm(t('messageManagement.delete'), t('common.tips'))
    await fetchDeleteSystemMessage(id)
    await refreshData()
}
const batchDelete = async () => {
    if (!selectedRows.value.length) return
    await ElMessageBox.confirm(`${t('messageManagement.batchDelete')} (${selectedRows.value.length})`, t('common.tips'))
    await fetchBatchDeleteSystemMessages(selectedRows.value.map((x) => x.id))
    selectedRows.value = []
    await refreshData()
}
const view = (item: Api.SystemManage.SystemMessageListItem) => {
    selected.value = item
    viewVisible.value = true
}
const search = (params: Api.SystemManage.SystemMessageSearchParams) => {
    searchForm.value = params
    replaceSearchParams(params)
    void getData()
}
const resetSearch = () => {
    searchForm.value = {}
    resetSearchParams()
    void getData()
}
const applyCellQuery = async (condition: { field: string; operator: string; value: unknown }) => {
    if (condition.field === 'Title') {
        searchForm.value = { title: String(condition.value ?? '') }
        replaceSearchParams(searchForm.value)
        await getData()
    }
}
watch(useI18n().locale, () => resetColumns?.())
onMounted(load)
onBeforeUnmount(() => editor?.destroy())
</script>
<style scoped>
.editor-form {
    width: 100%;
}
.editor-host {
    width: 100%;
    height: calc(100vh - 260px);
    min-height: 500px;
    border: 1px solid var(--el-border-color);
}
.editor-host :deep(.aie-container) {
    height: 100%;
}
.editor-host :deep(.aie-content) {
    min-height: 0;
    height: calc(100% - 42px);
}
.message-content {
    max-height: 70vh;
    overflow: auto;
    line-height: 1.7;
}
</style>
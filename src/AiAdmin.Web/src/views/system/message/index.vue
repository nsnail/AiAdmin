<template>
    <div class="message-page art-full-height">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :loading="loading"
            :pagination="pagination"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @selection-change="selectedRows = $event"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            resource="message">
            <template #header-left>
                <ElSpace wrap>
                    <ElButton @click="openCreate" type="primary">{{ t('messageManagement.send') }}</ElButton>
                    <ElButton :disabled="!selectedRows.length" @click="confirmBatchDelete" plain type="danger">
                        {{ t('messageManagement.batchDelete') }}
                    </ElButton>
                </ElSpace>
            </template>
        </ArtTablePage>
        <ElDialog
            v-model="editorVisible"
            :title="editingId ? t('messageManagement.edit') : t('messageManagement.send')"
            class="message-editor-dialog"
            destroy-on-close
            fullscreen>
            <ElForm :model="form" class="editor-form" label-position="top">
                <ElFormItem><ElInput v-model="form.title" :placeholder="t('messageManagement.title')" maxlength="200" show-word-limit /></ElFormItem>
                <ElFormItem
                    ><ElCheckbox v-model="form.isPopup">{{ t('messageManagement.popup') }}</ElCheckbox></ElFormItem
                >
                <ElFormItem
                    ><ElSelect v-model="form.targetType" :placeholder="t('messageManagement.target')" class="w-full"
                        ><ElOption v-for="item in targetOptions" :key="item.value" :label="item.label" :value="item.value" /></ElSelect
                ></ElFormItem>
                <ElFormItem v-if="form.targetType === 'department' || form.targetType === 'department_children'"
                    ><ElSelect v-model="form.departmentIds" :placeholder="t('messageManagement.selectDepartment')" class="w-full" filterable multiple
                        ><ElOption v-for="item in departmentOptions" :key="item.id" :label="item.name" :value="Number(item.id)" /></ElSelect
                ></ElFormItem>
                <ElFormItem v-if="form.targetType === 'user'"
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
                <ElFormItem><div class="editor-host" ref="editorElement" /></ElFormItem>
            </ElForm>
            <template #footer
                ><ElButton @click="preview">{{ t('messageManagement.preview') }}</ElButton
                ><ElButton @click="editorVisible = false">{{ t('common.cancel') }}</ElButton
                ><ElButton :loading="sending" @click="send" type="primary">{{ t('messageManagement.send') }}</ElButton></template
            >
        </ElDialog>
        <ElDialog v-model="previewVisible" :title="form.title" width="900px"><div v-html="previewHtml" class="message-content" /></ElDialog>
        <ElDialog v-model="recipientsVisible" :title="recipientsTitle" width="900px">
            <ArtTable :columns="recipientColumns" :data="recipients" />
        </ElDialog>
    </div>
</template>
<script lang="ts" setup>
import { h } from 'vue'
import { ElMessageBox, ElTag } from 'element-plus'
import { AiEditor } from 'aieditor'
import 'aieditor/dist/style.css'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchBatchDeleteSystemMessages,
    fetchDeleteSystemMessage,
    fetchGetDepartmentTree,
    fetchGetSystemMessages,
    fetchGetSystemMessageRecipients,
    fetchGetUserList,
    fetchSendSystemMessage,
    fetchUpdateSystemMessage,
    type DynamicFilter,
} from '@/api/system-manage'
import { useI18n } from 'vue-i18n'
import mittBus from '@/utils/sys/mittBus'
const { t } = useI18n()
const sending = ref(false)
const userLoading = ref(false)
const editorVisible = ref(false)
const previewVisible = ref(false)
const recipientsVisible = ref(false)
const recipientsTitle = ref('')
const recipients = ref<Api.SystemManage.SystemMessageRecipientItem[]>([])
const users = ref<Api.SystemManage.UserListItem[]>([])
const departmentOptions = ref<{ id: string; name: string }[]>([])
const editorElement = ref<HTMLElement>()
let editor: AiEditor | undefined
const previewHtml = ref('')
const selectedRows = ref<Api.SystemManage.SystemMessageListItem[]>([])
const editingId = ref<string>()
const form = reactive<Api.SystemManage.SendSystemMessageParams>({
    title: '',
    content: '',
    targetType: 'all',
    departmentIds: [],
    userIds: [],
    isPopup: false,
})
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
        apiFn: fetchGetSystemMessages,
        apiParams: { current: 1, size: 20 },
        columnsFactory: () => [
            { type: 'selection', width: 50 },
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 160,
                align: 'left',
                formatter: (row) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'title',
                queryField: 'Title',
                queryValueField: 'title',
                queryValueType: 'string',
                label: t('messageManagement.title'),
                minWidth: 240,
            },
            {
                prop: 'isPopup',
                queryField: 'IsPopup',
                queryValueField: 'isPopup',
                queryValueType: 'boolean',
                label: t('messageManagement.popup'),
                width: 120,
                align: 'center',
                formatter: (row) =>
                    h(ElTag, { type: row.isPopup ? 'success' : 'info' }, () => t(row.isPopup ? 'listFilter.option.yes' : 'listFilter.option.no')),
            },
            {
                prop: 'recipientCount',
                queryField: 'RecipientCount',
                queryValueType: 'number',
                label: t('messageManagement.recipientCount'),
                width: 120,
                align: 'right',
            },
            {
                prop: 'actions',
                label: t('messageManagement.actions'),
                width: 150,
                fixed: 'right',
                formatter: (row) =>
                    h('div', { class: 'flex gap-1' }, [
                        h(ArtButtonTable, { type: 'view', onClick: () => viewRecipients(row) }),
                        h(ArtButtonTable, { type: 'edit', onClick: () => openEdit(row) }),
                        h(
                            ElPopconfirm,
                            {
                                cancelButtonText: t('common.cancel'),
                                confirmButtonText: t('common.confirm'),
                                title: t('messageManagement.deleteConfirm'),
                                width: 280,
                                onConfirm: () => deleteOne(row.id),
                            },
                            { reference: () => h(ArtButtonTable, { type: 'delete' }) },
                        ),
                    ]),
            },
        ],
    },
})
const recipientColumns = computed(() => [
    { prop: 'userName', label: t('messageManagement.user'), minWidth: 160 },
    { prop: 'userEmail', label: t('messageManagement.email'), minWidth: 220 },
    {
        prop: 'isRead',
        label: t('messageManagement.readStatus'),
        width: 130,
        formatter: (row: Api.SystemManage.SystemMessageRecipientItem) =>
            h(ElTag, { type: row.isRead ? 'success' : 'info' }, () => t(row.isRead ? 'messageManagement.read' : 'messageManagement.unread')),
    },
    {
        prop: 'isDeleted',
        label: t('messageManagement.deleteStatus'),
        width: 130,
        formatter: (row: Api.SystemManage.SystemMessageRecipientItem) =>
            h(ElTag, { type: row.isDeleted ? 'danger' : 'info' }, () =>
                t(row.isDeleted ? 'messageManagement.deleted' : 'messageManagement.notDeleted'),
            ),
    },
])
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
        toolbarKeys: [
            'undo',
            'redo',
            'brush',
            'eraser',
            'divider',
            'heading',
            'font-family',
            'font-size',
            'divider',
            'bold',
            'italic',
            'underline',
            'strike',
            'link',
            'code',
            'subscript',
            'superscript',
            'hr',
            'todo',
            'emoji',
            'divider',
            'highlight',
            'font-color',
            'divider',
            'align',
            'line-height',
            'divider',
            'bullet-list',
            'ordered-list',
            'indent-decrease',
            'indent-increase',
            'break',
            'divider',
            'image',
            'video',
            'attachment',
            'quote',
            'container',
            'code-block',
            'table',
            'divider',
            'source-code',
            'printer',
            'fullscreen',
        ],
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
const deleteOne = async (id: string) => {
    await fetchDeleteSystemMessage(id)
    await refreshData()
}
const batchDelete = async () => {
    if (!selectedRows.value.length) return
    await fetchBatchDeleteSystemMessages(selectedRows.value.map((x) => x.id))
    selectedRows.value = []
    await refreshData()
}
const confirmBatchDelete = async (): Promise<void> => {
    if (!selectedRows.value.length) return
    try {
        await ElMessageBox.confirm(t('messageManagement.batchDeleteConfirm'), t('messageManagement.batchDelete'), {
            cancelButtonText: t('common.cancel'),
            confirmButtonText: t('common.confirm'),
            type: 'warning',
        })
        await batchDelete()
    } catch {
        // 用户取消确认时保持当前选择和列表状态
    }
}
const viewRecipients = async (row: Api.SystemManage.SystemMessageListItem) => {
    recipientsTitle.value = row.title
    recipients.value = await fetchGetSystemMessageRecipients(row.id)
    recipientsVisible.value = true
}
const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
watch(useI18n().locale, () => resetColumns?.())
onMounted(load)
onBeforeUnmount(() => editor?.destroy())
</script>
<style scoped>
.editor-form {
    width: 100%;
}
.message-editor-dialog :deep(.el-dialog__body) {
    flex: 1 1 auto;
    min-height: 0;
    overflow-y: auto;
}
.message-editor-dialog :deep(.el-dialog) {
    display: flex;
    flex-direction: column;
    height: 100vh;
    margin: 0;
}
.message-editor-dialog :deep(.el-dialog__footer) {
    flex: 0 0 auto;
    display: flex;
    justify-content: flex-end;
    gap: 8px;
}
.editor-host {
    width: 100%;
    height: min(520px, max(220px, calc(100vh - 420px)));
    min-height: 220px;
    border: 1px solid var(--el-border-color);
    overflow: hidden;
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
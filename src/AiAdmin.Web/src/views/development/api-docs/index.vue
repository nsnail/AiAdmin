<template>
    <div class="api-docs page-content">
        <div class="toolbar">
            <el-input v-model="keyword" :placeholder="$t('apiDocs.search')" class="search" clearable />
            <el-button :loading="loading" @click="load"
                ><el-icon><Refresh /></el-icon>{{ $t('apiDocs.refresh') }}</el-button
            >
        </div>
        <el-empty v-if="!loading && filteredGroups.length === 0" :description="$t('apiDocs.empty')" />
        <el-collapse v-else v-model="openedGroups">
            <el-collapse-item v-for="group in filteredGroups" :key="group.name" :name="group.name">
                <template #title
                    ><span class="group-title">{{ group.name }}</span
                    ><span class="muted">{{ group.description }}</span></template
                >
                <div v-for="item in group.items" :key="item.method + item.path" class="endpoint">
                    <div @click="toggle(item)" class="endpoint-head">
                        <el-tag :type="methodType(item.method)" effect="dark">{{ item.method }}</el-tag>
                        <code>{{ item.path }}</code
                        ><span class="endpoint-name">{{ item.name }}</span>
                        <el-icon class="expand"><ArrowDown v-if="isOpen(item)" /><ArrowRight v-else /></el-icon>
                    </div>
                    <div v-if="isOpen(item)" class="endpoint-detail">
                        <p class="description">{{ item.description }}</p>
                        <el-tabs>
                            <el-tab-pane :label="$t('apiDocs.parameters')">
                                <el-table v-if="item.parameters.length" :data="item.parameters" size="small">
                                    <el-table-column :label="$t('apiDocs.name')" prop="name" width="150" />
                                    <el-table-column :label="$t('apiDocs.location')" prop="in" width="110" />
                                    <el-table-column :label="$t('apiDocs.type')" prop="type" width="130" />
                                    <el-table-column :label="$t('apiDocs.required')" width="90"
                                        ><template #default="scope"
                                            ><el-tag v-if="scope.row.required" size="small" type="danger">{{ $t('apiDocs.yes') }}</el-tag></template
                                        ></el-table-column
                                    >
                                    <el-table-column :label="$t('apiDocs.description')" prop="description" />
                                </el-table>
                                <el-empty v-else :description="$t('apiDocs.noParameters')" :image-size="55" />
                                <type-preview v-if="item.requestBody" :title="$t('apiDocs.requestBody')" :type="item.requestBody" />
                                <type-preview v-if="item.responseType" :title="$t('apiDocs.response')" :type="item.responseType" />
                            </el-tab-pane>
                            <el-tab-pane :label="$t('apiDocs.tryIt')">
                                <div class="debug-form">
                                    <el-input v-model="debug[item.path].url" :prepend="$t('apiDocs.url')" />
                                    <el-input
                                        v-if="item.parameters.length"
                                        v-model="debug[item.path].params"
                                        :placeholder="$t('apiDocs.paramsJson')"
                                        :rows="3"
                                        type="textarea" />
                                    <el-input
                                        v-if="item.requestBody"
                                        v-model="debug[item.path].body"
                                        :placeholder="$t('apiDocs.bodyJson')"
                                        :rows="6"
                                        type="textarea" />
                                    <el-button :loading="debug[item.path].loading" @click="send(item)" type="primary"
                                        ><el-icon><Promotion /></el-icon>{{ $t('apiDocs.send') }}</el-button
                                    >
                                    <pre v-if="debug[item.path].result" class="result">{{ debug[item.path].result }}</pre>
                                </div>
                            </el-tab-pane>
                        </el-tabs>
                    </div>
                </div>
            </el-collapse-item>
        </el-collapse>
    </div>
</template>

<script lang="ts" setup>
import { ArrowDown, ArrowRight, Promotion, Refresh } from '@element-plus/icons-vue'
import { computed, defineComponent, h, onMounted, reactive, ref } from 'vue'
import { ElTable, ElTableColumn } from 'element-plus'
import { fetchGetApiDocumentation } from '@/api/system-manage'
import request from '@/utils/http'

const TypePreview = defineComponent({
    props: { title: String, type: { type: Object, required: true } },
    setup(props) {
        return () =>
            h('div', { class: 'type-preview' }, [
                h('h4', props.title),
                h('code', `${props.type.name} (${props.type.type})`),
                props.type.properties?.length
                    ? h(
                          ElTable,
                          { data: props.type.properties, size: 'small' },
                          {
                              default: () => [
                                  h(ElTableColumn, { prop: 'name', label: 'Name' }),
                                  h(ElTableColumn, { prop: 'type', label: 'Type' }),
                                  h(ElTableColumn, { prop: 'description', label: 'Description' }),
                              ],
                          },
                      )
                    : null,
            ])
    },
})
const loading = ref(false)
const keyword = ref('')
const groups = ref<Api.SystemManage.ApiDocumentationGroup[]>([])
const openedGroups = ref<string[]>([])
const open = ref<string | null>(null)
const debug = reactive<Record<string, { url: string; params: string; body: string; loading: boolean; result: string }>>({})
const filteredGroups = computed(() =>
    groups.value
        .map((g) => ({
            ...g,
            items: g.items.filter(
                (i) => !keyword.value || `${i.path} ${i.name} ${i.description}`.toLowerCase().includes(keyword.value.toLowerCase()),
            ),
        }))
        .filter((g) => g.items.length),
)
const load = async () => {
    loading.value = true
    try {
        groups.value = await fetchGetApiDocumentation().then((x) => x.groups)
        openedGroups.value = groups.value.map((x) => x.name)
    } finally {
        loading.value = false
    }
}
const toggle = (item: Api.SystemManage.ApiDocumentationItem) => {
    open.value = open.value === item.method + item.path ? null : item.method + item.path
    if (!debug[item.path]) debug[item.path] = { url: item.path, params: '{}', body: '{}', loading: false, result: '' }
}
const isOpen = (item: Api.SystemManage.ApiDocumentationItem) => open.value === item.method + item.path
const methodType = (method: string) => ({ GET: 'success', POST: '', PUT: 'warning', DELETE: 'danger', PATCH: 'info' })[method] as any
const send = async (item: Api.SystemManage.ApiDocumentationItem) => {
    const state = debug[item.path]
    state.loading = true
    try {
        const params = JSON.parse(state.params || '{}')
        const body = JSON.parse(state.body || '{}')
        const res = await request.request({
            url: state.url,
            method: item.method,
            ...(item.method === 'GET' || item.method === 'DELETE' ? { params } : { data: body }),
        })
        state.result = JSON.stringify(res, null, 2)
    } catch (error) {
        state.result = String(error)
    } finally {
        state.loading = false
    }
}
onMounted(load)
</script>

<style scoped>
.api-docs {
    padding: 20px;
}
.toolbar {
    display: flex;
    gap: 12px;
    margin-bottom: 16px;
}
.search {
    max-width: 360px;
}
.group-title {
    font-weight: 600;
    margin-right: 14px;
}
.muted {
    color: var(--el-text-color-secondary);
}
.endpoint {
    border: 1px solid var(--el-border-color-lighter);
    margin: 8px 0;
}
.endpoint-head {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px;
    cursor: pointer;
}
.endpoint-head code {
    min-width: 260px;
}
.endpoint-name {
    color: var(--el-text-color-secondary);
}
.expand {
    margin-left: auto;
}
.endpoint-detail {
    padding: 0 16px 16px;
}
.description {
    color: var(--el-text-color-secondary);
}
.debug-form {
    display: grid;
    gap: 12px;
}
.result {
    background: var(--el-fill-color-light);
    padding: 12px;
    max-height: 360px;
    overflow: auto;
}
.type-preview {
    margin-top: 18px;
}
.type-preview h4 {
    margin: 0 0 8px;
}
</style>

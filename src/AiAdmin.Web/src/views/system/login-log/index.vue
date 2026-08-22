<template>
    <div class="login-log-page art-full-height">
        <ArtSearchBar
            v-model="searchForm"
            :advanced-query-fields="advancedQueryFields"
            :filter-fields="filterFields"
            @reset="handleReset"
            @search="handleSearch" />
        <ElCard class="art-table-card">
            <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData" />
            <ArtTable
                :columns="columns"
                :data="data"
                :loading="loading"
                :pagination="pagination"
                @cell-query="applyCellQuery"
                @pagination:current-change="handleCurrentChange"
                @pagination:size-change="handleSizeChange"
                @sort-change="handleSortChange" />
        </ElCard>
        <ElDialog v-model="detailVisible" :title="t('loginLog.detail.title')" destroy-on-close width="850px">
            <ElTabs v-if="selectedLog" v-model="activeDetailTab" type="card">
                <ElTabPane :label="t('loginLog.detail.tabs.details')" name="details">
                    <ElDescriptions :column="2" :label-width="150" border>
                        <ElDescriptionsItem v-for="field in detailFields" :key="field" :label="t(`loginLog.fields.${field}`)">
                            <span class="detail-value">{{ formatValue(selectedLog[field]) }}</span>
                        </ElDescriptionsItem>
                    </ElDescriptions>
                </ElTabPane>
                <ElTabPane :label="t('loginLog.detail.tabs.rawData')" name="rawData">
                    <ArtRawData :data="selectedLog" />
                </ElTabPane>
            </ElTabs>
            <template #footer
                ><ElButton @click="detailVisible = false">{{ t('loginLog.detail.close') }}</ElButton></template
            >
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { ElButton, ElDescriptions, ElDescriptionsItem, ElDialog } from 'element-plus'
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchGetListFilterFields, fetchGetLoginLogList, type ListFilterField, type LoginLogRecord } from '@/api/system-manage'
import type { DynamicFilter, DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useTable } from '@/hooks/core/useTable'
import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'

defineOptions({ name: 'LoginLog' })
const { t } = useI18n()
const filterFields = ref<ListFilterField[]>([])
const searchForm = ref<Record<string, unknown> & { dynamicFilter?: DynamicFilter }>({})
const detailVisible = ref(false)
const selectedLog = ref<LoginLogRecord>()
const activeDetailTab = ref('details')
const detailFields = [
    'userName',
    'ownerId',
    'ownerDepartmentId',
    'clientIp',
    'region',
    'operatingSystem',
    'browser',
    'deviceType',
    'platform',
    'language',
    'timeZone',
    'screenResolution',
    'viewportSize',
    'colorDepth',
    'pixelRatio',
    'touchPoints',
    'userAgent',
    'clientHints',
    'createdAt',
] as const
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({
        field: field.field,
        label: t(field.label),
        type: field.valueType,
    })),
)

const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    replaceSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    getData,
} = useTable({
    core: {
        apiFn: fetchGetLoginLogList,
        apiParams: { current: 1, size: 20 },
        columnsFactory: () => [
            {
                prop: 'id',
                queryField: 'Id',
                queryValueType: 'number',
                label: 'ID',
                width: 150,
                formatter: (row: LoginLogRecord) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'userName',
                queryField: 'UserName',
                label: t('loginLog.fields.userName'),
                width: 130,
            },
            {
                prop: 'clientIp',
                queryField: 'ClientIp',
                label: t('loginLog.fields.clientIp'),
                width: 150,
            },
            {
                prop: 'region',
                queryField: 'Region',
                label: t('loginLog.fields.region'),
                minWidth: 180,
                showOverflowTooltip: true,
            },
            {
                prop: 'operatingSystem',
                queryField: 'OperatingSystem',
                label: t('loginLog.fields.operatingSystem'),
                width: 150,
            },
            { prop: 'browser', queryField: 'Browser', label: t('loginLog.fields.browser'), width: 150 },
            {
                prop: 'deviceType',
                queryField: 'DeviceType',
                label: t('loginLog.fields.deviceType'),
                width: 110,
            },
            {
                prop: 'language',
                queryField: 'Language',
                label: t('loginLog.fields.language'),
                width: 110,
            },
            {
                prop: 'timeZone',
                queryField: 'TimeZone',
                label: t('loginLog.fields.timeZone'),
                width: 170,
            },
            {
                prop: 'operation',
                label: t('loginLog.fields.actions'),
                width: 90,
                fixed: 'right',
                formatter: (row: LoginLogRecord) =>
                    h(ArtButtonTable, {
                        type: 'view',
                        title: t('loginLog.detail.view'),
                        onClick: () => openDetail(row),
                    }),
            },
        ],
    },
})

const handleSearch = (params: Record<string, unknown>) => {
    searchForm.value = params
    replaceSearchParams(params)
    void getData()
}
const handleReset = () => {
    searchForm.value = {}
    replaceSearchParams({})
    void getData()
}
const applyCellQuery = async (condition: DynamicFilter) => {
    const current = searchForm.value.dynamicFilter
    searchForm.value.dynamicFilter = current ? { logic: 'And', filters: [current, condition] } : condition
    replaceSearchParams(searchForm.value)
    await getData()
}
const openDetail = (row: LoginLogRecord) => {
    selectedLog.value = row
    activeDetailTab.value = 'details'
    detailVisible.value = true
}
const formatValue = (value: unknown) =>
    value === undefined || value === null || value === '' ? '-' : typeof value === 'string' ? value : JSON.stringify(value)

onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('login-log')
})
</script>

<style scoped>
.detail-value {
    white-space: pre-wrap;
    word-break: break-word;
}
</style>
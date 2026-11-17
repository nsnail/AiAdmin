<template>
    <div class="login-log-page art-full-height">
        <ArtTablePage
            v-model:column-checks="columnChecks"
            :columns="columns"
            :data="data"
            :loading="loading"
            :pagination="pagination"
            :row-actions="rowActions"
            @filter-change="handleFilterChange"
            @page-change="handleCurrentChange"
            @refresh="refreshData"
            @reset="resetSearchParams"
            @size-change="handleSizeChange"
            @sort-change="handleSortChange"
            resource="login-log" />
        <LoginLogDetailDrawer v-model:visible="detailVisible" :log="selectedLog" />
    </div>
</template>

<script lang="ts" setup>
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchGetLoginLogList, type LoginLogRecord } from '@/api/system-manage'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useTable } from '@/hooks/core/useTable'
import type { RowAction } from '@/types/component'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import LoginLogDetailDrawer from './modules/login-log-detail-drawer.vue'

defineOptions({ name: 'LoginLog' })
const { t } = useI18n()
const detailVisible = ref(false)
const selectedLog = ref<LoginLogRecord>()
const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    replaceSearchParams,
    resetSearchParams,
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
                width: 160,
                align: 'left',
                formatter: (row: LoginLogRecord) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'userName',
                queryField: 'UserName',
                queryValueField: 'userName',
                queryValueType: 'string',
                label: t('loginLog.fields.userName'),
                width: 130,
            },
            {
                prop: 'clientIp',
                queryField: 'ClientIp',
                queryValueField: 'clientIp',
                queryValueType: 'string',
                label: t('loginLog.fields.clientIp'),
                width: 150,
            },
            {
                prop: 'region',
                queryField: 'Region',
                queryValueField: 'region',
                queryValueType: 'string',
                label: t('loginLog.fields.region'),
                minWidth: 180,
                showOverflowTooltip: true,
            },
            {
                prop: 'operatingSystem',
                queryField: 'OperatingSystem',
                queryValueField: 'operatingSystem',
                queryValueType: 'string',
                label: t('loginLog.fields.operatingSystem'),
                width: 150,
            },
            {
                prop: 'browser',
                queryField: 'Browser',
                queryValueField: 'browser',
                queryValueType: 'string',
                label: t('loginLog.fields.browser'),
                width: 150,
            },
            {
                prop: 'deviceType',
                queryField: 'DeviceType',
                queryValueField: 'deviceType',
                queryValueType: 'string',
                label: t('loginLog.fields.deviceType'),
                width: 110,
            },
            {
                prop: 'language',
                queryField: 'Language',
                queryValueField: 'language',
                queryValueType: 'string',
                label: t('loginLog.fields.language'),
                width: 110,
            },
            {
                prop: 'timeZone',
                queryField: 'TimeZone',
                queryValueField: 'timeZone',
                queryValueType: 'string',
                label: t('loginLog.fields.timeZone'),
                width: 170,
            },
        ],
    },
})

const handleFilterChange = async (dynamicFilter: DynamicFilter | undefined): Promise<void> => {
    replaceSearchParams({ dynamicFilter })
    await getData()
}
/** 行右键菜单操作 */
const rowActions = (row: LoginLogRecord): RowAction[] => [
    { key: 'view', label: t('loginLog.detail.view'), icon: 'ri:eye-line', onClick: () => openDetail(row) },
]
const openDetail = (row: LoginLogRecord) => {
    selectedLog.value = row
    detailVisible.value = true
}
</script>
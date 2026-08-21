<template>
  <div class="system-log-page art-full-height">
    <ArtSearchBar v-model="searchForm" :items="searchItems" :advanced-query-fields="advancedQueryFields" @search="handleSearch" @reset="handleReset" />
    <ElCard class="art-table-card">
      <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData" />
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        :show-pagination-when-empty="true"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
        @sort-change="handleSortChange"
        @cell-query="applyCellQuery"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import { fetchGetSystemLogs, type SystemLogSearchParams } from '@/api/system-manage'
  import { useTable } from '@/hooks/core/useTable'
  import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'

  defineOptions({ name: 'SystemLog' })
  const { t } = useI18n()
  const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical']
  const searchForm = ref<SystemLogSearchParams>({ current: 1, size: 20 })
  const searchItems = computed(() => [
    { label: '', key: 'level', type: 'select', props: { placeholder: t('systemLog.filters.level'), options: levels.map(level => ({ label: level, value: level })) } },
    { label: '', key: 'category', type: 'input', props: { placeholder: t('systemLog.filters.category') } },
    { label: '', key: 'keyword', type: 'input', props: { placeholder: t('systemLog.filters.keyword') } }
  ])
  const advancedQueryFields = computed<DynamicQueryField[]>(() => [
    'timestamp', 'level', 'category', 'clientIp', 'elapsedMilliseconds', 'eventId', 'eventName', 'exception', 'logType', 'message', 'requestBody', 'requestContentType', 'requestHeaders', 'requestId', 'requestMethod', 'requestRelativeUrl', 'requestUrl', 'responseBody', 'responseContentType', 'responseHeaders', 'serverIp', 'source', 'sql', 'statusCode', 'threadId', 'userAgent', 'userName'
  ].map(field => ({ field, label: t(`systemLog.fields.${field}`), type: ['elapsedMilliseconds', 'eventId', 'statusCode', 'threadId'].includes(field) ? 'number' : field === 'timestamp' ? 'date' : 'string' })))

  const levelType = (level: string) => {
    if (level === 'Error' || level === 'Critical') return 'danger'
    if (level === 'Warning') return 'warning'
    if (level === 'Information') return 'success'
    return 'info'
  }
  const formatTime = (value: string) => value ? new Date(value).toLocaleString() : '-'

  const withQueryFields = (items: Array<Record<string, unknown>>) => items.map(item => ({ ...item, queryField: item.queryField ?? item.prop }))

  const {
    columns, columnChecks, data, loading, pagination, getData, replaceSearchParams, handleCellQuery: applyCellQuery,
    handleSizeChange, handleCurrentChange, handleSortChange, refreshData
  } = useTable({
    core: {
      apiFn: fetchGetSystemLogs,
      apiParams: searchForm.value,
      columnsFactory: () => withQueryFields([
        { prop: 'timestamp', label: t('systemLog.fields.timestamp'), width: 180, sortable: true, formatter: (row: Api.SystemManage.SystemLogItem) => formatTime(row.timestamp) },
        { prop: 'level', label: t('systemLog.fields.level'), width: 120, formatter: (row: Api.SystemManage.SystemLogItem) => h(ElTag, { type: levelType(row.level), size: 'small' }, () => row.level) },
        { prop: 'category', label: t('systemLog.fields.category'), minWidth: 260, showOverflowTooltip: true },
        { prop: 'clientIp', label: t('systemLog.fields.clientIp'), minWidth: 140, showOverflowTooltip: true },
        { prop: 'elapsedMilliseconds', label: t('systemLog.fields.elapsedMilliseconds'), width: 150 },
        { prop: 'message', label: t('systemLog.fields.message'), minWidth: 360, showOverflowTooltip: true },
        { prop: 'eventId', label: t('systemLog.fields.eventId'), width: 100, align: 'center' },
        { prop: 'eventName', label: t('systemLog.fields.eventName'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'exception', label: t('systemLog.fields.exception'), minWidth: 260, showOverflowTooltip: true, formatter: (row: Api.SystemManage.SystemLogItem) => row.exception || '-' },
        { prop: 'logType', label: t('systemLog.fields.logType'), width: 140 },
        { prop: 'requestBody', label: t('systemLog.fields.requestBody'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'requestContentType', label: t('systemLog.fields.requestContentType'), minWidth: 180, showOverflowTooltip: true },
        { prop: 'requestHeaders', label: t('systemLog.fields.requestHeaders'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'requestId', label: t('systemLog.fields.requestId'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'requestMethod', label: t('systemLog.fields.requestMethod'), width: 140 },
        { prop: 'requestRelativeUrl', label: t('systemLog.fields.requestRelativeUrl'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'requestUrl', label: t('systemLog.fields.requestUrl'), minWidth: 260, showOverflowTooltip: true },
        { prop: 'responseBody', label: t('systemLog.fields.responseBody'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'responseContentType', label: t('systemLog.fields.responseContentType'), minWidth: 180, showOverflowTooltip: true },
        { prop: 'responseHeaders', label: t('systemLog.fields.responseHeaders'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'serverIp', label: t('systemLog.fields.serverIp'), minWidth: 140, showOverflowTooltip: true },
        { prop: 'source', label: t('systemLog.fields.source'), minWidth: 180, showOverflowTooltip: true },
        { prop: 'sql', label: t('systemLog.fields.sql'), minWidth: 300, showOverflowTooltip: true },
        { prop: 'statusCode', label: t('systemLog.fields.statusCode'), width: 130 },
        { prop: 'threadId', label: t('systemLog.fields.threadId'), width: 130 },
        { prop: 'userAgent', label: t('systemLog.fields.userAgent'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'userName', label: t('systemLog.fields.userName'), minWidth: 140, showOverflowTooltip: true }
      ])
    }
  })

  async function handleSearch(params: SystemLogSearchParams) {
    replaceSearchParams({ ...params, current: 1 })
    await getData()
  }

  async function handleReset() {
    searchForm.value = { current: 1, size: pagination.size }
    replaceSearchParams(searchForm.value)
    await getData()
  }

</script>

<style scoped>
  .system-log-page :deep(.art-table-card .el-card__body) { display: flex; flex-direction: column; min-height: 0; }
  .system-log-page :deep(.art-table) { position: relative; flex: 1 1 auto; min-height: 0; padding-bottom: 56px; }
  .system-log-page :deep(.art-table .custom-pagination) { position: absolute; right: 0; bottom: 0; left: 0; z-index: 2; }
  .system-log-page :deep(.el-table th .cell) { white-space: nowrap; }
</style>

<template>
  <div class="system-log-page art-full-height">
    <ArtSearchBar
      v-model="searchForm"
      :items="searchItems"
      :advanced-query-fields="advancedQueryFields"
      @search="handleSearch"
      @reset="handleReset"
    />
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
        @cell-query="applySystemLogCellQuery"
      />
    </ElCard>
    <ElDialog
      v-model="detailVisible"
      :title="t('systemLog.detail.title')"
      width="900px"
      destroy-on-close
    >
      <ElTabs v-if="selectedLog" v-model="activeDetailTab" type="card">
        <ElTabPane
          v-for="group in visibleLogDetailGroups"
          :key="group.key"
          :name="group.key"
          :label="t(`systemLog.detail.tabs.${group.key}`)"
        >
          <ElDescriptions :column="1" :label-width="220" border>
            <ElDescriptionsItem
              v-for="field in group.fields"
              :key="field"
              :label="t(`systemLog.fields.${field}`)"
            >
              <pre class="log-detail-value">{{ formatDetailValue(field, selectedLog[field]) }}</pre>
            </ElDescriptionsItem>
          </ElDescriptions>
        </ElTabPane>
      </ElTabs>
      <template #footer>
        <ElButton @click="detailVisible = false">{{ t('systemLog.detail.close') }}</ElButton>
      </template>
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
  import { ElButton, ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import { fetchGetSystemLogs, type SystemLogSearchParams } from '@/api/system-manage'
  import { useTable } from '@/hooks/core/useTable'
  import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'

  defineOptions({ name: 'SystemLog' })
  const { t, locale } = useI18n()
  const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical']
  const searchForm = ref<SystemLogSearchParams>({ current: 1, size: 20 })
  const searchItems = computed(() => [
    {
      label: '',
      key: 'level',
      type: 'select',
      props: {
        placeholder: t('systemLog.filters.level'),
        options: levels.map((level) => ({ label: level, value: level }))
      }
    },
    {
      label: '',
      key: 'category',
      type: 'input',
      props: { placeholder: t('systemLog.filters.category') }
    },
    {
      label: '',
      key: 'keyword',
      type: 'input',
      props: { placeholder: t('systemLog.filters.keyword') }
    }
  ])
  const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    [
      'timestamp',
      'level',
      'category',
      'clientIp',
      'elapsedMilliseconds',
      'eventId',
      'eventName',
      'exception',
      'logType',
      'message',
      'requestBody',
      'requestContentType',
      'requestHeaders',
      'requestId',
      'requestMethod',
      'requestRelativeUrl',
      'requestUrl',
      'responseBody',
      'responseContentType',
      'responseHeaders',
      'serverIp',
      'source',
      'sql',
      'statusCode',
      'threadId',
      'userAgent',
      'userName'
    ].map((field) => ({
      field,
      label: t(`systemLog.fields.${field}`),
      type: ['elapsedMilliseconds', 'eventId', 'statusCode', 'threadId'].includes(field)
        ? 'number'
        : field === 'timestamp'
          ? 'date'
          : 'string'
    }))
  )

  const levelType = (level: string) => {
    if (level === 'Error' || level === 'Critical') return 'danger'
    if (level === 'Warning') return 'warning'
    if (level === 'Information') return 'success'
    return 'info'
  }
  const formatTime = (value: string) =>
    value
      ? new Date(value).toLocaleString(locale.value, {
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
          hour: '2-digit',
          minute: '2-digit',
          second: '2-digit',
          fractionalSecondDigits: 3,
          hour12: false
        })
      : '-'
  const detailVisible = ref(false)
  const selectedLog = ref<Api.SystemManage.SystemLogItem | null>(null)
  const activeDetailTab = ref('basic')
  const logDetailGroups: Array<{
    key: string
    fields: Array<keyof Api.SystemManage.SystemLogItem>
  }> = [
    {
      key: 'basic',
      fields: [
        'timestamp',
        'level',
        'category',
        'logType',
        'message',
        'source',
        'threadId',
        'eventId',
        'eventName',
        'exception'
      ]
    },
    {
      key: 'api',
      fields: [
        'requestMethod',
        'clientIp',
        'serverIp',
        'userAgent',
        'requestRelativeUrl',
        'elapsedMilliseconds',
        'statusCode',
        'requestId',
        'userName',
        'requestContentType',
        'requestHeaders',
        'requestBody',
        'responseHeaders',
        'responseBody',
        'responseContentType'
      ]
    },
    { key: 'sql', fields: ['sql', 'elapsedMilliseconds'] },
    {
      key: 'http',
      fields: [
        'requestMethod',
        'requestUrl',
        'elapsedMilliseconds',
        'statusCode',
        'requestContentType',
        'requestHeaders',
        'requestBody',
        'responseHeaders',
        'responseBody',
        'responseContentType'
      ]
    },
    {
      key: 'other',
      fields: [
        'clientIp',
        'serverIp',
        'userAgent',
        'requestRelativeUrl',
        'requestUrl',
        'elapsedMilliseconds',
        'statusCode',
        'requestId',
        'userName',
        'requestContentType',
        'requestHeaders',
        'requestBody',
        'responseHeaders',
        'responseBody',
        'responseContentType',
        'sql'
      ]
    }
  ]
  const visibleLogDetailGroups = computed(() => {
    const logType = selectedLog.value?.logType?.toLowerCase() || ''
    const category = logType.includes('api')
      ? 'api'
      : logType.includes('sql')
        ? 'sql'
        : logType.includes('http')
          ? 'http'
          : 'other'
    return logDetailGroups.filter((group) => group.key === 'basic' || group.key === category)
  })
  const formatDetailValue = (field: keyof Api.SystemManage.SystemLogItem, value: unknown) => {
    if (value === null || value === undefined || value === '') return '-'
    if (field === 'timestamp') return formatTime(String(value))
    return typeof value === 'string' ? value : JSON.stringify(value)
  }
  const openDetail = (row: Api.SystemManage.SystemLogItem) => {
    selectedLog.value = row
    activeDetailTab.value = 'basic'
    detailVisible.value = true
  }

  const textQueryOperators = ['Equal', 'NotEqual', 'Contains', 'StartsWith', 'EndsWith']
  const numericQueryOperators = [
    'Equal',
    'NotEqual',
    'GreaterThan',
    'GreaterThanOrEqual',
    'LessThan',
    'LessThanOrEqual'
  ]
  const withQueryFields = (items: Array<Record<string, unknown>>) =>
    items.map((item) => {
      const valueType =
        item.queryValueType ||
        (item.prop === 'timestamp'
          ? 'date'
          : ['elapsedMilliseconds', 'eventId', 'statusCode', 'threadId'].includes(String(item.prop))
            ? 'number'
            : 'string')
      return {
        ...item,
        queryField: item.queryField ?? item.prop,
        queryValueType: valueType,
        queryOperators:
          item.queryOperators ||
          (valueType === 'string' ? textQueryOperators : numericQueryOperators)
      }
    })

  const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    handleCellQuery: applyCellQuery,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData
  } = useTable({
    core: {
      apiFn: fetchGetSystemLogs,
      apiParams: searchForm.value,
      columnsFactory: () =>
        withQueryFields([
          {
            prop: 'timestamp',
            label: t('systemLog.fields.timestamp'),
            width: 180,
            sortable: true,
            formatter: (row: Api.SystemManage.SystemLogItem) => formatTime(row.timestamp)
          },
          {
            prop: 'level',
            label: t('systemLog.fields.level'),
            width: 120,
            formatter: (row: Api.SystemManage.SystemLogItem) =>
              h(ElTag, { type: levelType(row.level), size: 'small' }, () => row.level)
          },
          { prop: 'logType', label: t('systemLog.fields.logType'), width: 120, align: 'center' },
          {
            prop: 'message',
            label: t('systemLog.fields.message'),
            minWidth: 360,
            showOverflowTooltip: true
          },
          {
            prop: 'eventName',
            label: t('systemLog.fields.eventName'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'elapsedMilliseconds',
            label: t('systemLog.fields.elapsedMilliseconds'),
            width: 130,
            align: 'right',
            formatter: (row: Api.SystemManage.SystemLogItem) => `${row.elapsedMilliseconds} ms`
          },
          { prop: 'threadId', label: t('systemLog.fields.threadId'), width: 100, align: 'right' },
          {
            prop: 'category',
            label: t('systemLog.fields.category'),
            minWidth: 260,
            showOverflowTooltip: true
          },
          {
            prop: 'requestRelativeUrl',
            label: t('systemLog.fields.requestRelativeUrl'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'requestMethod',
            label: t('systemLog.fields.requestMethod'),
            width: 140,
            align: 'center'
          },
          {
            prop: 'requestId',
            label: t('systemLog.fields.requestId'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'requestContentType',
            label: t('systemLog.fields.requestContentType'),
            minWidth: 180,
            showOverflowTooltip: true
          },
          {
            prop: 'requestHeaders',
            label: t('systemLog.fields.requestHeaders'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'requestBody',
            label: t('systemLog.fields.requestBody'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'userAgent',
            label: t('systemLog.fields.userAgent'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'serverIp',
            label: t('systemLog.fields.serverIp'),
            minWidth: 140,
            showOverflowTooltip: true
          },
          {
            prop: 'clientIp',
            label: t('systemLog.fields.clientIp'),
            minWidth: 140,
            showOverflowTooltip: true
          },
          {
            prop: 'userName',
            label: t('systemLog.fields.userName'),
            minWidth: 140,
            showOverflowTooltip: true
          },
          {
            prop: 'requestUrl',
            label: t('systemLog.fields.requestUrl'),
            minWidth: 260,
            showOverflowTooltip: true
          },
          {
            prop: 'responseContentType',
            label: t('systemLog.fields.responseContentType'),
            minWidth: 180,
            showOverflowTooltip: true
          },
          {
            prop: 'responseHeaders',
            label: t('systemLog.fields.responseHeaders'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          {
            prop: 'responseBody',
            label: t('systemLog.fields.responseBody'),
            minWidth: 220,
            showOverflowTooltip: true
          },
          { prop: 'statusCode', label: t('systemLog.fields.statusCode'), width: 130 },
          {
            prop: 'sql',
            label: t('systemLog.fields.sql'),
            minWidth: 300,
            showOverflowTooltip: true
          },
          {
            prop: 'exception',
            label: t('systemLog.fields.exception'),
            minWidth: 260,
            showOverflowTooltip: true,
            formatter: (row: Api.SystemManage.SystemLogItem) => row.exception || '-'
          },
          {
            prop: 'operation',
            queryField: false,
            label: t('systemLog.detail.operation'),
            width: 110,
            fixed: 'right',
            formatter: (row: Api.SystemManage.SystemLogItem) =>
              h(ArtButtonTable, {
                type: 'view',
                title: t('systemLog.detail.view'),
                onClick: () => openDetail(row)
              })
          }
        ])
    }
  })

  const applySystemLogCellQuery = async (condition: {
    field: string
    operator: string
    value: unknown
  }) => {
    const currentFilter = searchForm.value.dynamicFilter
    searchForm.value.dynamicFilter = currentFilter
      ? { logic: 'And', filters: [currentFilter, condition] }
      : condition
    await applyCellQuery(condition)
  }

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
  .system-log-page :deep(.art-table-card .el-card__body) {
    display: flex;
    flex-direction: column;
    min-height: 0;
  }
  .system-log-page :deep(.art-table) {
    display: flex;
    flex: 1 1 auto;
    flex-direction: column;
    min-height: 0;
  }
  .system-log-page :deep(.art-table > .el-table) {
    height: auto !important;
    min-height: 0;
    flex: 1 1 auto;
  }
  .system-log-page :deep(.art-table .custom-pagination) {
    position: static;
    z-index: auto;
    flex: 0 0 auto;
    box-sizing: border-box;
    min-height: 56px;
    padding: 8px 0 0;
    margin-top: 0;
    background: var(--default-box-color);
  }
  .system-log-page :deep(.el-table th .cell) {
    white-space: nowrap;
  }
  .system-log-page :deep(.el-descriptions__label) {
    width: 220px;
    white-space: nowrap;
  }
  .log-detail-value {
    max-height: 240px;
    margin: 0;
    overflow: auto;
    white-space: pre-wrap;
    word-break: break-word;
    font: inherit;
  }

  @media (max-width: 640px) {
    .system-log-page :deep(.art-table .custom-pagination) {
      min-height: 108px;
      padding: 8px 0;
    }
    .system-log-page :deep(.el-descriptions__label) {
      width: 140px;
    }
  }
</style>
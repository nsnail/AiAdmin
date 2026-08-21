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
              <pre
                class="log-detail-value"
                @contextmenu.prevent="copyDetailValue(formatDetailValue(field, selectedLog[field]))"
              >{{ formatDetailValue(field, selectedLog[field]) }}</pre>
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
  import { ElButton, ElMessage, ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import { fetchGetSystemLogs, type SystemLogSearchParams } from '@/api/system-manage'
  import { useTable } from '@/hooks/core/useTable'
  import type {
    DynamicFilter,
    DynamicQueryField
  } from '@/components/core/forms/art-dynamic-query-drawer/types'

  type SystemLogSearchForm = SystemLogSearchParams & {
    timestamp?: string[]
    level?: string
    logType?: string
    keyword?: string
  }

  defineOptions({ name: 'SystemLog' })
  const { t, locale } = useI18n()
  const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical']
  const logTypes = ['System', 'Api', 'Sql', 'Http']
  const getTimestampShortcuts = () => {
    const now = new Date()
    const today = new Date(now)
    today.setHours(0, 0, 0, 0)
    const tomorrow = new Date(today)
    tomorrow.setDate(tomorrow.getDate() + 1)
    const currentHour = new Date(now)
    currentHour.setMinutes(0, 0, 0)
    const nextHour = new Date(currentHour)
    nextHour.setHours(nextHour.getHours() + 1)
    const yesterday = new Date(today)
    yesterday.setDate(yesterday.getDate() - 1)
    const weekStart = new Date(today)
    weekStart.setDate(weekStart.getDate() - weekStart.getDay())
    const nextWeek = new Date(weekStart)
    nextWeek.setDate(nextWeek.getDate() + 7)
    const monthStart = new Date(today.getFullYear(), today.getMonth(), 1)
    const nextMonth = new Date(today.getFullYear(), today.getMonth() + 1, 1)
    const range = (start: Date, end: Date) => [start, end]
    return [
      {
        text: t('table.searchBar.lastHour'),
        value: () => range(new Date(now.getTime() - 3600000), new Date())
      },
      {
        text: t('table.searchBar.currentHour'),
        value: () => range(new Date(currentHour), new Date(nextHour))
      },
      {
        text: t('table.searchBar.previousHour'),
        value: () => range(new Date(now.getTime() - 7200000), new Date(now.getTime() - 3600000))
      },
      {
        text: t('table.searchBar.yesterdayAtThisTime'),
        value: () =>
          range(new Date(yesterday.getTime() + (now.getTime() - today.getTime())), new Date())
      },
      { text: t('table.searchBar.today'), value: () => range(new Date(today), new Date(tomorrow)) },
      {
        text: t('table.searchBar.yesterday'),
        value: () => range(new Date(yesterday), new Date(today))
      },
      {
        text: t('table.searchBar.previousDay'),
        value: () => {
          const start = new Date(yesterday)
          start.setDate(start.getDate() - 1)
          return range(start, new Date(yesterday))
        }
      },
      {
        text: t('table.searchBar.thisWeek'),
        value: () => range(new Date(weekStart), new Date(nextWeek))
      },
      {
        text: t('table.searchBar.previousWeek'),
        value: () => {
          const start = new Date(weekStart)
          start.setDate(start.getDate() - 7)
          return range(start, new Date(weekStart))
        }
      },
      {
        text: t('table.searchBar.thisMonth'),
        value: () => range(new Date(monthStart), new Date(nextMonth))
      },
      {
        text: t('table.searchBar.previousMonth'),
        value: () => {
          const start = new Date(monthStart.getFullYear(), monthStart.getMonth() - 1, 1)
          return range(start, new Date(monthStart))
        }
      }
    ]
  }
  const toLocalIsoString = (date: Date): string => {
    const pad = (value: number, length = 2) => String(value).padStart(length, '0')
    const offsetMinutes = -date.getTimezoneOffset()
    const sign = offsetMinutes >= 0 ? '+' : '-'
    const absoluteOffset = Math.abs(offsetMinutes)
    const offset = `${sign}${pad(Math.floor(absoluteOffset / 60))}:${pad(absoluteOffset % 60)}`
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
      + `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
      + `.${pad(date.getMilliseconds(), 3)}${offset}`
  }

  const getTodayTimestampRange = (): string[] => {
    const start = new Date()
    start.setHours(0, 0, 0, 0)
    const end = new Date(start)
    end.setDate(end.getDate() + 1)
    return [toLocalIsoString(start), toLocalIsoString(end)]
  }
  const initialTimestamp = getTodayTimestampRange()
  const searchForm = ref<SystemLogSearchForm>({
    current: 1,
    size: 20,
    timestamp: initialTimestamp,
    dynamicFilter: {
      field: 'timestamp',
      operator: 'DateRange',
      value: initialTimestamp
    }
  })
  const searchItems = computed(() => [
    {
      label: '',
      key: 'timestamp',
      type: 'datetime',
      span: 6,
      props: {
        style: { width: '100%' },
        placeholder: t('systemLog.filters.timestamp'),
        type: 'datetimerange',
        rangeSeparator: t('table.searchBar.to'),
        valueFormat: 'YYYY-MM-DDTHH:mm:ss.SSSZ',
        startPlaceholder: t('systemLog.filters.startTime'),
        endPlaceholder: t('systemLog.filters.endTime'),
        clearable: true,
        shortcuts: getTimestampShortcuts()
      }
    },
    {
      label: '',
      key: 'level',
      type: 'select',
      span: 3,
      props: {
        placeholder: t('systemLog.filters.level'),
        options: levels.map((level) => ({ label: level, value: level }))
      }
    },
    {
      label: '',
      key: 'logType',
      type: 'select',
      span: 3,
      props: {
        placeholder: t('systemLog.filters.logType'),
        options: logTypes.map((type) => ({ label: t(`systemLog.types.${type}`), value: type }))
      }
    },
    {
      label: '',
      span: 6,
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
  const copyDetailValue = async (value: string) => {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(value)
      } else {
        const textarea = document.createElement('textarea')
        textarea.value = value
        textarea.style.position = 'fixed'
        textarea.style.opacity = '0'
        document.body.appendChild(textarea)
        textarea.select()
        document.execCommand('copy')
        textarea.remove()
      }
      ElMessage.success(t('systemLog.detail.copied'))
    } catch {
      ElMessage.error(t('systemLog.detail.copyFailed'))
    }
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
            align: 'center',
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
            formatter: (row: Api.SystemManage.SystemLogItem) =>
              row.elapsedMilliseconds ? `${row.elapsedMilliseconds} ms` : ''
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
            align: 'center',
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

  const buildDynamicFilter = (params: SystemLogSearchForm): DynamicFilter | undefined => {
    const directFields = new Set<string>()
    if (params.timestamp?.length === 2) directFields.add('timestamp')
    if (params.level) directFields.add('level')
    if (params.logType) directFields.add('logType')
    if (params.keyword?.trim()) directFields.add('message')
    const existingFilters = params.dynamicFilter
      ? params.dynamicFilter.filters?.length
        ? params.dynamicFilter.filters.filter((filter) => !directFields.has(filter.field || ''))
        : directFields.has(params.dynamicFilter.field || '')
          ? []
          : [params.dynamicFilter]
      : []
    const filters: DynamicFilter[] = [...existingFilters]
    if (params.timestamp?.length === 2) {
      filters.push({ field: 'timestamp', operator: 'DateRange', value: params.timestamp })
    }
    if (params.level) filters.push({ field: 'level', operator: 'Equal', value: params.level })
    if (params.logType) filters.push({ field: 'logType', operator: 'Equal', value: params.logType })
    if (params.keyword?.trim()) {
      filters.push({ field: 'message', operator: 'Contains', value: params.keyword.trim() })
    }
    if (!filters.length) return undefined
    return filters.length === 1 ? filters[0] : { logic: 'And', filters }
  }

  async function handleSearch(params: SystemLogSearchForm) {
    const nextParams = { ...params, current: 1, dynamicFilter: buildDynamicFilter(params) }
    searchForm.value.dynamicFilter = nextParams.dynamicFilter
    replaceSearchParams(nextParams)
    await getData()
  }

  async function handleReset() {
    const timestamp = getTodayTimestampRange()
    searchForm.value = {
      current: 1,
      size: pagination.size,
      timestamp,
      dynamicFilter: { field: 'timestamp', operator: 'DateRange', value: timestamp }
    }
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

<template>
  <div class="art-full-height">
    <ScheduledJobSearch
      v-show="showSearchBar"
      v-model="searchForm"
      @search="handleSearch"
      @reset="resetSearchParams"
    />
    <ElCard class="art-table-card" :style="{ 'margin-top': showSearchBar ? '12px' : '0' }">
      <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData">
        <template #left><ElButton v-ripple @click="openDialog()">新增作业</ElButton></template>
      </ArtTableHeader>
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
        @sort-change="handleSortChange"
        @cell-query="handleCellQuery"
      >
        <template #cronExpression="{ row }">
          <div class="cron-cell">
            <code>{{ row.cronExpression }}</code>
            <span class="cron-description">{{ describeCron(row.cronExpression) }}</span>
          </div>
        </template>
      </ArtTable>
    </ElCard>
    <ScheduledJobDialog
      v-model:visible="dialogVisible"
      :job-data="currentJob"
      :saving="saving"
      @submit="saveJob"
    />
    <ElDialog
      v-model="executionVisible"
      class="execution-dialog"
      :title="`${executionJob?.name || '作业'}执行记录`"
      fullscreen
      destroy-on-close
    >
      <div class="execution-page">
        <ArtSearchBar
          v-show="executionShowSearchBar"
          v-model="executionSearchForm"
          :items="executionSearchItems"
          :advanced-query-fields="executionAdvancedQueryFields"
          @search="handleExecutionSearch"
          @reset="resetExecutionSearch"
        />
        <ElCard
          class="art-table-card"
          :style="{ 'margin-top': executionShowSearchBar ? '12px' : '0' }"
        >
          <ArtTableHeader
            v-model:columns="executionColumnChecks"
            :loading="executionLoading"
            @refresh="executionRefreshData"
          />
          <ArtTable
            class="execution-table"
            :loading="executionLoading"
            :data="executionData"
            :columns="executionColumns"
            :pagination="executionPagination"
            @pagination:size-change="executionHandleSizeChange"
            @pagination:current-change="executionHandleCurrentChange"
            @sort-change="executionHandleSortChange"
            @cell-query="executionHandleCellQuery"
          />
        </ElCard>
      </div>
      <template #footer><ElButton @click="executionVisible = false">关闭</ElButton></template>
    </ElDialog>
    <ScheduledJobExecutionDialog v-model:visible="detailVisible" :execution="selectedExecution" />
  </div>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox, ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import ArtButtonMore, {
    type ButtonMoreItem
  } from '@/components/core/forms/art-button-more/index.vue'
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
  import { useTable } from '@/hooks/core/useTable'
  import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
  import {
    fetchCreateScheduledJob,
    fetchDeleteScheduledJob,
    fetchGetScheduledJobs,
    fetchRunScheduledJob,
    fetchScheduledJobExecutions,
    fetchUpdateScheduledJob,
    type SaveScheduledJob,
    type ScheduledJob,
    type ScheduledJobExecution,
    type ScheduledJobExecutionSearchParams,
    type ScheduledJobSearchParams
  } from '@/api/system-manage'
  import ScheduledJobDialog from './modules/scheduled-job-dialog.vue'
  import ScheduledJobExecutionDialog from './modules/scheduled-job-execution-dialog.vue'
  import ScheduledJobSearch from './modules/scheduled-job-search.vue'

  defineOptions({ name: 'ScheduledJobManagement' })
  const { t } = useI18n()
  const showSearchBar = ref(true)
  const dialogVisible = ref(false)
  const saving = ref(false)
  const currentJob = ref<ScheduledJob>()
  const executionJob = ref<ScheduledJob>()
  const executionVisible = ref(false)
  const executionSearchForm = ref<ScheduledJobExecutionSearchParams>({})
  const executionShowSearchBar = ref(true)
  const selectedExecution = ref<ScheduledJobExecution>()
  const detailVisible = ref(false)
  const searchForm = ref<ScheduledJobSearchParams>({})
  const statusMap: Record<
    number,
    { label: string; type: 'info' | 'primary' | 'success' | 'danger' | 'warning' }
  > = {
    0: { label: '等待执行', type: 'info' },
    1: { label: '执行中', type: 'primary' },
    2: { label: '执行成功', type: 'success' },
    3: { label: '执行失败', type: 'danger' },
    4: { label: '超时', type: 'warning' }
  }
  const formatTime = (value: string | null): string =>
    value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-'
  const describeCron = (value?: string): string => {
    if (!value?.trim()) return '-'
    const parts = value.trim().split(/\s+/)
    if (parts.length === 5) parts.unshift('0')
    if (parts.length !== 6 || parts.some((part) => !/^[\d*/,-]+$/.test(part)))
      return '等待完整的 Cron 表达式'
    const [second, minute, hour, day, month, week] = parts
    if (
      second === '*' &&
      minute === '*' &&
      hour === '*' &&
      day === '*' &&
      month === '*' &&
      week === '*'
    )
      return t('cronEditor.description.everySecond')
    if (
      second === '0' &&
      minute === '*' &&
      hour === '*' &&
      day === '*' &&
      month === '*' &&
      week === '*'
    )
      return t('cronEditor.description.everyMinute')
    if (
      second === '0' &&
      minute === '0' &&
      hour === '*' &&
      day === '*' &&
      month === '*' &&
      week === '*'
    )
      return t('cronEditor.description.hourly')
    const secondMatch = second.match(/^(?:\*|\d+)\/(\d+)$/)
    if (
      secondMatch &&
      minute === '*' &&
      hour === '*' &&
      day === '*' &&
      month === '*' &&
      week === '*'
    )
      return t('cronEditor.description.everySeconds', { value: secondMatch[1] })
    const minuteMatch = minute.match(/^(?:\*|\d+)\/(\d+)$/)
    if (
      second === '0' &&
      minuteMatch &&
      hour === '*' &&
      day === '*' &&
      month === '*' &&
      week === '*'
    )
      return t('cronEditor.description.everyMinutes', { value: minuteMatch[1] })
    if (
      /^\d+$/.test(hour) &&
      /^\d+$/.test(minute) &&
      /^\d+$/.test(second) &&
      day === '*' &&
      month === '*' &&
      week === '*'
    ) {
      return t('cronEditor.description.daily', {
        time: [hour, minute, second].map((item) => item.padStart(2, '0')).join(':')
      })
    }
    return t('cronEditor.description.custom', { value: value.trim() })
  }

  const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    handleCellQuery,
    refreshData,
    refreshCreate,
    refreshUpdate,
    refreshRemove
  } = useTable({
    core: {
      apiFn: fetchGetScheduledJobs,
      apiParams: { current: 1, size: 20 },
      columnsFactory: () => [
        {
          prop: 'id',
          queryField: 'Id',
          queryValueType: 'number',
          label: 'ID',
          width: 150,
          sortable: true,
          formatter: (row) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt })
        },
        { prop: 'name', queryField: 'Name', label: '名称', minWidth: 150, sortable: true },
        {
          prop: 'cronExpression',
          queryField: 'CronExpression',
          label: 'Cron 表达式',
          minWidth: 180,
          sortable: true,
          useSlot: true
        },
        {
          prop: 'requestMethod',
          queryField: 'RequestMethod',
          label: '请求方法',
          width: 110,
          sortable: true,
          align: 'center',
          formatter: (row) => h(ElTag, { size: 'small', type: 'info' }, () => row.requestMethod)
        },
        {
          prop: 'requestUrl',
          queryField: 'RequestUrl',
          label: '请求地址',
          minWidth: 260,
          sortable: true,
          showOverflowTooltip: true
        },
        {
          prop: 'timeoutSeconds',
          queryField: 'TimeoutSeconds',
          queryValueType: 'number',
          label: '超时（秒）',
          width: 120,
          sortable: true,
          align: 'center'
        },
        {
          prop: 'isEnabled',
          queryField: 'IsEnabled',
          queryValueType: 'boolean',
          label: '是否启用',
          width: 110,
          sortable: true,
          align: 'center',
          formatter: (row) =>
            h(ElTag, { size: 'small', type: row.isEnabled ? 'success' : 'info' }, () =>
              row.isEnabled ? '启用' : '禁用'
            )
        },
        {
          prop: 'status',
          queryField: 'Status',
          queryValueType: 'number',
          label: '执行状态',
          width: 120,
          sortable: true,
          align: 'center',
          formatter: (row) => {
            const status = statusMap[row.status] || { label: '未知', type: 'info' as const }
            return h(ElTag, { size: 'small', type: status.type }, () => status.label)
          }
        },
        {
          prop: 'lastTriggeredAt',
          queryField: 'LastTriggeredAt',
          queryValueType: 'date',
          label: '最近触发时间',
          width: 180,
          sortable: true,
          formatter: (row) => formatTime(row.lastTriggeredAt)
        },
        {
          prop: 'lastFinishedAt',
          queryField: 'LastFinishedAt',
          queryValueType: 'date',
          label: '最近完成时间',
          width: 180,
          sortable: true,
          formatter: (row) => formatTime(row.lastFinishedAt)
        },
        {
          prop: 'lastError',
          queryField: 'LastError',
          label: '最近错误',
          minWidth: 180,
          sortable: true,
          showOverflowTooltip: true,
          formatter: (row) => row.lastError || '-'
        },
        {
          prop: 'createdAt',
          queryField: 'CreatedAt',
          queryValueType: 'date',
          label: '创建时间',
          width: 180,
          sortable: true,
          formatter: (row) => formatTime(row.createdAt)
        },
        {
          prop: 'operation',
          queryField: false,
          label: '操作',
          width: 70,
          fixed: 'right',
          formatter: (row) =>
            h(ArtButtonMore, {
              list: [
                {
                  key: 'run',
                  label: '立即执行',
                  icon: 'ri:play-circle-line',
                  disabled: row.status === 1
                },
                { key: 'executions', label: '执行记录', icon: 'ri:history-line' },
                { key: 'edit', label: '编辑作业', icon: 'ri:edit-2-line' },
                { key: 'delete', label: '删除作业', icon: 'ri:delete-bin-4-line', color: '#f56c6c' }
              ],
              onClick: (item: ButtonMoreItem) => handleAction(item, row)
            })
        }
      ]
    }
  })

  const executionSearchItems = [
    { label: '请求地址', key: 'RequestUrl', type: 'input', props: { clearable: true } },
    {
      label: '请求方法',
      key: 'RequestMethod',
      type: 'select',
      props: {
        clearable: true,
        options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((value) => ({ label: value, value }))
      }
    },
    {
      label: '执行状态',
      key: 'Status',
      type: 'select',
      props: {
        clearable: true,
        options: Object.entries(statusMap).map(([value, item]) => ({
          label: item.label,
          value: Number(value)
        }))
      }
    },
    { label: '错误信息', key: 'ErrorMessage', type: 'input', props: { clearable: true } }
  ]
  const executionAdvancedQueryFields: DynamicQueryField[] = [
    { field: 'StartedAt', label: '开始时间', type: 'date' },
    { field: 'FinishedAt', label: '完成时间', type: 'date' },
    { field: 'RequestMethod', label: '请求方法', type: 'string' },
    { field: 'RequestUrl', label: '请求地址', type: 'string' },
    { field: 'ResponseStatusCode', label: '响应状态', type: 'number' },
    { field: 'Status', label: '执行状态', type: 'number' },
    { field: 'ErrorMessage', label: '错误信息', type: 'string' }
  ]
  const executionTable = useTable({
    core: {
      apiFn: (params: ScheduledJobExecutionSearchParams) =>
        fetchScheduledJobExecutions(executionJob.value?.id || '0', params),
      apiParams: { current: 1, size: 20 },
      immediate: false,
      columnsFactory: () => [
        {
          prop: 'startedAt',
          queryField: 'StartedAt',
          label: '开始时间',
          width: 190,
          sortable: true,
          formatter: (row) => formatTime(row.startedAt)
        },
        {
          prop: 'finishedAt',
          queryField: 'FinishedAt',
          label: '完成时间',
          width: 190,
          sortable: true,
          formatter: (row) => formatTime(row.finishedAt)
        },
        {
          prop: 'requestMethod',
          queryField: 'RequestMethod',
          label: '方法',
          width: 90,
          sortable: true
        },
        {
          prop: 'requestUrl',
          queryField: 'RequestUrl',
          label: '请求地址',
          minWidth: 240,
          sortable: true,
          showOverflowTooltip: true
        },
        {
          prop: 'responseStatusCode',
          queryField: 'ResponseStatusCode',
          queryValueType: 'number',
          label: '响应状态',
          width: 130,
          sortable: true,
          formatter: (row) => row.responseStatusCode ?? '-'
        },
        {
          prop: 'status',
          queryField: 'Status',
          queryValueType: 'number',
          label: '执行状态',
          width: 130,
          sortable: true,
          formatter: (row) =>
            h(
              ElTag,
              { size: 'small', type: statusMap[row.status]?.type || 'info' },
              () => statusMap[row.status]?.label || '未知'
            )
        },
        {
          prop: 'errorMessage',
          queryField: 'ErrorMessage',
          label: '错误信息',
          minWidth: 180,
          sortable: true,
          showOverflowTooltip: true,
          formatter: (row) => row.errorMessage || '-'
        },
        {
          prop: 'operation',
          queryField: false,
          label: '详情',
          width: 70,
          fixed: 'right',
          formatter: (row) =>
            h(ArtButtonTable, {
              type: 'view',
              title: t('scheduledJob.executionDetail.title'),
              onClick: () => showExecutionDetail(row)
            })
        }
      ]
    }
  })
  const {
    columns: executionColumns,
    columnChecks: executionColumnChecks,
    data: executionData,
    loading: executionLoading,
    pagination: executionPagination,
    getData: executionGetData,
    replaceSearchParams: executionReplaceSearchParams,
    resetSearchParams: executionResetSearchParams,
    handleSizeChange: executionHandleSizeChange,
    handleCurrentChange: executionHandleCurrentChange,
    handleSortChange: executionHandleSortChange,
    handleCellQuery: executionHandleCellQuery,
    refreshData: executionRefreshData
  } = executionTable

  const handleSearch = (params: ScheduledJobSearchParams): void => {
    replaceSearchParams(params)
    void getData()
  }
  const openDialog = (job?: ScheduledJob): void => {
    currentJob.value = job
    dialogVisible.value = true
  }
  const saveJob = async (form: SaveScheduledJob): Promise<void> => {
    if (saving.value) return
    saving.value = true
    try {
      if (currentJob.value) {
        await fetchUpdateScheduledJob(currentJob.value.id, form)
        await refreshUpdate()
      } else {
        await fetchCreateScheduledJob(form)
        await refreshCreate()
      }
      dialogVisible.value = false
      ElMessage.success('作业已保存')
    } finally {
      saving.value = false
    }
  }
  const handleExecutionSearch = (params: ScheduledJobExecutionSearchParams): void => {
    executionReplaceSearchParams(params)
    void executionGetData()
  }
  const resetExecutionSearch = (): void => {
    executionResetSearchParams()
    executionSearchForm.value = {}
    void executionGetData()
  }
  const handleAction = async (item: ButtonMoreItem, job: ScheduledJob): Promise<void> => {
    if (item.key === 'edit') {
      openDialog(job)
      return
    }
    if (item.key === 'run') {
      await fetchRunScheduledJob(job.id)
      ElMessage.success('作业已加入执行队列')
      await refreshUpdate()
      return
    }
    if (item.key === 'executions') {
      executionJob.value = job
      executionReplaceSearchParams({ current: 1, size: 20 })
      executionSearchForm.value = {}
      executionVisible.value = true
      await executionGetData()
      return
    }
    await ElMessageBox.confirm(`确定删除作业“${job.name}”吗？`, '删除确认', { type: 'warning' })
    await fetchDeleteScheduledJob(job.id)
    await refreshRemove()
  }
  const showExecutionDetail = (execution: ScheduledJobExecution): void => {
    selectedExecution.value = execution
    detailVisible.value = true
  }
</script>

<style scoped>
  .cron-cell {
    display: flex;
    flex-direction: column;
    gap: 3px;
    line-height: 1.35;
  }
  .cron-description {
    color: var(--el-text-color-secondary);
    font-size: 12px;
  }
  .execution-page {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
  }
  .execution-page :deep(.art-table-card .el-card__body) {
    display: flex;
    flex-direction: column;
    min-height: 0;
  }
  .execution-table :deep(.el-table th .cell) {
    white-space: nowrap;
  }
  .execution-pagination {
    display: flex;
    justify-content: flex-end;
    margin-top: 16px;
  }
  :global(.execution-dialog.el-dialog) {
    display: flex;
    flex-direction: column;
  }
  :global(.execution-dialog.el-dialog .el-dialog__body) {
    flex: 1;
    min-height: 0;
    overflow: hidden;
  }
</style>

<template>
  <div class="system-log-page art-full-height">
    <ElCard class="art-table-card">
      <div class="search-row">
        <ElSelect v-model="searchForm.level" clearable :placeholder="t('systemLog.filters.level')" class="search-item">
          <ElOption v-for="level in levels" :key="level" :label="level" :value="level" />
        </ElSelect>
        <ElInput v-model="searchForm.category" clearable :placeholder="t('systemLog.filters.category')" class="search-item" />
        <ElInput v-model="searchForm.keyword" clearable :placeholder="t('systemLog.filters.keyword')" class="search-item keyword" @keyup.enter="handleSearch" />
        <ElButton type="primary" @click="handleSearch">{{ t('common.search') }}</ElButton>
        <ElButton @click="handleReset">{{ t('common.reset') }}</ElButton>
      </div>
      <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData" />
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
        @sort-change="handleSortChange"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { ElTag } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import { fetchGetSystemLogs, type SystemLogSearchParams } from '@/api/system-manage'
  import { useTable } from '@/hooks/core/useTable'

  defineOptions({ name: 'SystemLog' })
  const { t } = useI18n()
  const levels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical']
  const searchForm = ref<SystemLogSearchParams>({ current: 1, size: 20 })

  const levelType = (level: string) => {
    if (level === 'Error' || level === 'Critical') return 'danger'
    if (level === 'Warning') return 'warning'
    if (level === 'Information') return 'success'
    return 'info'
  }
  const formatTime = (value: string) => value ? new Date(value).toLocaleString() : '-'

  const {
    columns, columnChecks, data, loading, pagination, replaceSearchParams,
    handleSizeChange, handleCurrentChange, handleSortChange, refreshData
  } = useTable({
    core: {
      apiFn: fetchGetSystemLogs,
      apiParams: searchForm.value,
      columnsFactory: () => [
        { prop: 'timestamp', label: t('systemLog.fields.timestamp'), width: 180, sortable: true, formatter: (row: Api.SystemManage.SystemLogItem) => formatTime(row.timestamp) },
        { prop: 'level', label: t('systemLog.fields.level'), width: 120, formatter: (row: Api.SystemManage.SystemLogItem) => h(ElTag, { type: levelType(row.level), size: 'small' }, () => row.level) },
        { prop: 'category', label: t('systemLog.fields.category'), minWidth: 260, showOverflowTooltip: true },
        { prop: 'message', label: t('systemLog.fields.message'), minWidth: 360, showOverflowTooltip: true },
        { prop: 'eventId', label: t('systemLog.fields.eventId'), width: 100, align: 'center' },
        { prop: 'eventName', label: t('systemLog.fields.eventName'), minWidth: 220, showOverflowTooltip: true },
        { prop: 'exception', label: t('systemLog.fields.exception'), minWidth: 260, showOverflowTooltip: true, formatter: (row: Api.SystemManage.SystemLogItem) => row.exception || '-' }
      ]
    }
  })

  const handleSearch = () => {
    replaceSearchParams({ ...searchForm.value, current: 1 })
  }
  const handleReset = () => {
    searchForm.value = { current: 1, size: pagination.size }
    replaceSearchParams(searchForm.value)
  }
</script>

<style scoped>
  .search-row { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 12px; }
  .search-item { width: 180px; }
  .search-item.keyword { width: 260px; }
</style>

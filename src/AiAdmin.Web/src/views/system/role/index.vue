<!-- 角色管理页面 -->
<template>
  <div class="art-full-height">
    <RoleSearch
      v-show="showSearchBar"
      v-model="searchForm"
      @search="handleSearch"
      @reset="resetSearchParams"
    ></RoleSearch>

    <ElCard class="art-table-card" :style="{ 'margin-top': showSearchBar ? '12px' : '0' }">
      <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData">
        <template #left>
          <ElSpace wrap>
            <ElButton @click="showDialog('add')" v-ripple>新增角色</ElButton>
          </ElSpace>
        </template>
      </ArtTableHeader>

      <!-- 表格 -->
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
        @sort-change="handleSortChange"
        @cell-query="applyCellQuery"
      >
      </ArtTable>
    </ElCard>

    <!-- 角色编辑弹窗 -->
    <RoleEditDialog
      v-model="dialogVisible"
      :dialog-type="dialogType"
      :role-data="currentRoleData"
      @success="refreshData"
    />

    <!-- 菜单权限弹窗 -->
    <RolePermissionDialog
      v-model="permissionDialog"
      :role-data="currentRoleData"
      @success="refreshData"
    />

    <RoleApiDialog v-model="apiPermissionDialog" :role-data="currentRoleData" />
  </div>
</template>

<script setup lang="ts">
  import { ButtonMoreItem } from '@/components/core/forms/art-button-more/index.vue'
  import { useTable } from '@/hooks/core/useTable'
  import { fetchDeleteRole, fetchGetRoleList } from '@/api/system-manage'
  import ArtEnabledSwitch from '@/components/core/forms/art-enabled-switch/index.vue'
  import ArtButtonMore from '@/components/core/forms/art-button-more/index.vue'
  import RoleSearch from './modules/role-search.vue'
  import RoleEditDialog from './modules/role-edit-dialog.vue'
  import RolePermissionDialog from './modules/role-permission-dialog.vue'
  import RoleApiDialog from './modules/role-api-dialog.vue'
  import { ElMessageBox } from 'element-plus'

  defineOptions({ name: 'Role' })
  type RoleListItem = Api.SystemManage.RoleListItem
  type RoleSearchFormParams = Api.SystemManage.RoleSearchParams & {
    daterange?: string[]
  }

  // 搜索表单
  const searchForm = ref<RoleSearchFormParams>({
    roleName: undefined,
    roleCode: undefined,
    description: undefined,
    enabled: undefined,
    daterange: undefined,
    IsEnabled: true
  } as RoleSearchFormParams)

  const showSearchBar = ref(true)

  const dialogVisible = ref(false)
  const permissionDialog = ref(false)
  const apiPermissionDialog = ref(false)
  const currentRoleData = ref<RoleListItem | undefined>(undefined)

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
    refreshData
  } = useTable({
    // 核心配置
    core: {
      apiFn: fetchGetRoleList,
      apiParams: {
        current: 1,
        size: 20,
        IsEnabled: true
      },
      // 排除 apiParams 中的属性
      columnsFactory: () => [
        {
          prop: 'roleName',
          queryField: 'Name',
          label: '角色名称',
          minWidth: 120
        },
        {
          prop: 'roleCode',
          queryField: 'Code',
          label: '角色编码',
          minWidth: 120
        },
        {
          prop: 'description',
          queryField: 'Description',
          label: '角色描述',
          minWidth: 150,
          showOverflowTooltip: true
        },
        {
          prop: 'dataScope',
          queryField: 'DataScope',
          label: '数据权限',
          minWidth: 150,
          formatter: (row) =>
            ({
              all: '全部数据',
              department: '本部门数据',
              department_and_children: '本部门和子部门数据',
              self: '本人数据'
            })[row.dataScope] || row.dataScope
        },
        {
          prop: 'enabled',
          queryField: 'IsEnabled',
          queryValueType: 'boolean',
          label: '是否启用',
          width: 120,
          formatter: (row) =>
            h(ArtEnabledSwitch, {
              id: row.roleId,
              resource: 'role',
              modelValue: row.enabled,
              'onUpdate:modelValue': () => {
                void getData()
              }
            })
        },
        {
          prop: 'operation',
          label: '操作',
          width: 100,
          fixed: 'right',
          formatter: (row) =>
            h('div', [
              h(ArtButtonMore, {
                list: [
                  {
                    key: 'permission',
                    label: '菜单权限',
                    icon: 'ri:user-3-line'
                  },
                  {
                    key: 'apiPermission',
                    label: '接口权限',
                    icon: 'ri:route-line'
                  },
                  {
                    key: 'edit',
                    label: '编辑角色',
                    icon: 'ri:edit-2-line'
                  },
                  {
                    key: 'delete',
                    label: '删除角色',
                    icon: 'ri:delete-bin-4-line',
                    color: '#f56c6c'
                  }
                ],
                onClick: (item: ButtonMoreItem) => buttonMoreClick(item, row)
              })
            ])
        }
      ]
    }
  })

  const dialogType = ref<'add' | 'edit'>('add')

  const showDialog = (type: 'add' | 'edit', row?: RoleListItem) => {
    dialogVisible.value = true
    dialogType.value = type
    currentRoleData.value = row
  }

  /**
   * 搜索处理
   * @param params 搜索参数
   */
  const handleSearch = (params: RoleSearchFormParams) => {
    // 处理日期区间参数，把 daterange 转换为 startTime 和 endTime
    const { daterange, ...filtersParams } = params
    const [startDate, endDate] = Array.isArray(daterange) ? daterange : [null, null]
    const startTime = startDate ? new Date(`${startDate}T00:00:00`).toISOString() : null
    const endTime = endDate
      ? new Date(new Date(`${endDate}T00:00:00`).getTime() + 24 * 60 * 60 * 1000).toISOString()
      : null

    replaceSearchParams({ ...filtersParams, startTime, endTime })
    getData()
  }

  const applyCellQuery = async (condition: {
    field: string
    operator: string
    value: unknown
  }): Promise<void> => {
    const currentFilter = searchForm.value.dynamicFilter
    searchForm.value = {
      ...searchForm.value,
      dynamicFilter: currentFilter
        ? { logic: 'And', filters: [currentFilter, condition] }
        : condition
    }
    replaceSearchParams(searchForm.value)
    await getData()
  }

  const buttonMoreClick = (item: ButtonMoreItem, row: RoleListItem) => {
    switch (item.key) {
      case 'permission':
        showPermissionDialog(row)
        break
      case 'apiPermission':
        apiPermissionDialog.value = true
        currentRoleData.value = row
        break
      case 'edit':
        showDialog('edit', row)
        break
      case 'delete':
        deleteRole(row)
        break
    }
  }

  const showPermissionDialog = (row?: RoleListItem) => {
    permissionDialog.value = true
    currentRoleData.value = row
  }

  const deleteRole = (row: RoleListItem) => {
    ElMessageBox.confirm(`确定删除角色"${row.roleName}"吗？此操作不可恢复！`, '删除确认', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
      .then(async () => {
        await fetchDeleteRole(row.roleId)
        refreshData()
      })
      .catch(() => {
        ElMessage.info('已取消删除')
      })
  }
</script>

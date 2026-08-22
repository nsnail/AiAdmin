<!-- 基础表格 -->
<template>
    <div class="user-page art-full-height">
        <ElCard class="art-table-card" style="margin-top: 0">
            <!-- 表格 -->
            <ArtTable
                :columns="columns"
                :data="data"
                :loading="loading"
                :pagination="pagination"
                :show-table-header="false"
                @pagination:current-change="handleCurrentChange"
                @pagination:size-change="handleSizeChange"
                rowKey="id">
            </ArtTable>
        </ElCard>
    </div>
</template>

<script lang="ts" setup>
import { useTable } from '@/hooks/core/useTable'
import { fetchGetUserList } from '@/api/system-manage'

defineOptions({ name: 'UserMixedUsageExample' })

const { data, columns, loading, pagination, handleSizeChange, handleCurrentChange } = useTable({
    core: {
        apiFn: fetchGetUserList,
        apiParams: {
            current: 1,
            size: 20,
            userName: '',
            userPhone: '',
            userEmail: '',
        },
        columnsFactory: () => [
            {
                prop: 'id',
                label: 'ID',
            },
            {
                prop: 'nickName',
                label: '昵称',
            },
            {
                prop: 'userGender',
                label: '性别',
                sortable: true,
                formatter: (row) => row.userGender || '未知',
            },
            {
                prop: 'userPhone',
                label: '手机号',
            },
            {
                prop: 'userEmail',
                label: '邮箱',
            },
        ],
    },
})
</script>
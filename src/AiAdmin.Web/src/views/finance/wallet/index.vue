<template>
    <ArtTablePage
        v-model:column-checks="columnChecks"
        :columns="columns"
        :data="data"
        :loading="loading"
        :pagination="pagination"
        @filter-change="handleFilterChange"
        @page-change="handleCurrentChange"
        @refresh="refreshData"
        @reset="handleReset"
        @size-change="handleSizeChange"
        @sort-change="handleSortChange"
        resource="wallet" />
</template>

<script lang="ts" setup>
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElTag } from 'element-plus'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import ArtTablePage from '@/components/core/tables/art-table-page/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchGetWalletList, type DynamicFilter, type WalletInfo } from '@/api/system-manage'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'MyWallet' })
const { t, locale } = useI18n()
const money = (value: number) => value.toLocaleString(locale.value, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const time = (value: string | null) => (value ? formatDateTime(value, locale.value) : '-')
const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    replaceSearchParams,
    getData,
    handleSizeChange,
    handleCurrentChange,
    handleSortChange,
    refreshData,
    resetColumns,
} = useTable({
    core: {
        apiFn: fetchGetWalletList,
        apiParams: { current: 1, size: 20 },
        columnsFactory: () => [
            {
                prop: 'id',
                queryField: 'UserId',
                queryValueType: 'number',
                label: 'ID',
                width: 170,
                align: 'left',
                sortable: true,
                formatter: (row: WalletInfo) => h(ArtListIdCell, { id: row.id, createdAt: row.createdAt }),
            },
            {
                prop: 'userInfo',
                queryField: 'UserId',
                label: t('wallet.user'),
                minWidth: 280,
                formatter: (row: WalletInfo) =>
                    h('div', { class: 'user flex-c' }, [
                        h(ArtUserAvatar, { class: 'size-9.5 rounded-full', src: row.userAvatar, name: row.userName }),
                        h('div', { class: 'ml-2' }, [
                            h('p', { class: 'user-name' }, row.userName),
                            h('p', { class: 'email text-gray-400' }, row.userEmail),
                        ]),
                    ]),
            },
            {
                prop: 'currency',
                label: t('wallet.currency'),
                width: 110,
                align: 'right',
                formatter: (row: WalletInfo) => h(ElTag, { type: 'success', size: 'small' }, () => row.currency),
            },
            {
                prop: 'availableBalance',
                queryField: 'AvailableBalance',
                queryValueType: 'number',
                label: t('wallet.availableBalance'),
                minWidth: 150,
                sortable: true,
                align: 'right',
                formatter: (row: WalletInfo) => money(row.availableBalance),
            },
            {
                prop: 'frozenBalance',
                queryField: 'FrozenBalance',
                queryValueType: 'number',
                label: t('wallet.frozenBalance'),
                minWidth: 140,
                sortable: true,
                align: 'right',
                formatter: (row: WalletInfo) => money(row.frozenBalance),
            },
            {
                prop: 'totalIncome',
                queryField: 'TotalIncome',
                queryValueType: 'number',
                label: t('wallet.totalIncome'),
                minWidth: 140,
                sortable: true,
                align: 'right',
                formatter: (row: WalletInfo) => money(row.totalIncome),
            },
            {
                prop: 'totalExpense',
                queryField: 'TotalExpense',
                queryValueType: 'number',
                label: t('wallet.totalExpense'),
                minWidth: 140,
                sortable: true,
                align: 'right',
                formatter: (row: WalletInfo) => money(row.totalExpense),
            },
            {
                prop: 'lastTransactionAt',
                queryField: 'LastTransactionAt',
                queryValueType: 'date',
                label: t('wallet.lastTransactionAt'),
                minWidth: 180,
                sortable: true,
                formatter: (row: WalletInfo) => time(row.lastTransactionAt),
            },
        ],
    },
})
watch(locale, () => resetColumns?.())
const handleFilterChange = (filter: DynamicFilter | undefined) => {
    replaceSearchParams({ dynamicFilter: filter })
    void getData()
}
const handleReset = () => {
    replaceSearchParams({})
    void getData()
}
</script>
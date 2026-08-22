<template>
    <div class="wallet-page art-full-height">
        <WalletSearch v-model="searchForm" @reset="handleReset" @search="handleSearch" />
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
    </div>
</template>

<script lang="ts" setup>
import { h } from 'vue'
import { useI18n } from 'vue-i18n'
import { ElImage, ElTag } from 'element-plus'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchGetWalletList, type DynamicFilter, type WalletInfo } from '@/api/system-manage'
import WalletSearch from './modules/wallet-search.vue'
import defaultAvatar from '@/assets/images/user/avatar.png'

defineOptions({ name: 'MyWallet' })
const { t, locale } = useI18n()
const searchForm = ref<Record<string, unknown> & { dynamicFilter?: DynamicFilter }>({})
const money = (value: number) => value.toLocaleString(locale.value, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const time = (value: string | null) => (value ? new Date(value).toLocaleString(locale.value) : '-')
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
                        h(ElImage, {
                            class: 'size-9.5 rounded-full',
                            src: row.userAvatar || defaultAvatar,
                            fallback: defaultAvatar,
                            previewSrcList: row.userAvatar ? [row.userAvatar] : [],
                            previewTeleported: true,
                        }),
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
const handleSearch = (params: Record<string, unknown> & { dynamicFilter?: DynamicFilter }) => {
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
</script>
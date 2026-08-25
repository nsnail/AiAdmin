<template>
    <div class="wallet-page art-full-height">
        <ArtFilterGroups
            :groups="filterGroups"
            :loading="groupLoading"
            :reserve-space="filterFields.some((field) => field.groupCount)"
            :selections="groupSelections"
            @select="handleGroupSelect" />
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
import { ElTag } from 'element-plus'
import ArtListIdCell from '@/components/core/forms/art-list-id-cell/index.vue'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import ArtFilterGroups from '@/components/core/forms/art-filter-groups/index.vue'
import { useTable } from '@/hooks/core/useTable'
import {
    fetchGetListFilterGroups,
    fetchGetListFilterFields,
    fetchGetWalletList,
    type DynamicFilter,
    type ListFilterField,
    type ListFilterGroup,
    type WalletInfo,
} from '@/api/system-manage'
import WalletSearch from './modules/wallet-search.vue'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'MyWallet' })
const { t, locale } = useI18n()
const searchForm = ref<Record<string, unknown> & { dynamicFilter?: DynamicFilter }>({})
const filterFields = ref<ListFilterField[]>([])
const filterGroups = ref<ListFilterGroup[]>([])
const groupLoading = ref(false)
const groupSelections = computed<Record<string, unknown>>(() => {
    const result: Record<string, unknown> = {}
    const filter = searchForm.value.dynamicFilter
    const collect = (item?: DynamicFilter): void => {
        if (!item) return
        if (item.field && item.operator === 'Equal') result[item.field] = item.value
        item.filters?.forEach(collect)
    }
    collect(filter)
    return result
})
const money = (value: number) => value.toLocaleString(locale.value, { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const time = (value: string | null) => (value ? formatDateTime(value, locale.value) : '-')
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
const handleSearch = (params: Record<string, unknown> & { dynamicFilter?: DynamicFilter }) => {
    searchForm.value = params
    replaceSearchParams(params)
    void getData()
    void loadFilterGroups()
}
const handleReset = () => {
    searchForm.value = {}
    replaceSearchParams({})
    void getData()
    void loadFilterGroups()
}
const applyCellQuery = async (condition: DynamicFilter) => {
    const current = searchForm.value.dynamicFilter
    searchForm.value.dynamicFilter = current ? { logic: 'And', filters: [current, condition] } : condition
    replaceSearchParams(searchForm.value)
    await getData()
    await loadFilterGroups()
}

const loadFilterGroups = async () => {
    if (!filterFields.value.some((field) => field.groupCount)) return
    groupLoading.value = true
    try {
        filterGroups.value = await fetchGetListFilterGroups('wallet', searchForm.value.dynamicFilter)
    } finally {
        groupLoading.value = false
    }
}

const handleGroupSelect = (field: string, value: unknown) => {
    const current = searchForm.value.dynamicFilter
    const condition = value === undefined ? undefined : { field, operator: 'Equal', value }
    searchForm.value.dynamicFilter = condition ? (current ? { logic: 'And', filters: [current, condition] } : condition) : undefined
    replaceSearchParams(searchForm.value)
    void getData()
    void loadFilterGroups()
}

onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('wallet')
    await loadFilterGroups()
})
</script>
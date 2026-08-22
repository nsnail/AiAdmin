<template>
    <ArtSearchBar
        v-model="searchForm"
        :advanced-query-fields="advancedQueryFields"
        :filter-fields="filterFields"
        @reset="handleReset"
        @search="handleSearch" />
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { fetchGetListFilterFields, type DynamicFilter, type ListFilterField } from '@/api/system-manage'
import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'

const props = defineProps<{ modelValue: Record<string, unknown> & { dynamicFilter?: DynamicFilter } }>()
const emit = defineEmits<{
    (e: 'update:modelValue', value: Record<string, unknown> & { dynamicFilter?: DynamicFilter }): void
    (e: 'search', value: Record<string, unknown> & { dynamicFilter?: DynamicFilter }): void
    (e: 'reset'): void
}>()
const { t } = useI18n()
const filterFields = ref<ListFilterField[]>([])
const searchForm = computed({ get: () => props.modelValue, set: (value) => emit('update:modelValue', value) })
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({ field: field.field, label: t(field.label), type: field.valueType })),
)
const handleSearch = (value: Record<string, unknown> & { dynamicFilter?: DynamicFilter }) => emit('search', value)
const handleReset = () => emit('reset')
onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('wallet')
})
</script>

<template>
    <ArtSearchBar
        v-model="formData"
        :advanced-query-fields="advancedQueryFields"
        :filter-fields="filterFields"
        :items="formItems"
        @reset="emit('reset')"
        @search="search"
        ref="searchBarRef" />
</template>

<script lang="ts" setup>
import { fetchGetListFilterFields, type ListFilterField, type ScheduledJobSearchParams } from '@/api/system-manage'
import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'

const props = defineProps<{ modelValue: ScheduledJobSearchParams }>()
const emit = defineEmits<{
    (event: 'update:modelValue', value: ScheduledJobSearchParams): void
    (event: 'search', value: ScheduledJobSearchParams): void
    (event: 'reset'): void
}>()
const { t } = useI18n()
const searchBarRef = ref()
const filterFields = ref<ListFilterField[]>([])
const formData = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value),
})
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({
        field: field.field,
        label: t(field.label),
        type: field.valueType,
    })),
)
const formItems = [
    { label: '名称', key: 'Name', type: 'input', props: { clearable: true } },
    { label: 'Cron 表达式', key: 'CronExpression', type: 'input', props: { clearable: true } },
    { label: '请求地址', key: 'RequestUrl', type: 'input', props: { clearable: true } },
    {
        label: '请求方法',
        key: 'RequestMethod',
        type: 'select',
        props: {
            clearable: true,
            options: ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((value) => ({ label: value, value })),
        },
    },
    {
        label: '是否启用',
        key: 'IsEnabled',
        type: 'select',
        props: {
            clearable: true,
            options: [
                { label: '启用', value: true },
                { label: '禁用', value: false },
            ],
        },
    },
]

const search = async (value: ScheduledJobSearchParams): Promise<void> => {
    await searchBarRef.value?.validate()
    emit('search', value)
}

onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('scheduled-job')
})
</script>
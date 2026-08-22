<template>
    <ArtSearchBar
        v-model="formData"
        :advanced-query-fields="advancedQueryFields"
        :button-left-limit="0"
        :filter-fields="filterFields"
        :items="items"
        @reset="emit('reset')"
        @search="handleSearch" />
</template>
<script lang="ts" setup>
import { fetchGetListFilterFields, type ListFilterField } from '@/api/system-manage'
import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'
const { t } = useI18n()
const props = defineProps<{ modelValue: Api.SystemManage.SystemMessageSearchParams }>()
const emit = defineEmits<{
    'update:modelValue': [value: Api.SystemManage.SystemMessageSearchParams]
    search: [value: Api.SystemManage.SystemMessageSearchParams]
    reset: []
}>()
const filterFields = ref<ListFilterField[]>([])
const formData = computed({ get: () => props.modelValue, set: (value) => emit('update:modelValue', value) })
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({ field: field.field, label: t(field.label), type: field.valueType })),
)
const items = computed(() => [{ label: t('messageManagement.title'), key: 'title', type: 'input', props: { clearable: true } }])
const handleSearch = (value: Api.SystemManage.SystemMessageSearchParams) => emit('search', value)
onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('message')
})
</script>
<template>
    <ArtSearchBar
        v-model="formData"
        :advanced-query-fields="advancedQueryFields"
        :filter-fields="filterFields"
        :items="formItems"
        :rules="rules"
        @reset="handleReset"
        @search="handleSearch"
        ref="searchBarRef">
    </ArtSearchBar>
</template>

<script lang="ts" setup>
import { fetchGetListFilterFields, type ListFilterField } from '@/api/system-manage'
import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { useI18n } from 'vue-i18n'
type RoleSearchFormParams = Api.SystemManage.RoleSearchParams & {
    daterange?: string[]
}

interface Props {
    modelValue: RoleSearchFormParams
}

interface Emits {
    (e: 'update:modelValue', value: RoleSearchFormParams): void
    (e: 'search', params: RoleSearchFormParams): void
    (e: 'reset'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const { t } = useI18n()

const searchBarRef = ref()
const filterFields = ref<ListFilterField[]>([])
const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({
        field: field.field,
        label: t(field.label),
        type: field.valueType,
    })),
)

/**
 * 表单数据双向绑定
 */
const formData = computed({
    get: () => props.modelValue,
    set: (val) => emit('update:modelValue', val),
})

/**
 * 表单校验规则
 */
const rules = {}

/**
 * 角色启用选项
 */
const statusOptions = ref([
    { label: '启用', value: true },
    { label: '禁用', value: false },
])

/**
 * 搜索表单配置项
 */
const formItems = computed(() => [
    {
        label: '角色名称',
        key: 'roleName',
        type: 'input',
        placeholder: '请输入角色名称',
        clearable: true,
    },
    {
        label: '角色编码',
        key: 'roleCode',
        type: 'input',
        placeholder: '请输入角色编码',
        clearable: true,
    },
    {
        label: '角色描述',
        key: 'description',
        type: 'input',
        placeholder: '请输入角色描述',
        clearable: true,
    },
    {
        label: '是否启用',
        key: 'enabled',
        type: 'select',
        props: {
            placeholder: '请选择状态',
            options: statusOptions.value,
            clearable: true,
        },
    },
    {
        label: '创建日期',
        key: 'daterange',
        type: 'datetime',
        props: {
            style: { width: '100%' },
            placeholder: '请选择日期范围',
            type: 'daterange',
            rangeSeparator: '至',
            startPlaceholder: '开始日期',
            endPlaceholder: '结束日期',
            valueFormat: 'YYYY-MM-DD',
            shortcuts: [
                { text: '今日', value: [new Date(), new Date()] },
                { text: '最近一周', value: [new Date(Date.now() - 604800000), new Date()] },
                { text: '最近一个月', value: [new Date(Date.now() - 2592000000), new Date()] },
            ],
        },
    },
])

/**
 * 处理重置事件
 */
const handleReset = () => {
    emit('reset')
}

/**
 * 处理搜索事件
 * 验证表单后触发搜索
 */
const handleSearch = async (params: RoleSearchFormParams) => {
    await searchBarRef.value.validate()
    emit('search', params)
}

onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('role')
})
</script>

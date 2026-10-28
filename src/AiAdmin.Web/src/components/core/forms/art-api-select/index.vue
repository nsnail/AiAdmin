<template>
    <ElSelect
        :loading="loading"
        :model-value="modelValue"
        :placeholder="t('apiRequestLog.placeholder.api')"
        :remote-method="searchApis"
        @update:model-value="emit('update:modelValue', $event)"
        clearable
        filterable
        remote
        remote-show-suffix>
        <ElOption v-for="api in apis" :key="api.pathCrc32" :label="labelOf(api)" :value="api.pathCrc32" />
    </ElSelect>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { fetchGetApiEndpointList, type DynamicFilter } from '@/api/system-manage'
import { findServerMessagesByTranslation, translateServerMessage } from '@/utils/i18n/server-message'

// 接口远程搜索下拉：输入关键字时由后端按路径或英文说明模糊匹配，中文说明在前端先反查出英文原文一并提交，选项值为路径 CRC32
defineOptions({ name: 'ArtApiSelect' })
defineProps<{ modelValue?: number }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: number | undefined): void }>()
const { t } = useI18n()
const loading = ref(false)
const apis = ref<Api.SystemManage.ApiEndpointItem[]>([])
/** 防止慢请求的结果覆盖后发起的搜索 */
let requestId = 0

/** 选项文字：接口说明（已翻译）加路径 */
const labelOf = (api: Api.SystemManage.ApiEndpointItem): string => `${translateServerMessage(api.name) ?? api.name}  ${api.path}`

/** 构造搜索条件：路径或英文说明包含关键字，或英文说明等于中文关键字反查出的原文，任一命中即可 */
const buildFilter = (keyword: string): DynamicFilter | undefined => {
    const value = keyword.trim()
    if (!value) return undefined
    const originals = findServerMessagesByTranslation(value)
    return {
        logic: 'Or',
        filters: [
            { field: 'Path', operator: 'Contains', value },
            { field: 'Name', operator: 'Contains', value },
            ...(originals.length > 0 ? [{ field: 'Name', operator: 'Any', value: originals }] : []),
        ],
    }
}

/** 搜索接口，同一路径的多个 HTTP 方法只保留一项，最多展示 50 项，需要更精确时继续输入关键字 */
const searchApis = async (keyword: string): Promise<void> => {
    const current = ++requestId
    loading.value = true
    try {
        const list = await fetchGetApiEndpointList(buildFilter(keyword), 'Path', 'asc')
        if (current !== requestId) return
        const seen = new Set<number>()
        apis.value = list.filter((api) => !seen.has(api.pathCrc32) && seen.add(api.pathCrc32)).slice(0, 50)
    } finally {
        if (current === requestId) loading.value = false
    }
}

onMounted(() => searchApis(''))
</script>
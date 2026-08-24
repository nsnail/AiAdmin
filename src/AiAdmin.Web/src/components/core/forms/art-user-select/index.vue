<template>
    <ElSelect
        :loading="loading"
        :model-value="modelValue"
        :placeholder="t('wallet.userPlaceholder')"
        :remote-method="searchUsers"
        @update:model-value="emit('update:modelValue', $event)"
        clearable
        filterable
        remote>
        <ElOption v-for="user in users" :key="user.id" :label="`${user.userName} (${user.userEmail})`" :value="Number(user.id)" />
    </ElSelect>
</template>
<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { fetchGetUserList, type DynamicFilter } from '@/api/system-manage'
defineProps<{ modelValue?: number }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: number | undefined): void }>()
const { t } = useI18n()
const loading = ref(false)
const users = ref<Api.SystemManage.UserListItem[]>([])
const searchUsers = async (keyword: string) => {
    loading.value = true
    try {
        const value = keyword.trim()
        const dynamicFilter: DynamicFilter | undefined = value ? { field: 'UserName', operator: 'Contains', value } : undefined
        users.value = (await fetchGetUserList({ current: 1, size: 20, dynamicFilter })).records
    } finally {
        loading.value = false
    }
}
onMounted(() => searchUsers(''))
</script>
<template>
    <ElPopconfirm
        v-if="modelValue"
        @confirm="updateEnabled(false)"
        cancel-button-text="取消"
        confirm-button-text="确定禁用"
        title="确定要禁用当前记录吗？"
        width="220">
        <template #reference>
            <ElSwitch :loading="loading" :model-value="modelValue" />
        </template>
    </ElPopconfirm>
    <ElSwitch v-else :loading="loading" :model-value="modelValue" @change="updateEnabled(true)" />
</template>

<script lang="ts" setup>
import { ElMessage } from 'element-plus'
import { fetchUpdateEnabledState, type EnabledStateResource } from '@/api/system-manage'

defineOptions({ name: 'ArtEnabledSwitch' })

const props = defineProps<{ id: string; modelValue: boolean; resource: EnabledStateResource }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()
const loading = ref(false)

const updateEnabled = async (isEnabled: boolean): Promise<void> => {
    loading.value = true
    try {
        await fetchUpdateEnabledState(props.resource, props.id, isEnabled)
        emit('update:modelValue', isEnabled)
    } catch {
        ElMessage.error('状态更新失败')
    } finally {
        loading.value = false
    }
}
</script>

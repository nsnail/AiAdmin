<template>
  <ElPopconfirm
    v-if="modelValue"
    title="确定要禁用当前记录吗？"
    confirm-button-text="确定禁用"
    cancel-button-text="取消"
    width="220"
    @confirm="updateEnabled(false)"
  >
    <template #reference>
      <ElSwitch :model-value="modelValue" :loading="loading" />
    </template>
  </ElPopconfirm>
  <ElSwitch v-else :model-value="modelValue" :loading="loading" @change="updateEnabled(true)" />
</template>

<script setup lang="ts">
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

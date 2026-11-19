<template>
    <AlertMonitorDialog v-model:visible="visible" :prefill="prefill" :saving="saving" @submit="save" />
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { ElMessage } from 'element-plus'
import { fetchCreateAlertMonitor, type SaveAlertMonitor } from '@/api/system-manage'
import AlertMonitorDialog from './alert-monitor-dialog.vue'

defineOptions({ name: 'AlertMonitorQuickAdd' })

const { t } = useI18n()
const visible = ref(false)
const saving = ref(false)
const prefill = ref<Partial<SaveAlertMonitor>>()

/**
 * 打开新增监控抽屉并预填字段，供字典页、缓存页的右键菜单调用
 * @param values 预填的字段，如来源、字典目录编码、目标和名称
 */
const open = (values: Partial<SaveAlertMonitor>): void => {
    prefill.value = values
    visible.value = true
}

/** 保存后关闭抽屉，并提示可到告警监控页查看和调整 */
const save = async (form: SaveAlertMonitor): Promise<void> => {
    saving.value = true
    try {
        await fetchCreateAlertMonitor(form)
        visible.value = false
        ElMessage.success(t('alertMonitor.quickAdd.created'))
    } finally {
        saving.value = false
    }
}

defineExpose({ open })
</script>
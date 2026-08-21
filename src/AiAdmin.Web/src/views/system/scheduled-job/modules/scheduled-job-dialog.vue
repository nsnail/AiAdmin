<template>
  <ElDialog
    v-model="dialogVisible"
    :title="jobData?.id ? '编辑作业' : '新增作业'"
    width="680px"
    align-center
    destroy-on-close
  >
    <ElForm ref="formRef" :model="formData" :rules="rules" label-width="110px">
      <ElFormItem label="名称" prop="name">
        <ElInput v-model.trim="formData.name" maxlength="100" show-word-limit />
      </ElFormItem>
      <ElFormItem label="Cron 表达式" prop="cronExpression">
        <ScCron v-model="formData.cronExpression" maxlength="100" placeholder="0 */5 * * * *" />
      </ElFormItem>
      <ElFormItem label="请求地址" prop="requestUrl">
        <ElInput v-model.trim="formData.requestUrl" maxlength="2000" />
      </ElFormItem>
      <ElFormItem label="请求方法" prop="requestMethod">
        <ElSegmented v-model="formData.requestMethod" :options="methods" />
      </ElFormItem>
      <ElFormItem label="请求头 JSON" prop="requestHeadersJson">
        <VAceEditor
          v-model:value="formData.requestHeadersJson"
          lang="json"
          theme="tomorrow"
          class="scheduled-job-editor"
          :options="editorOptions"
          @blur="formRef?.validateField('requestHeadersJson')"
        />
      </ElFormItem>
      <ElFormItem label="请求体" prop="requestBody">
        <VAceEditor
          v-model:value="formData.requestBody"
          lang="text"
          theme="tomorrow"
          class="scheduled-job-editor scheduled-job-body-editor"
          :options="editorOptions"
        />
      </ElFormItem>
      <ElFormItem label="超时（秒）" prop="timeoutSeconds">
        <ElInputNumber
          v-model="formData.timeoutSeconds"
          :min="1"
          :max="86400"
          controls-position="right"
        />
      </ElFormItem>
      <ElFormItem label="启用">
        <ElSwitch v-model="formData.isEnabled" active-text="启用" inactive-text="禁用" />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton :disabled="saving" @click="dialogVisible = false">取消</ElButton>
      <ElButton type="primary" :loading="saving" @click="submit">保存</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import type { FormInstance, FormRules } from 'element-plus'
  import { VAceEditor } from 'vue3-ace-editor'
  import 'ace-builds/src-noconflict/mode-json'
  import 'ace-builds/src-noconflict/mode-text'
  import 'ace-builds/src-noconflict/theme-tomorrow'
  import type { SaveScheduledJob, ScheduledJob } from '@/api/system-manage'
  import ScCron from '@/components/business/sc-cron/index.vue'

  const props = defineProps<{
    visible: boolean
    jobData?: ScheduledJob
    saving?: boolean
  }>()
  const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
    (event: 'submit', value: SaveScheduledJob): void
  }>()

  const methods = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE']
  const editorOptions = { useWorker: false, tabSize: 2, useSoftTabs: true, showPrintMargin: false }
  const formRef = ref<FormInstance>()
  const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })
  const defaults = (): SaveScheduledJob => ({
    name: '',
    cronExpression: '',
    requestUrl: '',
    requestMethod: 'GET',
    requestHeadersJson: '{}',
    requestBody: '',
    timeoutSeconds: 30,
    isEnabled: true
  })
  const formData = reactive<SaveScheduledJob>(defaults())

  const validateCron = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
    const fields = value.trim().split(/\s+/)
    callback(
      (fields.length === 5 || fields.length === 6) &&
        fields.every((field) => /^[\d*/,-]+$/.test(field))
        ? undefined
        : new Error('请输入有效的 5 段或 6 段 Cron 表达式')
    )
  }
  const validateUrl = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
    try {
      const url = new URL(value)
      callback(
        ['http:', 'https:'].includes(url.protocol)
          ? undefined
          : new Error('仅支持 HTTP 或 HTTPS 地址')
      )
    } catch {
      callback(new Error('请输入有效的请求地址'))
    }
  }
  const validateHeaders = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
    try {
      const parsed = JSON.parse(value || '{}')
      callback(
        parsed && !Array.isArray(parsed) && typeof parsed === 'object'
          ? undefined
          : new Error('请求头必须是 JSON 对象')
      )
    } catch {
      callback(new Error('请输入有效的 JSON'))
    }
  }
  const rules: FormRules<SaveScheduledJob> = {
    name: [
      { required: true, message: '请输入作业名称', trigger: 'blur' },
      { min: 1, max: 100, message: '名称长度不能超过 100 个字符', trigger: 'blur' }
    ],
    cronExpression: [
      { required: true, message: '请输入 Cron 表达式', trigger: 'blur' },
      { validator: validateCron, trigger: 'blur' }
    ],
    requestUrl: [
      { required: true, message: '请输入请求地址', trigger: 'blur' },
      { validator: validateUrl, trigger: 'blur' }
    ],
    requestMethod: [{ required: true, message: '请选择请求方法', trigger: 'change' }],
    requestHeadersJson: [{ validator: validateHeaders, trigger: 'blur' }],
    timeoutSeconds: [
      { required: true, message: '请输入超时时间', trigger: 'change' },
      {
        type: 'number',
        min: 1,
        max: 86400,
        message: '超时时间应为 1 至 86400 秒',
        trigger: 'change'
      }
    ]
  }

  watch(
    () => [props.visible, props.jobData] as const,
    ([visible, job]) => {
      if (!visible) return
      Object.assign(formData, defaults(), job || {})
      nextTick(() => formRef.value?.clearValidate())
    },
    { immediate: true }
  )

  const submit = async (): Promise<void> => {
    await formRef.value?.validate()
    emit('submit', { ...formData })
  }
</script>

<style scoped>
  .scheduled-job-editor {
    width: 100%;
    height: 160px;
  }
  .scheduled-job-body-editor {
    height: 220px;
  }
</style>

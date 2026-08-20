<template>
  <div class="art-full-height system-settings-page">
    <ElCard v-loading="loading" class="art-table-card">
      <template #header>
        <div class="settings-header">
          <span>{{ t('menus.system.settings') }}</span>
          <ElButton type="primary" :loading="saving" :disabled="!category" @click="saveSettings">
            <ArtSvgIcon icon="ri:save-3-line" class="mr-1" />{{ t('systemSettings.save') }}
          </ElButton>
        </div>
      </template>

      <ElForm v-if="category" label-width="220px" class="settings-form">
        <h3>{{ t('systemSettings.sections.registration') }}</h3>
        <ElFormItem v-for="field in registrationFields" :key="field.label" :label="t(field.title)">
          <ElSwitch v-model="values[field.label]" />
        </ElFormItem>

        <ElDivider />
        <h3>{{ t('systemSettings.sections.smtp') }}</h3>
        <ElFormItem :label="t('systemSettings.fields.smtpHost')">
          <ElInput v-model="values['SMTP Host']" maxlength="100" />
        </ElFormItem>
        <ElFormItem :label="t('systemSettings.fields.smtpPort')">
          <ElInputNumber v-model="smtpPort" :min="1" :max="65535" class="w-full" />
        </ElFormItem>
        <ElFormItem :label="t('systemSettings.fields.smtpSsl')">
          <ElSwitch v-model="values['SMTP SSL']" />
        </ElFormItem>
        <ElFormItem :label="t('systemSettings.fields.smtpUser')">
          <ElInput v-model="values['SMTP User']" maxlength="100" autocomplete="username" />
        </ElFormItem>
        <ElFormItem :label="t('systemSettings.fields.smtpPassword')">
          <ElInput
            v-model="values['SMTP Password']"
            type="password"
            show-password
            maxlength="100"
            autocomplete="new-password"
          />
        </ElFormItem>
        <ElFormItem :label="t('systemSettings.fields.smtpFrom')">
          <ElInput v-model="values['SMTP From']" maxlength="100" />
        </ElFormItem>
      </ElForm>
      <ElEmpty v-else :description="t('systemSettings.loadFailed')" />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import {
    fetchGetDictionaryCategories,
    fetchGetDictionaryItems,
    fetchUpdateDictionaryItem
  } from '@/api/system-manage'

  defineOptions({ name: 'SystemSettings' })

  type Category = Api.SystemManage.DictionaryCategory
  type Item = Api.SystemManage.DictionaryItem

  const { t } = useI18n()
  const category = ref<Category>()
  const items = ref<Item[]>([])
  const values = reactive<Record<string, string | boolean>>({})
  const loading = ref(false)
  const saving = ref(false)
  const registrationFields = [
    {
      label: 'Enable login slider verification',
      title: 'systemSettings.fields.enableLoginSliderVerification'
    },
    { label: 'Enable user registration', title: 'systemSettings.fields.enableUserRegistration' },
    { label: 'Enable email verification', title: 'systemSettings.fields.enableEmailVerification' }
  ]
  const smtpPort = computed({
    get: () => Number(values['SMTP Port'] || 25),
    set: (value: number | undefined) => {
      values['SMTP Port'] = String(value ?? 25)
    }
  })

  const flattenCategories = (categories: Category[]): Category[] =>
    categories.flatMap((item) => [item, ...flattenCategories(item.children)])
  const loadSettings = async () => {
    loading.value = true
    try {
      const categories = await fetchGetDictionaryCategories()
      category.value = flattenCategories(categories).find((item) => item.code === 'system_settings')
      if (!category.value) return
      items.value = await fetchGetDictionaryItems(category.value.id)
      items.value.forEach((item) => {
        values[item.label] =
          item.value === 'true' ? true : item.value === 'false' ? false : item.value
      })
    } finally {
      loading.value = false
    }
  }

  const saveSettings = async () => {
    if (saving.value) return
    saving.value = true
    try {
      await Promise.all(
        items.value.map((item) =>
          fetchUpdateDictionaryItem(item.id, {
            value:
              typeof values[item.label] === 'boolean'
                ? String(values[item.label])
                : String(values[item.label] ?? ''),
            label: item.label,
            sort: item.sort,
            isEnabled: item.isEnabled,
            remark: item.remark
          })
        )
      )
      ElMessage.success(t('systemSettings.saved'))
    } finally {
      saving.value = false
    }
  }

  onMounted(loadSettings)
</script>

<style scoped>
  .settings-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    font-weight: 600;
  }
  .settings-form {
    max-width: 760px;
    padding: 8px 12px;
  }
  .settings-form h3 {
    margin: 0 0 20px;
    font-size: 16px;
    font-weight: 600;
  }
  @media (max-width: 768px) {
    .settings-form :deep(.el-form-item__label) {
      float: none;
      display: block;
      text-align: left;
      width: auto !important;
    }
    .settings-form :deep(.el-form-item__content) {
      margin-left: 0 !important;
    }
  }
</style>

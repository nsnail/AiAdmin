<template>
  <ArtSearchBar
    ref="searchBarRef"
    v-model="formData"
    :items="formItems"
    :filter-fields="filterFields"
    :advanced-query-fields="advancedQueryFields"
    :rules="rules"
    @reset="handleReset"
    @search="handleSearch"
  >
  </ArtSearchBar>
</template>

<script setup lang="ts">
  import { useI18n } from 'vue-i18n'
  import { fetchGetListFilterFields, type ListFilterField } from '@/api/system-manage'
  import type { DynamicQueryField } from '@/components/core/forms/art-dynamic-query-drawer/types'

  const { t } = useI18n()
  interface Props {
    modelValue: Api.SystemManage.UserSearchParams
  }
  interface Emits {
    (e: 'update:modelValue', value: Api.SystemManage.UserSearchParams): void
    (e: 'search', params: Api.SystemManage.UserSearchParams): void
    (e: 'reset'): void
  }
  const props = defineProps<Props>()
  const emit = defineEmits<Emits>()

  // 表单数据双向绑定
  const searchBarRef = ref()
  const filterFields = ref<ListFilterField[]>([])
  const advancedQueryFields = computed<DynamicQueryField[]>(() =>
    filterFields.value.map((field) => ({
      field: field.field,
      label: t(field.label),
      type: field.valueType
    }))
  )
  const formData = computed({
    get: () => props.modelValue,
    set: (val) => emit('update:modelValue', val)
  })

  // 校验规则
  const rules = {
    // userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }]
  }

  const statusOptions = computed(() => [
    { label: t('userManagement.status.enabled'), value: '1' },
    { label: t('userManagement.status.disabled'), value: '2' }
  ])

  // 表单配置
  const formItems = computed(() => [
    {
      label: t('userManagement.fields.userName'),
      key: 'userName',
      type: 'input',
      placeholder: t('userManagement.placeholder.userName'),
      clearable: true
    },
    {
      label: t('userManagement.fields.phone'),
      key: 'userPhone',
      type: 'input',
      props: { placeholder: t('userManagement.placeholder.phone'), maxlength: '20' }
    },
    {
      label: t('userManagement.fields.email'),
      key: 'userEmail',
      type: 'input',
      props: { placeholder: t('userManagement.placeholder.email') }
    },
    {
      label: t('listFilter.common.status'),
      key: 'status',
      type: 'select',
      props: {
        placeholder: t('userManagement.placeholder.status'),
        options: statusOptions.value
      }
    },
    {
      label: t('userManagement.fields.gender'),
      key: 'userGender',
      type: 'radiogroup',
      props: {
        options: [
          { label: t('userManagement.gender.male'), value: 1 },
          { label: t('userManagement.gender.female'), value: 2 }
        ]
      }
    }
  ])

  // 事件
  function handleReset() {
    emit('reset')
  }

  async function handleSearch(params: Api.SystemManage.UserSearchParams) {
    await searchBarRef.value.validate()
    emit('search', params)
  }

  onMounted(async () => {
    filterFields.value = await fetchGetListFilterFields('user')
  })
</script>

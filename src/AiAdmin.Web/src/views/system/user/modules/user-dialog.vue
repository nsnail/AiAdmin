<template>
  <ElDialog
    v-model="dialogVisible"
    :title="t(dialogType === 'add' ? 'userManagement.dialog.addTitle' : 'userManagement.dialog.editTitle')"
    width="520px"
    align-center
  >
    <ElForm ref="formRef" :model="formData" :rules="rules" label-width="90px">
      <ElFormItem :label="t('userManagement.fields.userName')" prop="userName">
        <ElInput v-model.trim="formData.userName" :placeholder="t('userManagement.placeholder.userName')" />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.nickName')" prop="nickName">
        <ElInput v-model.trim="formData.nickName" :placeholder="t('userManagement.placeholder.nickName')" />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.password')" prop="password">
        <ElInput
          v-model="formData.password"
          type="password"
          show-password
          :placeholder="t(dialogType === 'edit' ? 'userManagement.dialog.passwordKeep' : 'userManagement.dialog.passwordNew')"
        />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.email')" prop="email">
        <ElInput v-model.trim="formData.email" :placeholder="t('userManagement.placeholder.email')" />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.phone')" prop="phone">
        <ElInput v-model.trim="formData.phone" :placeholder="t('userManagement.placeholder.phone')" />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.gender')" prop="gender">
        <ElSegmented v-model="formData.gender" :options="genderOptions" />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.roles')" prop="roles">
        <ElSelect v-model="formData.roles" multiple class="w-full">
          <ElOption v-for="role in roleList" :key="role.roleCode" :value="role.roleCode" :label="role.roleName" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.departments')" prop="departmentIds">
        <ElTreeSelect
          v-model="formData.departmentIds"
          :data="departmentList"
          node-key="id"
          :props="{ label: 'name', children: 'children' }"
          multiple
          check-strictly
          clearable
          class="w-full"
        />
      </ElFormItem>
      <ElFormItem :label="t('userManagement.fields.status')">
        <ElSwitch
          v-model="formData.isEnabled"
          :active-text="t('userManagement.status.enabled')"
          :inactive-text="t('userManagement.status.disabled')"
        />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="dialogVisible = false">{{ t('common.cancel') }}</ElButton>
      <ElButton type="primary" @click="handleSubmit">{{ t('userManagement.actions.save') }}</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchGetDepartmentTree, fetchGetUserRoles } from '@/api/system-manage'
  import type { FormInstance, FormRules } from 'element-plus'
  import { useI18n } from 'vue-i18n'

  const props = defineProps<{
    visible: boolean
    type: string
    userData?: Partial<Api.SystemManage.UserListItem>
  }>()
  const emit = defineEmits<{
    (e: 'update:visible', value: boolean): void
    (e: 'submit', value: Api.SystemManage.SaveUserParams): void
  }>()
  const { t } = useI18n()
  const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })
  const dialogType = computed(() => props.type)
  const formRef = ref<FormInstance>()
  const roleList = ref<Api.SystemManage.RoleListItem[]>([])
  const departmentList = ref<Api.SystemManage.DepartmentTreeItem[]>([])
  const genderOptions = computed(() => [
    { label: t('userManagement.gender.male'), value: 'male' },
    { label: t('userManagement.gender.female'), value: 'female' }
  ])
  const formData = reactive<Api.SystemManage.SaveUserParams>({
    userName: '', nickName: '', password: '', email: '', phone: '', gender: 'male', roles: [], departmentIds: [], isEnabled: true
  })
  const rules = computed<FormRules>(() => ({
    userName: [
      { required: true, message: t('userManagement.validation.userNameRequired'), trigger: 'blur' },
      { min: 2, max: 50, message: t('userManagement.validation.userNameLength'), trigger: 'blur' }
    ],
    password: [{
      validator: (_rule, value, callback) => {
        if (props.type === 'add' && !value) callback(new Error(t('userManagement.validation.passwordRequired')))
        else if (value && value.length < 6) callback(new Error(t('userManagement.validation.passwordLength')))
        else callback()
      },
      trigger: 'blur'
    }],
    email: [{ type: 'email', message: t('userManagement.validation.emailInvalid'), trigger: 'blur' }],
    roles: [{ required: true, type: 'array', min: 1, message: t('userManagement.validation.rolesRequired'), trigger: 'change' }]
  }))

  watch(() => props.visible, async (visible) => {
    if (!visible) return
    if (!roleList.value.length) roleList.value = await fetchGetUserRoles()
    if (!departmentList.value.length) departmentList.value = await fetchGetDepartmentTree()
    const row = props.userData
    Object.assign(formData, {
      userName: props.type === 'edit' ? row?.userName ?? '' : '',
      nickName: props.type === 'edit' ? row?.nickName ?? '' : '',
      password: '',
      email: props.type === 'edit' ? row?.userEmail ?? '' : '',
      phone: props.type === 'edit' ? row?.userPhone ?? '' : '',
      gender: props.type === 'edit' ? row?.userGender ?? 'male' : 'male',
      roles: props.type === 'edit' ? [...(row?.userRoles ?? [])] : ['R_USER'],
      departmentIds: props.type === 'edit' ? [...(row?.departmentIds ?? [])] : [],
      isEnabled: props.type === 'edit' ? row?.status === '1' : true
    })
    nextTick(() => formRef.value?.clearValidate())
  })

  const handleSubmit = async () => {
    if (formRef.value && await formRef.value.validate()) {
      emit('submit', { ...formData, roles: [...formData.roles], departmentIds: [...formData.departmentIds] })
    }
  }
</script>

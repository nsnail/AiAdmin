<template>
  <ElDialog
    v-model="dialogVisible"
    :title="
      t(dialogType === 'add' ? 'userManagement.dialog.addTitle' : 'userManagement.dialog.editTitle')
    "
    width="520px"
    align-center
  >
    <ElTabs v-model="activeTab">
      <ElTabPane :label="t('common.edit')" name="form">
        <ElForm ref="formRef" :model="formData" :rules="rules" label-width="90px">
          <ElFormItem label="头像">
            <div class="avatar-field">
              <ElAvatar
                :size="64"
                :src="avatarPreview || defaultAvatar"
                @error="handleAvatarError"
              />
              <ElUpload
                accept="image/*"
                :auto-upload="false"
                :show-file-list="false"
                :on-change="handleAvatarChange"
              >
                <ElButton><ArtSvgIcon icon="ri:image-add-line" />选择图片</ElButton>
              </ElUpload>
              <ElButton
                v-if="avatarFile || (dialogType === 'edit' && props.userData?.avatar)"
                link
                type="danger"
                @click="clearAvatar"
                >{{ avatarFile ? '移除' : '删除头像' }}</ElButton
              >
            </div>
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.userName')" prop="userName">
            <ElInput
              v-model.trim="formData.userName"
              :disabled="dialogType === 'edit'"
              :placeholder="t('userManagement.placeholder.userName')"
            />
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.password')" prop="password">
            <div class="password-field">
              <ElInput
                v-model="formData.password"
                type="password"
                show-password
                :placeholder="
                  t(
                    dialogType === 'edit'
                      ? 'userManagement.dialog.passwordKeep'
                      : 'userManagement.dialog.passwordNew'
                  )
                "
              />
              <div v-if="formData.password" class="password-strength">
                <div class="password-strength-bars" aria-hidden="true">
                  <span
                    v-for="level in 3"
                    :key="level"
                    class="password-strength-bar"
                    :class="{ active: level <= passwordStrengthLevel }"
                  />
                </div>
                <span class="password-strength-text">{{ passwordStrengthText }}</span>
              </div>
            </div>
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.email')" prop="email">
            <ElInput
              v-model.trim="formData.email"
              :placeholder="t('userManagement.placeholder.email')"
            />
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.phone')" prop="phone">
            <ElInput
              v-model.trim="formData.phone"
              :placeholder="t('userManagement.placeholder.phone')"
            />
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.gender')" prop="gender">
            <ElSegmented v-model="formData.gender" :options="genderOptions" />
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.roles')" prop="roles">
            <ElSelect v-model="formData.roles" multiple class="w-full" filterable>
              <ElOption
                v-for="role in roleList"
                :key="role.roleCode"
                :value="role.roleCode"
                :label="role.roleName"
              />
            </ElSelect>
          </ElFormItem>
          <ElFormItem :label="t('userManagement.fields.departments')" prop="departmentIds">
            <ElTreeSelect
              v-model="formData.departmentIds"
              :data="localizedDepartments"
              node-key="id"
              :props="{ label: 'name', children: 'children' }"
              multiple
              check-strictly
              clearable
              filterable
              class="w-full"
            />
          </ElFormItem>
          <ElFormItem :label="t('listFilter.common.status')">
            <ElSwitch
              v-model="formData.isEnabled"
              :active-text="t('userManagement.status.enabled')"
              :inactive-text="t('userManagement.status.disabled')"
            />
          </ElFormItem>
        </ElForm>
      </ElTabPane>
      <ElTabPane v-if="props.type === 'edit'" :label="t('rawData')" name="raw-data"
        ><ArtRawData :data="rawData"
      /></ElTabPane>
    </ElTabs>
    <template #footer>
      <ElButton :disabled="props.saving" @click="dialogVisible = false">{{
        t('common.cancel')
      }}</ElButton>
      <ElButton type="primary" :loading="props.saving" @click="handleSubmit">{{
        t('userManagement.actions.save')
      }}</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchGetDepartmentTree, fetchGetUserRoles } from '@/api/system-manage'
  import { ElMessage, type FormInstance, type FormRules, type UploadFile } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
  import defaultAvatar from '@/assets/images/user/avatar.png'

  const props = defineProps<{
    visible: boolean
    type: string
    userData?: Partial<Api.SystemManage.UserListItem>
    saving?: boolean
  }>()
  const emit = defineEmits<{
    (e: 'update:visible', value: boolean): void
    (e: 'submit', value: Api.SystemManage.SaveUserParams): void
  }>()
  const { t } = useI18n()
  const defaultDepartmentCode = 'DEFAULT'
  const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })
  const dialogType = computed(() => props.type)
  const formRef = ref<FormInstance>()
  const activeTab = ref('form')
  const roleList = ref<Api.SystemManage.RoleListItem[]>([])
  const departmentList = ref<Api.SystemManage.DepartmentTreeItem[]>([])
  const avatarFile = ref<File>()
  const avatarPreview = ref('')
  const avatarRemoved = ref(false)

  const handleAvatarError = (): void => {
    if (avatarPreview.value !== defaultAvatar) avatarPreview.value = defaultAvatar
  }
  const localizedDepartments = computed(() => {
    const localize = (
      items: Api.SystemManage.DepartmentTreeItem[]
    ): Api.SystemManage.DepartmentTreeItem[] =>
      items.map((item) => ({
        ...item,
        name:
          item.code === defaultDepartmentCode ? t('userManagement.defaultDepartment') : item.name,
        children: localize(item.children)
      }))
    return localize(departmentList.value)
  })
  const genderOptions = computed(() => [
    { label: t('userManagement.gender.male'), value: 1 },
    { label: t('userManagement.gender.female'), value: 2 }
  ])
  const formData = reactive<Api.SystemManage.SaveUserParams>({
    userName: '',
    password: '',
    email: '',
    phone: '',
    gender: 1,
    roles: [],
    departmentIds: [],
    isEnabled: true
  })
  const passwordStrengthLevel = computed(() => {
    const password = formData.password
    if (!password) return 0
    const valid = password.length >= 8 && /[A-Za-z]/.test(password) && /\d/.test(password)
    if (!valid) return 1
    return password.length >= 12 ||
      (/[a-z]/.test(password) && /[A-Z]/.test(password)) ||
      /[^A-Za-z0-9]/.test(password)
      ? 3
      : 2
  })
  const passwordStrengthText = computed(() => {
    const keys = ['', 'weak', 'medium', 'strong']
    return passwordStrengthLevel.value
      ? t(`register.passwordStrength.${keys[passwordStrengthLevel.value]}`)
      : ''
  })
  const rawData = computed(() => (props.type === 'edit' ? props.userData : formData))
  const rules = computed<FormRules>(() => ({
    userName: [
      { required: true, message: t('userManagement.validation.userNameRequired'), trigger: 'blur' },
      { min: 2, max: 50, message: t('userManagement.validation.userNameLength'), trigger: 'blur' }
    ],
    password: [
      {
        validator: (_rule, value, callback) => {
          if (props.type === 'add' && !value)
            callback(new Error(t('userManagement.validation.passwordRequired')))
          else if (value && (value.length < 8 || !/[A-Za-z]/.test(value) || !/\d/.test(value)))
            callback(new Error(t('register.rule.passwordStrength')))
          else callback()
        },
        trigger: 'blur'
      }
    ],
    email: [
      { required: true, message: t('userManagement.placeholder.email'), trigger: 'blur' },
      { type: 'email', message: t('userManagement.validation.emailInvalid'), trigger: 'blur' }
    ],
    roles: [
      {
        required: true,
        type: 'array',
        min: 1,
        message: t('userManagement.validation.rolesRequired'),
        trigger: 'change'
      }
    ]
  }))

  watch(
    () => props.visible,
    async (visible) => {
      if (!visible) return
      activeTab.value = 'form'
      if (!roleList.value.length) roleList.value = await fetchGetUserRoles()
      if (!departmentList.value.length) departmentList.value = await fetchGetDepartmentTree()
      const row = props.userData
      avatarFile.value = undefined
      avatarRemoved.value = false
      avatarPreview.value = props.type === 'edit' ? (row?.avatar ?? '') : ''
      Object.assign(formData, {
        userName: props.type === 'edit' ? (row?.userName ?? '') : '',
        password: '',
        email: props.type === 'edit' ? (row?.userEmail ?? '') : '',
        phone: props.type === 'edit' ? (row?.userPhone ?? '') : '',
        gender: props.type === 'edit' ? (row?.userGender ?? 1) : 1,
        roles: props.type === 'edit' ? [...(row?.userRoles ?? [])] : ['R_USER'],
        departmentIds: props.type === 'edit' ? [...(row?.departmentIds ?? [])] : [],
        isEnabled: props.type === 'edit' ? (row?.isEnabled ?? true) : true
      })
      nextTick(() => formRef.value?.clearValidate())
    }
  )

  const handleSubmit = async () => {
    if (props.saving) return
    if (formRef.value && (await formRef.value.validate())) {
      emit('submit', {
        ...formData,
        avatarFile: avatarFile.value,
        removeAvatar: avatarRemoved.value,
        roles: [...formData.roles],
        departmentIds: [...formData.departmentIds]
      })
    }
  }

  const handleAvatarChange = (uploadFile: UploadFile) => {
    const file = uploadFile.raw
    if (!file) return
    const extension = file.name.split('.').pop()?.toLowerCase()
    if (
      !file.type.startsWith('image/') ||
      !extension ||
      !['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'tif', 'tiff'].includes(extension)
    ) {
      ElMessage.error('头像只允许上传图像格式')
      return
    }
    if (file.size > 500 * 1024) {
      ElMessage.error('头像大小不能超过 500 KB')
      return
    }
    if (avatarPreview.value.startsWith('blob:')) URL.revokeObjectURL(avatarPreview.value)
    avatarFile.value = file
    avatarRemoved.value = false
    avatarPreview.value = URL.createObjectURL(file)
  }

  const clearAvatar = () => {
    if (avatarPreview.value.startsWith('blob:')) URL.revokeObjectURL(avatarPreview.value)
    avatarFile.value = undefined
    avatarRemoved.value = props.type === 'edit'
    avatarPreview.value = defaultAvatar
  }
</script>

<style scoped>
  .password-field {
    width: 100%;
  }
  .avatar-field {
    display: flex;
    align-items: center;
    gap: 12px;
  }
  .password-strength {
    display: flex;
    align-items: center;
    gap: 10px;
    margin-top: 8px;
  }
  .password-strength-bars {
    display: grid;
    flex: 1;
    grid-template-columns: repeat(3, 1fr);
    gap: 6px;
  }
  .password-strength-bar {
    height: 4px;
    border-radius: 2px;
    background: var(--el-border-color);
    transition: background-color 0.2s ease;
  }
  .password-strength-bar.active {
    background: #dc2626;
  }
  .password-strength-bar.active:nth-child(2) {
    background: #d97706;
  }
  .password-strength-bar.active:nth-child(3) {
    background: #16a34a;
  }
  .password-strength-text {
    min-width: 28px;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    line-height: 1;
    text-align: right;
  }
</style>

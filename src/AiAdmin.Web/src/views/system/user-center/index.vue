<!-- 个人中心页面 -->
<template>
  <div class="w-full h-full p-0 bg-transparent border-none shadow-none">
    <div class="relative flex-b mt-2.5 max-md:block max-md:mt-1">
      <div class="w-112 mr-5 max-md:w-full max-md:mr-0">
        <div class="art-card-sm relative p-9 pb-6 overflow-hidden text-center">
          <img class="absolute top-0 left-0 w-full h-50 object-cover" src="@imgs/user/bg.webp" />
          <ElUpload
            accept="image/*"
            :auto-upload="false"
            :show-file-list="false"
            :on-change="handleAvatarChange"
          >
            <div class="avatar-upload relative z-10 w-20 h-20 mt-30 mx-auto">
              <img
                class="w-full h-full object-cover border-2 border-white rounded-full"
                :src="userInfo.avatar || defaultAvatar"
                :alt="displayName"
                @error="handleAvatarError"
              />
              <div class="avatar-upload-mask"><ArtSvgIcon icon="ri:camera-line" /></div>
              <ElButton
                v-if="userInfo.avatar"
                class="avatar-delete"
                circle
                text
                type="danger"
                aria-label="删除头像"
                @click.stop.prevent="removeAvatar"
              >
                <ArtSvgIcon icon="ri:delete-bin-line" />
              </ElButton>
            </div>
          </ElUpload>
          <h2 class="mt-5 text-xl font-normal">{{ displayName }}</h2>

          <div class="w-75 mx-auto mt-7.5 text-left">
            <div class="mt-2.5">
              <ArtSvgIcon icon="ri:user-3-line" class="text-g-700" />
              <span class="ml-2 text-sm">{{ genderLabel }}</span>
            </div>
            <div class="mt-2.5">
              <ArtSvgIcon icon="ri:mail-line" class="text-g-700" />
              <span class="ml-2 text-sm">{{ userInfo.email || t('userCenter.empty.email') }}</span>
            </div>
            <div class="mt-2.5">
              <ArtSvgIcon icon="ri:phone-line" class="text-g-700" />
              <span class="ml-2 text-sm">{{ userInfo.phone || t('userCenter.empty.phone') }}</span>
            </div>
          </div>

          <div class="mt-10">
            <h3 class="text-sm font-medium">{{ t('userCenter.roles') }}</h3>
            <div class="flex flex-wrap justify-center mt-3.5">
              <div
                v-for="item in userInfo.roles || []"
                :key="item"
                class="py-1 px-1.5 mr-2.5 mb-2.5 text-xs border border-g-300 rounded"
              >
                {{ getRoleName(item) }}
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="flex-1 overflow-hidden max-md:w-full max-md:mt-3.5">
        <div class="art-card-sm">
          <h1 class="p-4 text-xl font-normal border-b border-g-300">{{
            t('userCenter.profile.title')
          }}</h1>

          <ElForm
            :model="form"
            class="box-border p-5 [&>.el-row_.el-form-item]:w-[calc(50%-10px)] [&>.el-row_.el-input]:w-full [&>.el-row_.el-select]:w-full"
            ref="ruleFormRef"
            :rules="rules"
            label-width="86px"
            label-position="top"
          >
            <ElRow>
              <ElFormItem :label="t('userCenter.profile.userName')">
                <ElInput v-model="form.userName" disabled />
              </ElFormItem>
              <ElFormItem :label="t('userCenter.profile.gender')" prop="gender" class="ml-5">
                <ElSelect
                  v-model="form.gender"
                  :placeholder="t('userCenter.validation.genderRequired')"
                  :disabled="!isEdit"
                  filterable
                >
                  <ElOption
                    v-for="item in options"
                    :key="item.value"
                    :label="item.label"
                    :value="item.value"
                  />
                </ElSelect>
              </ElFormItem>
            </ElRow>

            <ElRow>
              <ElFormItem :label="t('userCenter.profile.email')" prop="email">
                <ElInput v-model="form.email" :disabled="!isEdit" />
              </ElFormItem>
              <ElFormItem :label="t('userCenter.profile.phone')" prop="phone" class="ml-5">
                <ElInput v-model="form.phone" :disabled="!isEdit" />
              </ElFormItem>
            </ElRow>

            <div class="flex-c justify-end [&_.el-button]:!w-27.5">
              <ElButton type="primary" class="w-22.5" v-ripple @click="edit">
                {{
                  t(isEdit ? 'userCenter.actions.saveProfile' : 'userCenter.actions.editProfile')
                }}
              </ElButton>
            </div>
          </ElForm>
        </div>

        <div class="art-card-sm my-5">
          <h1 class="p-4 text-xl font-normal border-b border-g-300">{{
            t('userCenter.password.title')
          }}</h1>

          <ElForm
            ref="pwdFormRef"
            :model="pwdForm"
            :rules="pwdRules"
            class="box-border p-5"
            label-width="86px"
            label-position="top"
          >
            <ElFormItem :label="t('userCenter.password.current')" prop="currentPassword">
              <ElInput
                v-model="pwdForm.currentPassword"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <ElFormItem :label="t('userCenter.password.new')" prop="newPassword">
              <ElInput
                v-model="pwdForm.newPassword"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <ElFormItem :label="t('userCenter.password.confirm')" prop="confirmPassword">
              <ElInput
                v-model="pwdForm.confirmPassword"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <div class="flex-c justify-end [&_.el-button]:!w-27.5">
              <ElButton type="primary" class="w-22.5" v-ripple @click="editPwd">
                {{
                  t(
                    isEditPwd
                      ? 'userCenter.actions.savePassword'
                      : 'userCenter.actions.changePassword'
                  )
                }}
              </ElButton>
            </div>
          </ElForm>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import defaultAvatar from '@/assets/images/user/avatar.png'
  import { fetchChangeUserPassword, fetchUpdateUserProfile } from '@/api/auth'
  import { fetchDeleteUserAvatar, fetchUploadUserAvatar } from '@/api/system-manage'
  import { useUserStore } from '@/store/modules/user'
  import { ElMessage, type FormInstance, type FormRules, type UploadFile } from 'element-plus'
  import { useI18n } from 'vue-i18n'

  defineOptions({ name: 'UserCenter' })

  const userStore = useUserStore()
  const { t } = useI18n()
  const userInfo = computed(() => userStore.getUserInfo)

  const isEdit = ref(false)
  const isEditPwd = ref(false)
  const ruleFormRef = ref<FormInstance>()
  const pwdFormRef = ref<FormInstance>()
  const displayName = computed(() => userInfo.value.userName || t('userCenter.empty.user'))
  const handleAvatarError = (event: Event): void => {
    const image = event.target as HTMLImageElement
    image.src = defaultAvatar
  }
  const genderLabel = computed(() =>
    t(userInfo.value.gender === 2 ? 'userCenter.gender.female' : 'userCenter.gender.male')
  )

  /**
   * 用户信息表单
   */
  const form = reactive({
    userName: '',
    email: '',
    phone: '',
    gender: 1 as 1 | 2
  })

  /**
   * 密码修改表单
   */
  const pwdForm = reactive({
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  })

  /**
   * 表单验证规则
   */
  const rules = computed<FormRules>(() => ({
    email: [
      { required: true, message: t('userCenter.validation.emailRequired'), trigger: 'blur' },
      { type: 'email', message: t('userCenter.validation.emailInvalid'), trigger: 'blur' }
    ],
    gender: [
      { required: true, message: t('userCenter.validation.genderRequired'), trigger: 'change' }
    ]
  }))

  const pwdRules = computed<FormRules>(() => ({
    currentPassword: [
      {
        required: true,
        message: t('userCenter.validation.currentPasswordRequired'),
        trigger: 'blur'
      }
    ],
    newPassword: [
      { required: true, message: t('userCenter.validation.newPasswordRequired'), trigger: 'blur' },
      { min: 6, message: t('userCenter.validation.passwordLength'), trigger: 'blur' }
    ],
    confirmPassword: [
      {
        required: true,
        message: t('userCenter.validation.confirmPasswordRequired'),
        trigger: 'blur'
      },
      {
        validator: (_rule, value, callback) => {
          if (value !== pwdForm.newPassword)
            callback(new Error(t('userCenter.validation.passwordMismatch')))
          else callback()
        },
        trigger: 'blur'
      }
    ]
  }))

  /**
   * 性别选项
   */
  const options = computed(() => [
    { value: 1, label: t('userCenter.gender.male') },
    { value: 2, label: t('userCenter.gender.female') }
  ])

  onMounted(() => {
    syncForm()
  })

  watch(userInfo, syncForm, { deep: true })

  function syncForm() {
    form.userName = userInfo.value.userName || ''
    form.email = userInfo.value.email || ''
    form.phone = userInfo.value.phone || ''
    form.gender = userInfo.value.gender || 1
  }

  const getRoleName = (role: string) => {
    const key = `userCenter.roleNames.${role}`
    return t(key) === key ? role : t(key)
  }

  const handleAvatarChange = async (uploadFile: UploadFile) => {
    const file = uploadFile.raw
    const userId = userInfo.value.userId
    if (!file || !userId) return
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
    const result = await fetchUploadUserAvatar(userId, file)
    userStore.setUserInfo({ ...userInfo.value, avatar: result.avatar } as Api.Auth.UserInfo)
    ElMessage.success('头像更新成功')
  }

  const removeAvatar = async (): Promise<void> => {
    const userId = userInfo.value.userId
    if (!userId) return
    const result = await fetchDeleteUserAvatar(userId)
    userStore.setUserInfo({ ...userInfo.value, avatar: result.avatar || '' } as Api.Auth.UserInfo)
    ElMessage.success('头像已删除')
  }

  /**
   * 切换用户信息编辑状态
   */
  const edit = async () => {
    if (!isEdit.value) {
      isEdit.value = true
      return
    }

    if (!ruleFormRef.value || !(await ruleFormRef.value.validate().catch(() => false))) return
    const data = await fetchUpdateUserProfile({
      email: form.email,
      phone: form.phone,
      gender: form.gender
    })
    userStore.setUserInfo(data)
    isEdit.value = false
    ElMessage.success(t('userCenter.messages.profileUpdated'))
  }

  /**
   * 切换密码编辑状态
   */
  const editPwd = async () => {
    if (!isEditPwd.value) {
      isEditPwd.value = true
      return
    }

    if (!pwdFormRef.value || !(await pwdFormRef.value.validate().catch(() => false))) return
    await fetchChangeUserPassword({
      currentPassword: pwdForm.currentPassword,
      newPassword: pwdForm.newPassword
    })
    Object.assign(pwdForm, { currentPassword: '', newPassword: '', confirmPassword: '' })
    pwdFormRef.value.resetFields()
    isEditPwd.value = false
    ElMessage.success(t('userCenter.messages.passwordChanged'))
  }
</script>

<style scoped>
  .avatar-upload-mask {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    color: white;
    font-size: 22px;
    cursor: pointer;
    background: rgb(0 0 0 / 45%);
    border-radius: 50%;
    opacity: 0;
    transition: opacity 0.2s;
  }

  .avatar-delete {
    position: absolute;
    right: 2px;
    bottom: 2px;
    z-index: 2;
    width: 26px;
    height: 26px;
    color: var(--el-color-danger);
    background: rgb(255 255 255 / 90%);
  }

  .avatar-upload:hover .avatar-upload-mask {
    opacity: 1;
  }
</style>

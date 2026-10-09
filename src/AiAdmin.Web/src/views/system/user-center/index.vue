<!-- 个人中心页面 -->
<template>
    <div class="w-full h-full p-0 bg-transparent border-none shadow-none">
        <div class="relative flex-b mt-2.5 max-md:block max-md:mt-1">
            <div class="w-112 mr-5 max-md:w-full max-md:mr-0">
                <div class="art-card-sm relative p-9 pb-6 overflow-hidden text-center">
                    <img class="absolute top-0 left-0 w-full h-50 object-cover" src="@imgs/user/bg.webp" />
                    <ElUpload :auto-upload="false" :on-change="handleAvatarChange" :show-file-list="false" accept="image/*">
                        <div class="avatar-upload relative z-10 w-20 h-20 mt-30 mx-auto">
                            <ArtUserAvatar
                                :name="displayName"
                                :size="80"
                                :src="userInfo.avatar"
                                class="w-full h-full border-2 border-white rounded-full" />
                            <div class="avatar-upload-mask"><ArtSvgIcon icon="ri:camera-line" /></div>
                            <ElButton
                                v-if="userInfo.avatar"
                                :aria-label="t('userCenter.actions.deleteAvatar')"
                                @click.stop.prevent="removeAvatar"
                                circle
                                class="avatar-delete"
                                text
                                type="danger">
                                <ArtSvgIcon icon="ri:delete-bin-line" />
                            </ElButton>
                        </div>
                    </ElUpload>
                    <h2 class="mt-5 text-xl font-normal">{{ displayName }}</h2>

                    <div class="w-75 mx-auto mt-7.5 text-left">
                        <div class="mt-2.5">
                            <ArtSvgIcon class="text-g-700" icon="ri:user-3-line" />
                            <span class="ml-2 text-sm">{{ genderLabel }}</span>
                        </div>
                        <div class="mt-2.5">
                            <ArtSvgIcon class="text-g-700" icon="ri:mail-line" />
                            <span class="ml-2 text-sm">{{ userInfo.email || t('userCenter.empty.email') }}</span>
                        </div>
                        <div class="mt-2.5">
                            <ArtSvgIcon class="text-g-700" icon="ri:phone-line" />
                            <span class="ml-2 text-sm">{{ userInfo.phone || t('userCenter.empty.phone') }}</span>
                        </div>
                    </div>

                    <div class="mt-10">
                        <h3 class="text-sm font-medium">{{ t('userCenter.roles') }}</h3>
                        <div class="flex flex-wrap justify-center mt-3.5">
                            <div
                                v-for="item in userInfo.roles || []"
                                :key="item"
                                class="py-1 px-1.5 mr-2.5 mb-2.5 text-xs border border-g-300 rounded">
                                {{ getRoleName(item) }}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            <div class="flex-1 overflow-hidden max-md:w-full max-md:mt-3.5">
                <div class="art-card-sm">
                    <h1 class="p-4 text-xl font-normal border-b border-g-300">{{ t('userCenter.profile.title') }}</h1>

                    <ElForm
                        :model="form"
                        :rules="rules"
                        class="box-border p-5 [&>.el-row_.el-form-item]:w-[calc(50%-10px)] [&>.el-row_.el-input]:w-full [&>.el-row_.el-select]:w-full"
                        label-position="top"
                        label-width="86px"
                        ref="ruleFormRef">
                        <ElRow>
                            <ElFormItem :label="t('userCenter.profile.userName')">
                                <ElInput v-model="form.userName" disabled />
                            </ElFormItem>
                            <ElFormItem :label="t('userCenter.profile.gender')" class="ml-5" prop="gender">
                                <ElSelect
                                    v-model="form.gender"
                                    :disabled="!isEdit"
                                    :placeholder="t('userCenter.validation.genderRequired')"
                                    filterable>
                                    <ElOption v-for="item in options" :key="item.value" :label="item.label" :value="item.value" />
                                </ElSelect>
                            </ElFormItem>
                        </ElRow>

                        <ElRow>
                            <ElFormItem :label="t('userCenter.profile.email')" prop="email">
                                <ElInput v-model="form.email" :disabled="!isEdit" />
                            </ElFormItem>
                            <ElFormItem :label="t('userCenter.profile.phone')" class="ml-5" prop="phone">
                                <ElInput v-model="form.phone" :disabled="!isEdit" />
                            </ElFormItem>
                        </ElRow>

                        <ElRow v-if="isEdit && emailChanged">
                            <ElFormItem :label="t('userCenter.profile.emailVerificationCode')" prop="emailVerificationCode">
                                <div class="flex w-full gap-2">
                                    <ElInput
                                        v-model="form.emailVerificationCode"
                                        :placeholder="
                                            t('userCenter.profile.emailVerificationCodePlaceholder', {
                                                email: userInfo.email,
                                            })
                                        " />
                                    <ElButton
                                        :disabled="sendCodeCountdown > 0"
                                        :loading="sendingCode"
                                        @click="sendEmailCode"
                                        class="shrink-0"
                                        type="primary">
                                        {{
                                            sendCodeCountdown > 0
                                                ? t('userCenter.actions.resendCode', { seconds: sendCodeCountdown })
                                                : t('userCenter.actions.sendCode')
                                        }}
                                    </ElButton>
                                </div>
                            </ElFormItem>
                        </ElRow>

                        <div class="flex-c justify-end [&_.el-button]:!w-27.5">
                            <ElButton v-if="isEdit" v-ripple @click="cancelEditProfile">
                                {{ t('userCenter.actions.cancel') }}
                            </ElButton>
                            <ElButton v-ripple @click="edit" class="w-22.5" type="primary">
                                {{ t(isEdit ? 'userCenter.actions.saveProfile' : 'userCenter.actions.editProfile') }}
                            </ElButton>
                        </div>
                    </ElForm>
                </div>

                <div class="art-card-sm my-5">
                    <h1 class="p-4 text-xl font-normal border-b border-g-300">{{ t('userCenter.password.title') }}</h1>

                    <ElForm :model="pwdForm" :rules="pwdRules" class="box-border p-5" label-position="top" label-width="86px" ref="pwdFormRef">
                        <ElFormItem :label="t('userCenter.password.current')" prop="currentPassword">
                            <ElInput v-model="pwdForm.currentPassword" :disabled="!isEditPwd" show-password type="password" />
                        </ElFormItem>

                        <ElFormItem :label="t('userCenter.password.new')" prop="newPassword">
                            <ElInput v-model="pwdForm.newPassword" :disabled="!isEditPwd" show-password type="password" />
                            <div v-if="isEditPwd" aria-live="polite" class="password-strength">
                                <div class="password-strength-bars">
                                    <span v-for="level in 3" :class="{ active: passwordStrengthLevel >= level }" :key="level" />
                                </div>
                                <span>{{ t('userCenter.password.strength', { level: passwordStrengthText }) }}</span>
                            </div>
                        </ElFormItem>

                        <ElFormItem :label="t('userCenter.password.confirm')" prop="confirmPassword">
                            <ElInput v-model="pwdForm.confirmPassword" :disabled="!isEditPwd" show-password type="password" />
                        </ElFormItem>

                        <div class="flex-c justify-end [&_.el-button]:!w-27.5">
                            <ElButton v-if="isEditPwd" v-ripple @click="cancelEditPassword">
                                {{ t('userCenter.actions.cancel') }}
                            </ElButton>
                            <ElButton v-ripple @click="editPwd" class="w-22.5" type="primary">
                                {{ t(isEditPwd ? 'userCenter.actions.savePassword' : 'userCenter.actions.changePassword') }}
                            </ElButton>
                        </div>
                    </ElForm>
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'
import { fetchChangeUserPassword, fetchProfileEmailCode, fetchUpdateUserProfile } from '@/api/auth'
import { fetchDeleteCurrentUserAvatar, fetchUploadCurrentUserAvatar } from '@/api/system-manage'
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
const genderLabel = computed(() => t(userInfo.value.gender === 2 ? 'userCenter.gender.female' : 'userCenter.gender.male'))
const emailChanged = computed(() => form.email.trim().toLowerCase() !== (userInfo.value.email || '').trim().toLowerCase())
const sendingCode = ref(false)
const sendCodeCountdown = ref(0)
let sendCodeTimer: ReturnType<typeof setInterval> | undefined

/**
 * 用户信息表单
 */
const form = reactive({
    userName: '',
    email: '',
    emailVerificationCode: '',
    phone: '',
    gender: 1 as 1 | 2,
})

/**
 * 密码修改表单
 */
const pwdForm = reactive({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
})

/**
 * 表单验证规则
 */
const rules = computed<FormRules>(() => ({
    email: [
        { required: true, message: t('userCenter.validation.emailRequired'), trigger: 'blur' },
        { type: 'email', message: t('userCenter.validation.emailInvalid'), trigger: 'blur' },
    ],
    emailVerificationCode: [
        {
            validator: (_rule, value, callback) => {
                if (emailChanged.value && !/^\d{6}$/.test(value)) callback(new Error(t('userCenter.validation.emailCodeRequired')))
                else callback()
            },
            trigger: 'blur',
        },
    ],
    gender: [{ required: true, message: t('userCenter.validation.genderRequired'), trigger: 'change' }],
}))

const pwdRules = computed<FormRules>(() => ({
    currentPassword: [
        {
            required: true,
            message: t('userCenter.validation.currentPasswordRequired'),
            trigger: 'blur',
        },
    ],
    newPassword: [
        { required: true, message: t('userCenter.validation.newPasswordRequired'), trigger: 'blur' },
        { min: 8, message: t('userCenter.validation.passwordLength'), trigger: 'blur' },
        {
            validator: (_rule, value, callback) => {
                if (value && (!/[A-Za-z]/.test(value) || !/\d/.test(value))) callback(new Error(t('userCenter.validation.passwordStrength')))
                else callback()
            },
            trigger: 'blur',
        },
    ],
    confirmPassword: [
        {
            required: true,
            message: t('userCenter.validation.confirmPasswordRequired'),
            trigger: 'blur',
        },
        {
            validator: (_rule, value, callback) => {
                if (value !== pwdForm.newPassword) callback(new Error(t('userCenter.validation.passwordMismatch')))
                else callback()
            },
            trigger: 'blur',
        },
    ],
}))

const passwordStrengthLevel = computed(() => {
    const password = pwdForm.newPassword
    if (!password) return 0
    if (password.length < 8 || !/[A-Za-z]/.test(password) || !/\d/.test(password)) return 1
    return password.length >= 12 || (/[a-z]/.test(password) && /[A-Z]/.test(password)) || /[^A-Za-z0-9]/.test(password) ? 3 : 2
})
const passwordStrengthText = computed(() => {
    const keys = ['weak', 'weak', 'medium', 'strong']
    return t(`userCenter.password.strengthLevels.${keys[passwordStrengthLevel.value]}`)
})

/**
 * 性别选项
 */
const options = computed(() => [
    { value: 1, label: t('userCenter.gender.male') },
    { value: 2, label: t('userCenter.gender.female') },
])

onMounted(() => {
    syncForm()
})

onBeforeUnmount(() => clearInterval(sendCodeTimer))

watch(userInfo, syncForm, { deep: true })

function syncForm() {
    form.userName = userInfo.value.userName || ''
    form.email = userInfo.value.email || ''
    form.emailVerificationCode = ''
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
    if (!file.type.startsWith('image/') || !extension || !['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'tif', 'tiff'].includes(extension)) {
        ElMessage.error(t('userCenter.messages.avatarFormatInvalid'))
        return
    }
    if (file.size > 500 * 1024) {
        ElMessage.error(t('userCenter.messages.avatarTooLarge'))
        return
    }
    const result = await fetchUploadCurrentUserAvatar(file)
    userStore.setUserInfo({ ...userInfo.value, avatar: result.avatar } as Api.Auth.UserInfo)
    ElMessage.success(t('userCenter.messages.avatarUpdated'))
}

const removeAvatar = async (): Promise<void> => {
    const userId = userInfo.value.userId
    if (!userId) return
    const result = await fetchDeleteCurrentUserAvatar()
    userStore.setUserInfo({ ...userInfo.value, avatar: result.avatar || '' } as Api.Auth.UserInfo)
    ElMessage.success(t('userCenter.messages.avatarDeleted'))
}

/**
 * 向待绑定的新邮箱发送验证码
 */
const sendEmailCode = async () => {
    if (!ruleFormRef.value || !(await ruleFormRef.value.validateField('email').catch(() => false))) return
    sendingCode.value = true
    try {
        await fetchProfileEmailCode()
        ElMessage.success(t('userCenter.messages.emailCodeSent'))
        sendCodeCountdown.value = 60
        clearInterval(sendCodeTimer)
        sendCodeTimer = setInterval(() => {
            sendCodeCountdown.value--
            if (sendCodeCountdown.value <= 0) clearInterval(sendCodeTimer)
        }, 1000)
    } finally {
        sendingCode.value = false
    }
}

/**
 * 取消编辑个人资料并恢复当前数据
 */
const cancelEditProfile = () => {
    isEdit.value = false
    clearInterval(sendCodeTimer)
    sendCodeCountdown.value = 0
    syncForm()
    ruleFormRef.value?.clearValidate()
}

/**
 * 取消修改密码并清空输入
 */
const cancelEditPassword = () => {
    isEditPwd.value = false
    Object.assign(pwdForm, { currentPassword: '', newPassword: '', confirmPassword: '' })
    pwdFormRef.value?.resetFields()
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
        emailVerificationCode: emailChanged.value ? form.emailVerificationCode : undefined,
        phone: form.phone,
        gender: form.gender,
        version: userInfo.value.version ?? 0,
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
        newPassword: pwdForm.newPassword,
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

.password-strength {
    display: flex;
    gap: 10px;
    align-items: center;
    width: 100%;
    margin-top: 8px;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    line-height: 1;
}

.password-strength-bars {
    display: grid;
    flex: 1;
    grid-template-columns: repeat(3, 1fr);
    gap: 5px;
}

.password-strength-bars span {
    height: 4px;
    background: var(--el-border-color);
    border-radius: 2px;
}

.password-strength-bars span.active:nth-child(1) {
    background: var(--el-color-danger);
}

.password-strength-bars span.active:nth-child(2) {
    background: var(--el-color-warning);
}

.password-strength-bars span.active:nth-child(3) {
    background: var(--el-color-success);
}
</style>
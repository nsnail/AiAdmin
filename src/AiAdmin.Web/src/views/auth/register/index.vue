<!-- 注册页面 -->
<template>
    <div class="flex w-full h-screen">
        <LoginLeftView />

        <div class="relative flex-1">
            <AuthTopBar />

            <div class="auth-right-wrap">
                <div class="form">
                    <h3 class="title">{{ $t('register.title') }}</h3>
                    <p class="sub-title">{{ $t('register.subTitle') }}</p>
                    <ElForm :key="formKey" :model="formData" :rules="rules" class="mt-7.5" label-position="top" ref="formRef">
                        <ElFormItem prop="username">
                            <ElInput v-model.trim="formData.username" :placeholder="$t('register.placeholder.username')" class="custom-height" />
                        </ElFormItem>

                        <ElFormItem prop="email">
                            <ElInput v-model.trim="formData.email" :placeholder="$t('register.placeholder.email')" class="custom-height" />
                        </ElFormItem>
                        <ElFormItem prop="invitationCode">
                            <ElInput
                                v-model.trim="formData.invitationCode"
                                :placeholder="$t('register.placeholder.invitationCode')"
                                class="custom-height"
                                maxlength="12" />
                        </ElFormItem>
                        <ElFormItem v-if="emailVerificationEnabled" prop="verificationCode">
                            <div class="verification-row">
                                <ElInput
                                    v-model.trim="formData.verificationCode"
                                    :placeholder="$t('register.placeholder.verificationCode')"
                                    class="custom-height" />
                                <ElButton
                                    :disabled="sendCooldown > 0"
                                    :loading="codeSending"
                                    @click="sendCode"
                                    class="verification-button"
                                    type="primary">
                                    {{ sendCooldown > 0 ? `${sendCooldown}s` : $t('register.sendCode') }}
                                </ElButton>
                            </div>
                        </ElFormItem>

                        <ElFormItem prop="password">
                            <div class="password-field">
                                <ElInput
                                    v-model.trim="formData.password"
                                    :placeholder="$t('register.placeholder.password')"
                                    autocomplete="off"
                                    class="custom-height"
                                    show-password
                                    type="password" />
                                <div
                                    v-if="formData.password"
                                    :aria-label="t('register.passwordStrength.label', { level: passwordStrengthText })"
                                    class="password-strength"
                                    role="status">
                                    <div aria-hidden="true" class="password-strength-bars">
                                        <span
                                            v-for="level in 3"
                                            :class="{ active: level <= passwordStrengthLevel }"
                                            :key="level"
                                            class="password-strength-bar" />
                                    </div>
                                    <span class="password-strength-text">{{ passwordStrengthText }}</span>
                                </div>
                            </div>
                        </ElFormItem>

                        <ElFormItem prop="confirmPassword">
                            <ElInput
                                v-model.trim="formData.confirmPassword"
                                :placeholder="$t('register.placeholder.confirmPassword')"
                                @keyup.enter="register"
                                autocomplete="off"
                                class="custom-height"
                                show-password
                                type="password" />
                        </ElFormItem>

                        <ElFormItem prop="agreement">
                            <ElCheckbox v-model="formData.agreement">
                                {{ $t('register.agreeText') }}
                                <RouterLink style="color: var(--theme-color); text-decoration: none" to="/privacy-policy">{{
                                    $t('register.privacyPolicy')
                                }}</RouterLink>
                            </ElCheckbox>
                        </ElFormItem>

                        <div style="margin-top: 15px">
                            <ElButton v-ripple :loading="loading" @click="register" class="w-full custom-height" type="primary">
                                {{ $t('register.submitBtnText') }}
                            </ElButton>
                        </div>

                        <div class="mt-5 text-sm text-g-600">
                            <span>{{ $t('register.hasAccount') }}</span>
                            <RouterLink :to="{ name: 'Login' }" class="text-theme">{{ $t('register.toLogin') }}</RouterLink>
                        </div>
                    </ElForm>
                </div>
            </div>
        </div>

        <ElDialog v-model="puzzleVisible" :title="$t('register.puzzle.title')" destroy-on-close width="380px">
            <div v-loading="puzzleLoading" class="puzzle-wrap">
                <div v-if="puzzle" :style="{ width: `${puzzle.width}px`, height: `${puzzle.height}px` }" class="puzzle-board">
                    <img :src="puzzle.backgroundImage" alt="" class="puzzle-background" />
                    <img
                        :src="puzzle.pieceImage"
                        :style="{
                            left: `${puzzleOffset}px`,
                            top: `${puzzle.pieceY}px`,
                            width: `${puzzle.pieceSize}px`,
                        }"
                        alt=""
                        class="puzzle-piece" />
                </div>
                <ElSlider
                    v-if="puzzle"
                    v-model="puzzleOffset"
                    :disabled="puzzleVerifying"
                    :max="puzzle.width - puzzle.pieceSize"
                    :min="0"
                    :show-tooltip="false"
                    @change="verifyPuzzle"
                    class="puzzle-slider mt-4" />
                <div class="puzzle-actions">
                    <ElButton :disabled="puzzleVerifying" :title="$t('register.puzzle.refresh')" @click="loadPuzzle" circle>
                        <ArtSvgIcon icon="ri:refresh-line" />
                    </ElButton>
                    <span class="puzzle-status">{{ puzzleVerifying ? $t('register.puzzle.verifying') : $t('register.puzzle.hint') }}</span>
                </div>
            </div>
        </ElDialog>
    </div>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import { fetchLoginConfig, fetchRegister, fetchRegisterCode, fetchRegisterPuzzle, fetchVerifyRegisterPuzzle } from '@/api/auth'

defineOptions({ name: 'Register' })

interface RegisterForm {
    username: string
    email: string
    verificationCode: string
    invitationCode: string
    password: string
    confirmPassword: string
    agreement: boolean
}

const USERNAME_MIN_LENGTH = 3
const USERNAME_MAX_LENGTH = 20
const PASSWORD_MIN_LENGTH = 8
const REDIRECT_DELAY = 1000

const { t, locale } = useI18n()
const router = useRouter()
const formRef = ref<FormInstance>()

const loading = ref(false)
const emailVerificationEnabled = ref(true)
const formKey = ref(0)
const puzzleVisible = ref(false)
const puzzleLoading = ref(false)
const puzzleVerifying = ref(false)
const puzzleOffset = ref(0)
const puzzle = ref<Api.Auth.RegisterPuzzle>()
const codeSending = ref(false)
const sendCooldown = ref(0)
let cooldownTimer: ReturnType<typeof setInterval> | undefined

// 监听语言切换，重置表单
watch(locale, () => {
    formKey.value++
})

const formData = reactive<RegisterForm>({
    username: '',
    email: '',
    verificationCode: '',
    invitationCode: '',
    password: '',
    confirmPassword: '',
    agreement: false,
})

const passwordStrengthLevel = computed(() => {
    const password = formData.password
    if (!password) return 0

    const meetsRegistrationRule = password.length >= PASSWORD_MIN_LENGTH && /[A-Za-z]/.test(password) && /\d/.test(password)
    if (!meetsRegistrationRule) return 1

    const hasMixedCase = /[a-z]/.test(password) && /[A-Z]/.test(password)
    const hasSymbol = /[^A-Za-z0-9]/.test(password)
    return password.length >= 12 || hasMixedCase || hasSymbol ? 3 : 2
})

const passwordStrengthText = computed(() => {
    const strengthKeys = ['', 'weak', 'medium', 'strong']
    return passwordStrengthLevel.value ? t(`register.passwordStrength.${strengthKeys[passwordStrengthLevel.value]}`) : ''
})

/**
 * 验证密码
 * 当密码输入后，如果确认密码已填写，则触发确认密码的验证
 */
const validatePassword = (_rule: any, value: string, callback: (error?: Error) => void) => {
    if (!value) {
        callback(new Error(t('register.placeholder.password')))
        return
    }

    if (!/[A-Za-z]/.test(value) || !/\d/.test(value)) {
        callback(new Error(t('register.rule.passwordStrength')))
        return
    }

    if (formData.confirmPassword) {
        formRef.value?.validateField('confirmPassword')
    }

    callback()
}

/**
 * 验证确认密码
 * 检查确认密码是否与密码一致
 */
const validateConfirmPassword = (_rule: any, value: string, callback: (error?: Error) => void) => {
    if (!value) {
        callback(new Error(t('register.rule.confirmPasswordRequired')))
        return
    }

    if (value !== formData.password) {
        callback(new Error(t('register.rule.passwordMismatch')))
        return
    }

    callback()
}

/**
 * 验证用户协议
 * 确保用户已勾选同意协议
 */
const validateAgreement = (_rule: any, value: boolean, callback: (error?: Error) => void) => {
    if (!value) {
        callback(new Error(t('register.rule.agreementRequired')))
        return
    }
    callback()
}

const rules = computed<FormRules<RegisterForm>>(() => ({
    username: [
        { required: true, message: t('register.placeholder.username'), trigger: 'blur' },
        {
            min: USERNAME_MIN_LENGTH,
            max: USERNAME_MAX_LENGTH,
            message: t('register.rule.usernameLength'),
            trigger: 'blur',
        },
    ],
    email: [
        { required: true, message: t('register.placeholder.email'), trigger: 'blur' },
        { type: 'email', message: t('register.rule.emailInvalid'), trigger: 'blur' },
    ],
    verificationCode: emailVerificationEnabled.value
        ? [{ required: true, message: t('register.placeholder.verificationCode'), trigger: 'blur' }]
        : [],
    password: [
        { required: true, validator: validatePassword, trigger: 'blur' },
        { min: PASSWORD_MIN_LENGTH, message: t('register.rule.passwordLength'), trigger: 'blur' },
    ],
    confirmPassword: [{ required: true, validator: validateConfirmPassword, trigger: 'blur' }],
    agreement: [{ validator: validateAgreement, trigger: 'change' }],
}))

/**
 * 注册用户
 * 验证表单后提交注册请求
 */
const register = async () => {
    if (!formRef.value) return

    try {
        await formRef.value.validate()
        loading.value = true

        await fetchRegister({
            userName: formData.username,
            password: formData.password,
            email: formData.email,
            verificationCode: formData.verificationCode,
            invitationCode: formData.invitationCode || undefined,
        })
        ElMessage.success(t('register.success'))
        toLogin()
    } catch (error) {
        console.error('表单验证失败:', error)
        loading.value = false
    }
}

const sendCode = async () => {
    if (sendCooldown.value > 0) return
    await formRef.value?.validateField('email')
    puzzleVisible.value = true
    await loadPuzzle()
}

const loadPuzzle = async () => {
    puzzleLoading.value = true
    puzzleOffset.value = 0
    try {
        puzzle.value = await fetchRegisterPuzzle()
    } finally {
        puzzleLoading.value = false
    }
}
const verifyPuzzle = async () => {
    if (!puzzle.value) return
    puzzleVerifying.value = true
    try {
        const result = await fetchVerifyRegisterPuzzle(puzzle.value.challengeId, puzzleOffset.value, formData.email)
        codeSending.value = true
        await fetchRegisterCode(formData.email, result.puzzleTicket)
        puzzleVisible.value = false
        startSendCooldown()
        ElMessage.success(t('register.codeSent'))
    } catch {
        await loadPuzzle()
    } finally {
        codeSending.value = false
        puzzleVerifying.value = false
    }
}
const startSendCooldown = () => {
    sendCooldown.value = 60
    if (cooldownTimer) clearInterval(cooldownTimer)
    cooldownTimer = setInterval(() => {
        sendCooldown.value--
        if (sendCooldown.value <= 0 && cooldownTimer) {
            clearInterval(cooldownTimer)
            cooldownTimer = undefined
        }
    }, 1000)
}
onMounted(async () => {
    emailVerificationEnabled.value = (await fetchLoginConfig()).emailVerificationEnabled
})
onBeforeUnmount(() => {
    if (cooldownTimer) clearInterval(cooldownTimer)
})

/**
 * 跳转到登录页面
 */
const toLogin = () => {
    setTimeout(() => {
        router.push({ name: 'Login' })
    }, REDIRECT_DELAY)
}
</script>

<style scoped>
@import '../login/style.css';

.puzzle-wrap {
    min-height: 230px;
}
.puzzle-board {
    position: relative;
    max-width: 100%;
    margin: 0 auto;
    overflow: hidden;
    border-radius: 6px;
}
.puzzle-background {
    display: block;
    width: 100%;
    height: 100%;
    object-fit: cover;
}
.puzzle-piece {
    position: absolute;
    height: auto;
    pointer-events: none;
    filter: drop-shadow(0 2px 3px rgb(0 0 0 / 35%));
}
.puzzle-actions {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 12px;
}
.puzzle-status {
    color: var(--art-gray-500);
    font-size: 13px;
}
.puzzle-slider {
    height: 34px;
}
.puzzle-slider :deep(.el-slider__runway) {
    width: calc(100% - 44px);
    height: 10px;
    margin-right: 22px;
    margin-left: 22px;
}
.puzzle-slider :deep(.el-slider__bar) {
    height: 10px;
}
.puzzle-slider :deep(.el-slider__button-wrapper) {
    top: -13px;
    width: 44px;
    height: 36px;
}
.puzzle-slider :deep(.el-slider__button) {
    position: relative;
    width: 42px;
    height: 30px;
    border-radius: 4px;
}
.puzzle-slider :deep(.el-slider__button::after) {
    content: '→';
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    color: var(--el-color-primary);
    font-size: 20px;
    line-height: 1;
}
.verification-row {
    display: flex;
    width: 100%;
    gap: 10px;
}
.verification-row .el-input {
    min-width: 0;
}
.verification-button {
    width: 112px;
    height: 40px;
    flex-shrink: 0;
}
.password-field {
    width: 100%;
}
.password-strength {
    display: flex;
    align-items: center;
    margin-top: 8px;
    gap: 10px;
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

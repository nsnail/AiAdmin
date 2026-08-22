<!-- 登录页面 -->
<template>
    <div class="flex w-full h-screen">
        <LoginLeftView />

        <div class="relative flex-1">
            <AuthTopBar />

            <div class="auth-right-wrap">
                <div class="form">
                    <h3 class="title">{{ $t('login.title') }}</h3>
                    <p class="sub-title">{{ $t('login.subTitle') }}</p>
                    <ElForm :key="formKey" :model="formData" :rules="rules" @keyup.enter="handleSubmit" ref="formRef" style="margin-top: 25px">
                        <ElFormItem prop="username">
                            <ElInput v-model.trim="formData.username" :placeholder="$t('login.placeholder.username')" class="custom-height" />
                        </ElFormItem>
                        <ElFormItem prop="password">
                            <ElInput
                                v-model.trim="formData.password"
                                :placeholder="$t('login.placeholder.password')"
                                autocomplete="off"
                                class="custom-height"
                                show-password
                                type="password" />
                        </ElFormItem>

                        <!-- 推拽验证 -->
                        <div v-if="loginSliderVerification" class="relative pb-5 mt-6">
                            <div
                                :class="{ '!border-[#FF4E4F]': !isPassing && isClickPass }"
                                class="relative z-[2] overflow-hidden select-none rounded-lg border border-transparent tad-300">
                                <ArtDragVerify
                                    v-model:value="isPassing"
                                    :background="isDark ? '#26272F' : '#F1F1F4'"
                                    :successText="$t('login.sliderSuccessText')"
                                    :text="$t('login.sliderText')"
                                    handlerBg="var(--default-box-color)"
                                    progressBarBg="var(--main-color)"
                                    ref="dragVerify"
                                    textColor="var(--art-gray-700)" />
                            </div>
                            <p
                                :class="{ 'translate-y-10': !isPassing && isClickPass }"
                                class="absolute top-0 z-[1] px-px mt-2 text-xs text-[#f56c6c] tad-300">
                                {{ $t('login.placeholder.slider') }}
                            </p>
                        </div>

                        <div class="flex-cb mt-2 text-sm">
                            <ElCheckbox v-model="formData.rememberPassword">{{ $t('login.rememberPwd') }}</ElCheckbox>
                            <RouterLink :to="{ name: 'ForgetPassword' }" class="text-theme">{{ $t('login.forgetPwd') }}</RouterLink>
                        </div>

                        <div style="margin-top: 30px">
                            <ElButton v-ripple :loading="loading" @click="handleSubmit" class="w-full custom-height" type="primary">
                                {{ $t('login.btnText') }}
                            </ElButton>
                        </div>

                        <div v-if="registrationEnabled" class="mt-5 text-sm text-gray-600">
                            <span>{{ $t('login.noAccount') }}</span>
                            <RouterLink :to="{ name: 'Register' }" class="text-theme"> {{ $t('login.register') }}</RouterLink>
                        </div>
                    </ElForm>
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { useUserStore } from '@/store/modules/user'
import { useI18n } from 'vue-i18n'
import { HttpError } from '@/utils/http/error'
import { fetchLogin, fetchLoginChallenge, fetchLoginConfig } from '@/api/auth'
import { ElNotification, type FormInstance, type FormRules } from 'element-plus'
import { useSettingStore } from '@/store/modules/setting'

defineOptions({ name: 'Login' })

const settingStore = useSettingStore()
const { isDark } = storeToRefs(settingStore)
const { t, locale } = useI18n()
const formKey = ref(0)

// 监听语言切换，重置表单
watch(locale, () => {
    formKey.value++
})

const dragVerify = ref()

const userStore = useUserStore()
const router = useRouter()
const route = useRoute()
const isPassing = ref(false)
const loginSliderVerification = ref(true)
const registrationEnabled = ref(true)
const loginChallenge = ref<{ challenge: string; difficulty: number }>()
const loginProof = ref('')
const isClickPass = ref(false)

const formRef = ref<FormInstance>()
const REMEMBER_LOGIN_KEY = 'aiadmin-remember-login'

const formData = reactive({
    username: '',
    password: '',
    rememberPassword: true,
})

const rules = computed<FormRules>(() => ({
    username: [{ required: true, message: t('login.placeholder.username'), trigger: 'blur' }],
    password: [{ required: true, message: t('login.placeholder.password'), trigger: 'blur' }],
}))

const loading = ref(false)

// 登录
const handleSubmit = async () => {
    if (!formRef.value) return

    try {
        // 表单验证
        const valid = await formRef.value.validate()
        if (!valid) return

        // 拖拽验证
        if (loginSliderVerification.value && !isPassing.value) {
            isClickPass.value = true
            return
        }

        loading.value = true

        // 登录请求
        const { username, password } = formData
        if (!loginChallenge.value) return
        if (!loginProof.value) loginProof.value = await solveProof(loginChallenge.value.challenge, loginChallenge.value.difficulty)

        const { token, refreshToken, previousLogin } = await fetchLogin({
            userName: username,
            password,
            challenge: loginChallenge.value.challenge,
            proof: loginProof.value,
            clientInfo: collectClientInfo(),
        })

        // 验证token
        if (!token) {
            throw new Error('Login failed - no token received')
        }

        // 存储 token 和登录状态
        userStore.setToken(token, refreshToken)
        userStore.setLoginStatus(true)
        if (formData.rememberPassword) {
            localStorage.setItem(REMEMBER_LOGIN_KEY, JSON.stringify({ username, password }))
        } else {
            localStorage.removeItem(REMEMBER_LOGIN_KEY)
        }

        // 登录成功处理
        showLoginSuccessNotice(username, previousLogin)

        // 获取 redirect 参数，如果存在则跳转到指定页面，否则跳转到首页
        const redirect = route.query.redirect as string
        // 登录页不能作为登录成功后的目标，否则会不断嵌套 redirect 参数
        const target = redirect && !redirect.startsWith('/auth/login') ? redirect : '/'
        router.push(target)
    } catch (error) {
        // 处理 HttpError
        if (error instanceof HttpError) {
            if (error.message === t('login.proofExpired')) await refreshLoginChallenge()
        } else {
            // 处理非 HttpError
            // ElMessage.error('登录失败，请稍后重试')
            console.error('[Login] Unexpected error:', error)
        }
    } finally {
        loading.value = false
        resetDragVerify()
    }
}

// 重置拖拽验证
const resetDragVerify = () => {
    dragVerify.value.reset()
}

onMounted(async () => {
    const remembered = localStorage.getItem(REMEMBER_LOGIN_KEY)
    if (remembered) {
        try {
            const credentials = JSON.parse(remembered) as { username?: string; password?: string }
            formData.username = credentials.username ?? ''
            formData.password = credentials.password ?? ''
        } catch {
            localStorage.removeItem(REMEMBER_LOGIN_KEY)
        }
    }
    try {
        const config = await fetchLoginConfig()
        loginSliderVerification.value = config.loginSliderVerification
        registrationEnabled.value = config.registrationEnabled
        loginChallenge.value = await fetchLoginChallenge()
    } catch {
        loginSliderVerification.value = true
    }
})

const refreshLoginChallenge = async () => {
    loginChallenge.value = await fetchLoginChallenge()
    loginProof.value = ''
    isPassing.value = false
    isClickPass.value = false
}

const solveProof = async (challenge: string, difficulty: number) => {
    const prefix = '0'.repeat(difficulty)
    let nonce = 0
    while (true) {
        const bytes = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(`${challenge}:${nonce}`))
        const hash = Array.from(new Uint8Array(bytes), (byte) => byte.toString(16).padStart(2, '0')).join('')
        if (hash.startsWith(prefix)) return String(nonce)
        nonce++
        if (nonce % 1000 === 0) await new Promise((resolve) => setTimeout(resolve, 0))
    }
}

// 登录成功提示
const collectClientInfo = (): Api.Auth.LoginClientInfo => {
    const ua = navigator.userAgent
    const nav = navigator as Navigator & {
        userAgentData?: {
            brands?: Array<{ brand: string; version: string }>
            mobile?: boolean
            platform?: string
            model?: string
        }
    }
    const userAgentData = nav.userAgentData
    const browser = userAgentData?.brands?.find((item) => !/Not.?A.?Brand/i.test(item.brand))
    const operatingSystem =
        userAgentData?.platform ||
        (/Windows NT/i.test(ua)
            ? 'Windows'
            : /Mac OS X/i.test(ua)
              ? 'macOS'
              : /Android/i.test(ua)
                ? 'Android'
                : /Linux/i.test(ua)
                  ? 'Linux'
                  : /iPhone|iPad/i.test(ua)
                    ? 'iOS'
                    : 'Unknown')
    const isMobile = userAgentData?.mobile ?? /Android|iPhone|Mobile/i.test(ua)
    const isTablet = /iPad|Tablet/i.test(ua) || (isMobile && Math.min(screen.width, screen.height) >= 600)
    return {
        browser: browser
            ? `${browser.brand} ${browser.version}`
            : /Edg\//.test(ua)
              ? 'Edge'
              : /Chrome\//.test(ua)
                ? 'Chrome'
                : /Firefox\//.test(ua)
                  ? 'Firefox'
                  : /Safari\//.test(ua)
                    ? 'Safari'
                    : 'Unknown',
        clientHints: userAgentData ? JSON.stringify(userAgentData) : '',
        colorDepth: screen.colorDepth,
        deviceType: isTablet ? 'Tablet' : isMobile ? 'Mobile' : 'Desktop',
        language: navigator.language,
        operatingSystem,
        platform: userAgentData?.platform || navigator.platform,
        pixelRatio: window.devicePixelRatio,
        screenResolution: `${screen.width}x${screen.height}`,
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
        touchPoints: navigator.maxTouchPoints,
        viewportSize: `${window.innerWidth}x${window.innerHeight}`,
    }
}

const showLoginSuccessNotice = (username: string, previousLogin?: Api.Auth.LoginResponse['previousLogin']) => {
    setTimeout(() => {
        const detail = previousLogin
            ? t('login.success.previousLogin', {
                  ip: previousLogin.clientIp,
                  region: previousLogin.region || t('login.success.unknownRegion'),
                  time: new Date(previousLogin.loginAt).toLocaleString(),
              })
            : t('login.success.firstLogin')
        ElNotification({
            title: t('login.success.title'),
            type: 'success',
            duration: 2500,
            zIndex: 10000,
            message: `${t('login.success.message')}, ${username}! ${detail}`,
        })
    }, 1000)
}
</script>

<style scoped>
@import './style.css';
</style>

<style lang="scss" scoped>
:deep(.el-select__wrapper) {
    height: 40px !important;
}
</style>
<template>
    <div class="flex w-full h-screen">
        <LoginLeftView />
        <div class="relative flex-1">
            <AuthTopBar />
            <div class="auth-right-wrap">
                <div class="form">
                    <h3 class="title">{{ $t('forgetPassword.title') }}</h3>
                    <p class="sub-title">{{ $t('forgetPassword.subTitle') }}</p>
                    <ElForm :model="form" :rules="rules" class="mt-5" ref="formRef">
                        <ElFormItem prop="email"
                            ><ElInput v-model.trim="form.email" :placeholder="$t('forgetPassword.emailPlaceholder')" class="custom-height"
                        /></ElFormItem>
                        <ElFormItem prop="verificationCode">
                            <div class="flex w-full gap-2">
                                <ElInput
                                    v-model.trim="form.verificationCode"
                                    :placeholder="$t('forgetPassword.codePlaceholder')"
                                    class="custom-height" />
                                <ElButton :disabled="cooldown > 0" :loading="codeLoading" @click="sendCode" class="code-button custom-height">{{
                                    cooldown > 0 ? `${cooldown}s` : $t('forgetPassword.sendCode')
                                }}</ElButton>
                            </div>
                        </ElFormItem>
                        <ElFormItem prop="password"
                            ><ElInput
                                v-model.trim="form.password"
                                :placeholder="$t('forgetPassword.passwordPlaceholder')"
                                class="custom-height"
                                show-password
                                type="password"
                        /></ElFormItem>
                        <ElFormItem prop="confirmPassword"
                            ><ElInput
                                v-model.trim="form.confirmPassword"
                                :placeholder="$t('forgetPassword.confirmPasswordPlaceholder')"
                                class="custom-height"
                                show-password
                                type="password"
                        /></ElFormItem>
                        <ElButton v-ripple :loading="loading" @click="resetPassword" class="w-full custom-height" type="primary">{{
                            $t('forgetPassword.submitBtnText')
                        }}</ElButton>
                    </ElForm>
                    <div style="margin-top: 15px">
                        <ElButton @click="toLogin" class="w-full custom-height" plain>{{ $t('forgetPassword.backBtnText') }}</ElButton>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import type { FormInstance, FormRules } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { fetchForgotPasswordCode, fetchResetPassword } from '@/api/auth'

defineOptions({ name: 'ForgetPassword' })

const { t } = useI18n()
const router = useRouter()
const formRef = ref<FormInstance>()
const form = reactive({ email: '', verificationCode: '', password: '', confirmPassword: '' })
const loading = ref(false)
const codeLoading = ref(false)
const cooldown = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

const rules = computed<FormRules>(() => ({
    email: [{ required: true, type: 'email', message: t('forgetPassword.emailInvalid'), trigger: 'blur' }],
    verificationCode: [{ required: true, message: t('forgetPassword.codeRequired'), trigger: 'blur' }],
    password: [{ required: true, min: 8, pattern: /^(?=.*[A-Za-z])(?=.*\d).{8,}$/, message: t('forgetPassword.passwordRule'), trigger: 'blur' }],
    confirmPassword: [
        {
            required: true,
            validator: (_rule, value, callback) => callback(value === form.password ? undefined : new Error(t('forgetPassword.passwordMismatch'))),
            trigger: 'blur',
        },
    ],
}))

const sendCode = async () => {
    await formRef.value?.validateField('email')
    codeLoading.value = true
    try {
        await fetchForgotPasswordCode(form.email)
        ElMessage.success(t('forgetPassword.codeSent'))
        cooldown.value = 60
        timer = setInterval(() => {
            cooldown.value -= 1
            if (cooldown.value <= 0 && timer) clearInterval(timer)
        }, 1000)
    } finally {
        codeLoading.value = false
    }
}

const resetPassword = async () => {
    if (!formRef.value || !(await formRef.value.validate().catch(() => false))) return
    loading.value = true
    try {
        await fetchResetPassword({ email: form.email, verificationCode: form.verificationCode, password: form.password })
        ElMessage.success(t('forgetPassword.success'))
        toLogin()
    } finally {
        loading.value = false
    }
}

const toLogin = () => router.push({ name: 'Login' })
</script>

<style scoped>
@import '../login/style.css';
.code-button {
    min-width: 110px;
}
</style>

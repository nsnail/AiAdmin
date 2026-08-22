<template>
    <div class="flex w-full h-screen">
        <LoginLeftView />

        <div class="relative flex-1">
            <AuthTopBar />

            <div class="auth-right-wrap">
                <div class="form">
                    <h3 class="title">{{ $t('forgetPassword.title') }}</h3>
                    <p class="sub-title">{{ $t('forgetPassword.subTitle') }}</p>
                    <div class="mt-5">
                        <span v-if="showInputLabel" class="input-label">账号</span>
                        <ElInput v-model.trim="username" :placeholder="$t('forgetPassword.placeholder')" class="custom-height" />
                    </div>

                    <div style="margin-top: 15px">
                        <ElButton v-ripple :loading="loading" @click="register" class="w-full custom-height" type="primary">
                            {{ $t('forgetPassword.submitBtnText') }}
                        </ElButton>
                    </div>

                    <div style="margin-top: 15px">
                        <ElButton @click="toLogin" class="w-full custom-height" plain>
                            {{ $t('forgetPassword.backBtnText') }}
                        </ElButton>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'ForgetPassword' })

const router = useRouter()
const showInputLabel = ref(false)

const username = ref('')
const loading = ref(false)

const register = async () => {}

const toLogin = () => {
    router.push({ name: 'Login' })
}
</script>

<style scoped>
@import '../login/style.css';
</style>
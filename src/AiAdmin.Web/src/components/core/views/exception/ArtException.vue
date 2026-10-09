<template>
    <div class="page-content !border-0 !bg-transparent min-h-screen flex-cc">
        <div class="flex-cc max-md:!block max-md:text-center">
            <ThemeSvg :src="data.imgUrl" class="!w-100" size="100%" />
            <div class="ml-15 w-75 max-md:mx-auto max-md:mt-10 max-md:w-full max-md:text-center">
                <p class="text-xl leading-7 text-g-600 max-md:text-lg">{{ data.desc }}</p>
                <p class="mt-3 text-sm text-g-500">{{ $t('exceptionPage.autoRedirect', { seconds: countdown }) }}</p>
                <ElButton v-ripple @click="backHome" class="mt-5" size="large" type="primary">{{ data.btnText }}</ElButton>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { useCommon } from '@/hooks/core/useCommon'
import { useUserStore } from '@/store/modules/user'

const router = useRouter()
const userStore = useUserStore()
const countdown = ref(5)
let countdownTimer: ReturnType<typeof setInterval> | undefined

interface ExceptionData {
    /** 标题 */
    title: string
    /** 描述 */
    desc: string
    /** 按钮文本 */
    btnText: string
    /** 图片地址 */
    imgUrl: string
}

withDefaults(
    defineProps<{
        data: ExceptionData
    }>(),
    {},
)

const { homePath } = useCommon()

const backHome = () => {
    if (countdownTimer) {
        clearInterval(countdownTimer)
        countdownTimer = undefined
    }

    const targetHomePath = homePath.value || '/'

    if (!userStore.isLogin) {
        router.push({
            name: 'Login',
            query: { redirect: targetHomePath },
        })
        return
    }

    router.push(targetHomePath)
}

onMounted(() => {
    countdownTimer = setInterval(() => {
        countdown.value -= 1

        if (countdown.value === 0) {
            backHome()
        }
    }, 1000)
})

onUnmounted(() => {
    if (countdownTimer) {
        clearInterval(countdownTimer)
    }
})
</script>
<!-- 锁屏：仅负责解锁遮罩，令牌的移走与还原见 utils/lock -->
<template>
    <div v-if="isScreenLocked" @contextmenu.prevent class="unlock-content">
        <div class="flex-c flex-col w-80">
            <ElAvatar :size="64" :src="userInfo.avatar" aria-label="avatar" class="mt-5">
                <ArtSvgIcon icon="ri:user-line" />
            </ElAvatar>
            <div class="mt-3 mb-3.5 text-base font-medium">{{ userInfo.userName }}</div>
            <ElForm :model="form" :rules="rules" @submit.prevent="handleUnlock" class="w-full !px-2.5" ref="formRef">
                <ElFormItem prop="password">
                    <ElInput
                        v-model="form.password"
                        :placeholder="$t('lockScreen.unlock.inputPlaceholder')"
                        :show-password="true"
                        autocomplete="new-password"
                        class="mt-5"
                        ref="inputRef"
                        type="password">
                        <template #suffix>
                            <ElIcon @click="handleUnlock" class="c-p">
                                <Unlock />
                            </ElIcon>
                        </template>
                    </ElInput>
                </ElFormItem>
                <ElButton v-ripple :loading="submitting" @click="handleUnlock" class="w-full mt-2" type="primary">
                    {{ $t('lockScreen.unlock.btnText') }}
                </ElButton>
                <div class="w-full text-center">
                    <ElButton @click="abandonLock" class="mt-2.5 !text-g-600 hover:!text-theme hover:!bg-transparent" text>
                        {{ $t('lockScreen.unlock.backBtnText') }}
                    </ElButton>
                </div>
            </ElForm>
        </div>
    </div>
</template>

<script lang="ts" setup>
import { Unlock } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { useI18n } from 'vue-i18n'
import { useUserStore } from '@/store/modules/user'
import { abandonLock, isScreenLocked, unlockScreen } from '@/utils/lock'

defineOptions({ name: 'ArtScreenLock' })

const { t } = useI18n()
const { info: userInfo } = storeToRefs(useUserStore())
const formRef = ref<FormInstance>()
const inputRef = ref<{ input?: HTMLInputElement }>()
const submitting = ref(false)
const form = reactive({ password: '' })
const rules = computed<FormRules>(() => ({
    password: [{ required: true, message: t('lockScreen.unlock.inputPlaceholder'), trigger: 'blur' }],
}))

/** 需要拦截的键盘事件类型 */
const KEY_EVENTS = ['keydown', 'keyup', 'keypress'] as const

/**
 * 锁定期间拦截所有按键：只放行解锁框内的普通输入、回车和粘贴，其余按键与组合键一律屏蔽
 * @param event 键盘事件
 */
const blockKeys = (event: KeyboardEvent): void => {
    if (!isScreenLocked.value) return
    const inside = !!(event.target as HTMLElement | null)?.closest('.unlock-content')
    const isPaste = (event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === 'v'
    const isCombo = (event.ctrlKey || event.metaKey || event.altKey) && !isPaste
    if (inside && !isCombo && !/^F\d{1,2}$/.test(event.key)) return
    event.preventDefault()
    event.stopImmediatePropagation()
}

/** 校验解锁密码，错误时清空输入并提示 */
const handleUnlock = async (): Promise<void> => {
    if (!formRef.value || submitting.value) return
    if (!(await formRef.value.validate().catch(() => false))) return
    submitting.value = true
    try {
        if (!(await unlockScreen(form.password))) ElMessage.error(t('lockScreen.pwdError'))
        form.password = ''
    } finally {
        submitting.value = false
    }
}

watch(isScreenLocked, (locked) => {
    document.body.style.overflow = locked ? 'hidden' : 'auto'
    if (locked) setTimeout(() => inputRef.value?.input?.focus(), 100)
})

onMounted(() => {
    KEY_EVENTS.forEach((type) => window.addEventListener(type, blockKeys, true))
})
onUnmounted(() => {
    KEY_EVENTS.forEach((type) => window.removeEventListener(type, blockKeys, true))
    document.body.style.overflow = 'auto'
})
</script>

<style lang="scss" scoped>
.unlock-content {
    position: fixed;
    inset: 0;
    z-index: 2500;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    background-color: #fff;
    background-image: url('@imgs/lock/bg_light.webp');
    background-size: cover;
}

.dark .unlock-content {
    background-image: url('@imgs/lock/bg_dark.webp');
}
</style>
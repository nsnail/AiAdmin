<template>
    <ElConfigProvider
        :card="{
            shadow: 'never',
        }"
        :locale="locales[language]"
        :z-index="3000"
        size="default">
        <RouterView></RouterView>
    </ElConfigProvider>
</template>

<script lang="ts" setup>
import { useUserStore } from './store/modules/user'
import zh from 'element-plus/es/locale/lang/zh-cn'
import en from 'element-plus/es/locale/lang/en'
import { systemUpgrade } from './utils/sys'
import { toggleTransition } from './utils/ui/animation'
import { checkStorageCompatibility } from './utils/storage'
import { initializeTheme } from './hooks/core/useTheme'

const userStore = useUserStore()
const { language } = storeToRefs(userStore)

const locales = {
    zh: zh,
    en: en,
}

onBeforeMount(() => {
    toggleTransition(true)
    initializeTheme()
})

onMounted(() => {
    checkStorageCompatibility()
    toggleTransition(false)
    systemUpgrade()
})
</script>
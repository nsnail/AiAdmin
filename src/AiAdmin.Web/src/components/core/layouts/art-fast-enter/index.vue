<!-- 顶部快速入口面板 -->
<template>
    <ElPopover
        :offset="0"
        :popper-style="{
            border: '1px solid var(--default-border)',
            borderRadius: 'calc(var(--custom-radius) / 2 + 4px)',
        }"
        :show-arrow="false"
        :width="700"
        placement="bottom-start"
        popper-class="fast-enter-popover"
        ref="popoverRef"
        trigger="hover">
        <template #reference>
            <div class="flex-c gap-2">
                <slot />
            </div>
        </template>

        <div class="grid grid-cols-[2fr_0.8fr]">
            <div>
                <div class="grid grid-cols-2 gap-1.5">
                    <!-- 应用列表 -->
                    <div
                        v-for="application in enabledApplications"
                        :key="application.name"
                        @click="handleApplicationClick(application)"
                        class="mr-3 c-p flex-c gap-3 rounded-lg p-2 hover:bg-g-200/70 dark:hover:bg-g-200/90 hover:[&_.app-icon]:!bg-transparent">
                        <div class="app-icon size-12 flex-cc rounded-lg bg-g-200/80 dark:bg-g-300/30">
                            <ArtSvgIcon :icon="application.icon" :style="{ color: application.iconColor }" class="text-xl" />
                        </div>
                        <div>
                            <h3 class="m-0 text-sm font-medium text-g-800">{{ application.nameKey ? t(application.nameKey) : application.name }}</h3>
                            <p class="mt-1 text-xs text-g-600">
                                {{ application.descriptionKey ? t(application.descriptionKey) : application.description }}
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            <div class="border-l-d pl-6 pt-2">
                <h3 class="mb-2.5 text-base font-medium text-g-800">{{ t('fastEnter.quickLinks') }}</h3>
                <ul>
                    <li
                        v-for="quickLink in enabledQuickLinks"
                        :key="quickLink.name"
                        @click="handleQuickLinkClick(quickLink)"
                        class="c-p py-2 hover:[&_span]:text-theme">
                        <span class="text-g-600 no-underline">{{ quickLink.nameKey ? t(quickLink.nameKey) : quickLink.name }}</span>
                    </li>
                </ul>
            </div>
        </div>
    </ElPopover>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { useFastEnter } from '@/hooks/core/useFastEnter'
import { mittBus } from '@/utils/sys'
import type { FastEnterApplication, FastEnterBaseItem, FastEnterQuickLink } from '@/types/config'

defineOptions({ name: 'ArtFastEnter' })

const router = useRouter()
const { t } = useI18n()
const popoverRef = ref()

// 使用快速入口配置
const { enabledApplications, enabledQuickLinks } = useFastEnter()

/**
 * 处理条目点击：有内置动作的先执行动作（如打开通知抽屉），否则按路由名称或外部链接跳转
 * @param item 快速入口条目
 */
const handleItemClick = (item: FastEnterBaseItem): void => {
    if (item.action) {
        mittBus.emit(item.action)
        popoverRef.value?.hide()
        return
    }

    handleNavigate(item.routeName, item.link)
}

/**
 * 处理导航跳转
 * @param routeName 路由名称
 * @param link 外部链接
 */
const handleNavigate = (routeName?: string, link?: string): void => {
    const targetPath = routeName || link

    if (!targetPath) {
        console.warn('导航配置无效：缺少路由名称或链接')
        return
    }

    if (targetPath.startsWith('http')) {
        window.open(targetPath, '_blank')
    } else {
        router.push({ name: targetPath })
    }

    popoverRef.value?.hide()
}

/**
 * 处理应用项点击
 * @param application 应用配置对象
 */
const handleApplicationClick = (application: FastEnterApplication): void => {
    handleItemClick(application)
}

/**
 * 处理快速链接点击
 * @param quickLink 快速链接配置对象
 */
const handleQuickLinkClick = (quickLink: FastEnterQuickLink): void => {
    handleItemClick(quickLink)
}
</script>
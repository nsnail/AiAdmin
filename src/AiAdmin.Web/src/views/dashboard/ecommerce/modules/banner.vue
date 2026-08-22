<template>
    <ArtBasicBanner
        :buttonConfig="{
            show: false,
            text: '',
        }"
        :decoration="false"
        :imageConfig="{
            src: bannerCover,
            width: '18rem',
            bottom: '-7.5rem',
        }"
        :meteorConfig="{
            enabled: true,
            count: 10,
        }"
        :title="`欢迎回来 ${userInfo.userName}`"
        @click="handleBannerClick"
        boxStyle="!bg-theme/10"
        class="justify-center !h-53 mb-5 max-sm:!pt-8 max-sm:!h-48 max-sm:mb-4"
        titleColor="var(--art-gray-900)">
        <div class="flex mt-6">
            <div class="mr-8 pr-8 border-r border-g-400 dark:border-g-300/60">
                <p class="text-3xl">
                    <ArtCountTo :duration="1500" :target="2340" class="number box-title" prefix="¥" separator="," />
                    <ArtSvgIcon class="text-xl text-success relative -top-2" icon="ri:arrow-right-up-line" />
                </p>
                <p class="mt-1 text-sm text-g-700">今日销售额</p>
            </div>
            <div class="mr-8">
                <p class="text-3xl">
                    <ArtCountTo :duration="1500" :target="35" class="number box-title" suffix="%" />
                    <ArtSvgIcon class="text-xl text-success relative -top-2" icon="ri:arrow-right-up-line" />
                </p>
                <p class="mt-1 text-sm text-g-700">较昨日</p>
            </div>
        </div>
    </ArtBasicBanner>
</template>

<script lang="ts" setup>
import bannerCover from '@imgs/login/lf_icon2.webp'
import { useUserStore } from '@/store/modules/user'

const userStore = useUserStore()

/**
 * 获取当前用户信息
 */
const userInfo = computed(() => userStore.getUserInfo)

/**
 * 处理横幅点击事件
 */
const handleBannerClick = (): void => {
    // TODO: 添加横幅点击处理逻辑
}
</script>
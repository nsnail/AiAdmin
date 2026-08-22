<template>
    <div class="space-y-5 mb-5">
        <div class="text-2xl font-medium mt-5 max-sm:text-2xl max-sm:mt-3">图标</div>
        <div class="text-g-800">
            v.3.0 版本图标库升级为 iconify，可在
            <a class="text-theme hover:underline" href="https://icones.js.org/" target="_blank">Iconify</a>
            中查找，支持多种图标库，如 Remix Icon, Solar, Tabler Icons 等。
        </div>
        <div class="text-g-800">
            为确保系统图标风格统一，项目全部采用 Remix Icon 图标库，可在
            <a class="text-theme hover:underline" href="https://icones.js.org/collection/ri" target="_blank">Iconify</a>
            或<a class="text-theme hover:underline" href="https://remixicon.com/" target="_blank"> Remix Icon 官网 </a>搜索使用。
        </div>

        <!-- Iconify 图标 -->
        <div class="art-card-sm p-5">
            <div class="text-lg font-semibold mb-4">Iconify</div>
            <div class="flex items-center gap-6">
                <ArtSvgIcon class="text-2xl" icon="ri:github-fill" />
                <ArtSvgIcon class="text-2xl text-theme" icon="ri:copilot-line" />
                <ArtSvgIcon class="text-2xl text-secondary" icon="ri:edge-line" />
                <ArtSvgIcon class="text-2xl text-warning" icon="ri:planet-line" />
                <ArtSvgIcon class="text-2xl text-info" icon="ri:windows-line" />
                <ArtSvgIcon class="text-2xl text-danger" icon="ri:thumb-up-line" />
                <ArtSvgIcon class="text-2xl text-success" icon="ri:gift-2-line" />
                <ArtSvgIcon class="text-2xl text-secondary" icon="ri:apple-line" />
            </div>
        </div>

        <!-- Svg Icons -->
        <div class="art-card-sm p-5">
            <div class="text-lg font-semibold mb-4">Svg Icons</div>
            <div class="flex items-center gap-6">
                <ArtSvgIcon class="text-2xl text-red-400" icon="svg-spinners:3-dots-fade" />
                <ArtSvgIcon class="text-2xl text-blue-400" icon="svg-spinners:3-dots-bounce" />
                <ArtSvgIcon class="text-2xl text-orange-400" icon="svg-spinners:3-dots-move" />
                <ArtSvgIcon class="text-2xl text-purple-400" icon="svg-spinners:3-dots-rotate" />
                <ArtSvgIcon class="text-2xl text-pink-300" icon="svg-spinners:blocks-shuffle-2" />
                <ArtSvgIcon class="text-2xl text-yellow-500" icon="svg-spinners:clock" />
                <ArtSvgIcon class="text-2xl text-orange-500" icon="svg-spinners:tadpole" />
                <ArtSvgIcon class="text-2xl text-blue-500" icon="svg-spinners:blocks-wave" />
            </div>
        </div>

        <!-- Svg Icons -->
        <div class="art-card-sm p-5">
            <div class="text-lg font-semibold mb-4">Material Line Icons</div>
            <div class="flex items-center gap-6">
                <ArtSvgIcon class="text-2xl text-blue-500" icon="line-md:phone-call-twotone-loop" />

                <ArtSvgIcon class="text-2xl text-green-500" icon="line-md:switch-off" />
                <ArtSvgIcon class="text-2xl text-yellow-400" icon="line-md:sun-rising-filled-loop" />
                <ArtSvgIcon class="text-2xl text-purple-500" icon="line-md:volume-high-filled" />
                <ArtSvgIcon class="text-2xl text-gray-700" icon="line-md:github-twotone" />
                <ArtSvgIcon class="text-2xl text-sky-500" icon="line-md:telegram" />
                <ArtSvgIcon class="text-2xl text-orange-400" icon="line-md:reddit-loop" />
                <ArtSvgIcon class="text-2xl text-emerald-500" icon="line-md:coffee-half-empty-filled-loop" />
            </div>
        </div>

        <!-- 使用示例 -->
        <div class="art-card-sm p-5">
            <div class="text-lg font-semibold mb-4">使用示例</div>
            <div class="space-y-4">
                <div>
                    <div class="text-sm text-g-600 mb-2">基础使用</div>
                    <div class="bg-g-200 dark:bg-g-300/30 p-4 rounded font-mono text-sm text-g-800">&lt;ArtSvgIcon icon="ri:home-line" /&gt;</div>
                </div>
                <div>
                    <div class="text-sm text-g-600 mb-2">自定义大小</div>
                    <div class="bg-g-200 dark:bg-g-300/30 p-4 rounded font-mono text-sm text-g-800">
                        &lt;ArtSvgIcon icon="ri:user-line" class="text-2xl" /&gt;
                    </div>
                </div>
                <div>
                    <div class="text-sm text-g-600 mb-2">自定义颜色</div>
                    <div class="bg-g-200 dark:bg-g-300/30 p-4 rounded font-mono text-sm text-g-800">
                        &lt;ArtSvgIcon icon="ri:heart-fill" class="text-red-500" /&gt;
                    </div>
                </div>
                <div>
                    <div class="text-sm text-g-600 mb-2">组合使用</div>
                    <div class="bg-g-200 dark:bg-g-300/30 p-4 rounded font-mono text-sm text-g-800">
                        &lt;ArtSvgIcon icon="ri:star-fill" class="text-4xl text-yellow-500" /&gt;
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'

defineOptions({ name: 'IconPage' })
</script>

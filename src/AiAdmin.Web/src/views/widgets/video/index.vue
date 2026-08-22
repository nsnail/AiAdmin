<template>
    <div class="page-content">
        <div class="max-w-150">
            <ArtVideoPlayer
                :autoplay="false"
                :playbackRates="[0.5, 1, 1.5, 2]"
                :posterUrl="posterUrl"
                :videoUrl="videoUrl"
                :volume="1"
                playerId="my-video-1" />
        </div>
    </div>
</template>

<script lang="ts" setup>
import lockImg from '@imgs/lock/bg_dark.webp'

defineOptions({ name: 'WidgetsVideo' })

/**
 * 视频源 URL
 */
const videoUrl = ref('//lf3-static.bytednsdoc.com/obj/eden-cn/nupenuvpxnuvo/xgplayer_doc/xgplayer-demo.mp4')

/**
 * 视频封面图片 URL
 */
const posterUrl = ref(lockImg)
</script>
<!-- 节日文本滚动 -->
<template>
    <div
        :style="{
            height: showFestivalText ? '48px' : '0',
        }"
        class="overflow-hidden transition-[height] duration-600 ease-in-out">
        <ArtTextScroll
            v-if="showFestivalText && currentFestivalData?.scrollText !== ''"
            :text="currentFestivalData?.scrollText || ''"
            @close="handleClose"
            showClose
            style="margin-bottom: 12px" />
    </div>
</template>

<script lang="ts" setup>
import { useSettingStore } from '@/store/modules/setting'
import { useCeremony } from '@/hooks/core/useCeremony'

defineOptions({ name: 'ArtFestivalTextScroll' })

const settingStore = useSettingStore()
const { showFestivalText } = storeToRefs(settingStore)
const { currentFestivalData } = useCeremony()

const handleClose = () => {
    settingStore.setShowFestivalText(false)
}
</script>
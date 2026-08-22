<!-- 返回顶部按钮 -->
<template>
    <Transition
        enter-active-class="tad-300 ease-out"
        enter-from-class="opacity-0 translate-y-2"
        enter-to-class="opacity-100 translate-y-0"
        leave-active-class="tad-200 ease-in"
        leave-from-class="opacity-100 translate-y-0"
        leave-to-class="opacity-0 translate-y-2">
        <div
            v-show="showButton"
            @click="scrollToTop"
            class="fixed right-10 bottom-15 size-9.5 flex-cc c-p border border-g-300 rounded-md tad-300 hover:bg-g-200">
            <ArtSvgIcon class="text-g-500 text-lg" icon="ri:arrow-up-wide-line" />
        </div>
    </Transition>
</template>

<script lang="ts" setup>
import { useCommon } from '@/hooks/core/useCommon'

defineOptions({ name: 'ArtBackToTop' })

const { scrollToTop } = useCommon()

const showButton = ref(false)
const scrollThreshold = 300

onMounted(() => {
    const scrollContainer = document.getElementById('app-main')
    if (scrollContainer) {
        const { y } = useScroll(scrollContainer)
        watch(y, (newY: number) => {
            showButton.value = newY > scrollThreshold
        })
    }
})
</script>
<!-- 布局内容 -->
<template>
    <div :class="{ 'overflow-auto': isFullPage }" :style="containerStyle" @contextmenu="handleContentContextMenu" class="layout-content">
        <div id="app-content-header">
            <!-- 节日滚动 -->
            <ArtFestivalTextScroll v-if="!isFullPage" />

            <!-- 路由信息调试 -->
            <div v-if="isOpenRouteInfo === 'true'" class="px-2 py-1.5 mb-3 text-sm text-g-500 bg-g-200 border-full-d rounded-md">
                router meta：{{ route.meta }}
            </div>
        </div>

        <RouterView v-if="isRefresh" v-slot="{ Component, route }" :style="contentStyle">
            <!-- 缓存路由动画 -->
            <Transition :name="showTransitionMask ? '' : actualTransition" appear mode="out-in">
                <KeepAlive :exclude="keepAliveExclude" :max="10">
                    <component v-if="route.meta.keepAlive" :is="Component" :key="route.path" class="art-page-view" />
                </KeepAlive>
            </Transition>

            <!-- 非缓存路由动画 -->
            <Transition :name="showTransitionMask ? '' : actualTransition" appear mode="out-in">
                <component v-if="!route.meta.keepAlive" :is="Component" :key="route.path" class="art-page-view" />
            </Transition>
        </RouterView>

        <!-- 全屏页面切换过渡遮罩（用于提升页面切换视觉体验） -->
        <Teleport to="body">
            <div v-show="showTransitionMask" class="fixed top-0 left-0 z-[2000] w-screen h-screen pointer-events-none bg-box" />
        </Teleport>
    </div>
</template>
<script lang="ts" setup>
import type { CSSProperties } from 'vue'
import { useRoute } from 'vue-router'
import { useAutoLayoutHeight } from '@/hooks/core/useLayoutHeight'
import { useSettingStore } from '@/store/modules/setting'
import { useWorktabStore } from '@/store/modules/worktab'
import { useAutoRefresh } from '@/hooks/core/useAutoRefresh'
import { openActiveTabMenu } from '@/hooks/core/useTabContextMenu'

defineOptions({ name: 'ArtPageContent' })

/**
 * 接管内容区的浏览器默认右键菜单，改为弹出当前标签页的右键菜单
 * 表格单元格、文件管理等已自行处理右键（已调用 preventDefault）的，以及输入框、文本域、可编辑区域保留原有行为，避免影响复制粘贴
 * @param e 右键事件
 */
const handleContentContextMenu = (e: MouseEvent): void => {
    if (e.defaultPrevented) return
    const target = e.target as HTMLElement | null
    if (target?.closest('input, textarea, [contenteditable=""], [contenteditable="true"]')) return
    openActiveTabMenu(e)
}

const route = useRoute()
const { containerMinHeight } = useAutoLayoutHeight()
const { pageTransition, containerWidth, refresh } = storeToRefs(useSettingStore())
const { keepAliveExclude } = storeToRefs(useWorktabStore())

const isRefresh = shallowRef(true)
const isOpenRouteInfo = import.meta.env.VITE_OPEN_ROUTE_INFO
const showTransitionMask = ref(false)

// 标记是否是首次加载（浏览器刷新）
const isFirstLoad = ref(true)

// 检查当前路由是否需要使用无基础布局模式
const isFullPage = computed(() => route.matched.some((r) => r.meta?.isFullPage))
const prevIsFullPage = ref(isFullPage.value)

// 定时刷新：倒计时与提示消息，全屏页面不启用
useAutoRefresh(
    computed(() => route.path),
    computed(() => !isFullPage.value),
)

// 切换动画名称：首次加载、从全屏返回时不使用动画
const actualTransition = computed(() => {
    if (isFirstLoad.value) return ''
    if (prevIsFullPage.value && !isFullPage.value) return ''
    return pageTransition.value
})

// 监听全屏状态变化，显示过渡遮罩
watch(isFullPage, (val, oldVal) => {
    if (val !== oldVal) {
        showTransitionMask.value = true
        // 延迟隐藏遮罩，给足时间让页面完成切换
        setTimeout(() => {
            showTransitionMask.value = false
        }, 50)
    }

    nextTick(() => {
        prevIsFullPage.value = val
    })
})

const containerStyle = computed((): CSSProperties =>
    isFullPage.value
        ? {
              position: 'fixed',
              top: 0,
              left: 0,
              width: '100%',
              height: '100vh',
              zIndex: 2500,
              background: 'var(--default-bg-color)',
          }
        : {
              maxWidth: containerWidth.value,
          },
)

const contentStyle = computed((): CSSProperties => ({
    minHeight: containerMinHeight.value,
}))

const reload = () => {
    isRefresh.value = false
    nextTick(() => {
        isRefresh.value = true
    })
}

watch(refresh, reload, { flush: 'post' })

// 组件挂载后标记首次加载完成
onMounted(() => {
    // 延迟一帧，确保首次渲染完成
    nextTick(() => {
        isFirstLoad.value = false
    })
})
</script>
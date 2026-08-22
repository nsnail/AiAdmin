<template>
    <div class="page-content space-y-5">
        <!-- 基础用法 -->
        <ArtTextScroll
            showClose
            text="Art Design Pro 是一款兼具设计美学与高效开发的后台系统 <a target='_blank' href='https://www.artd.pro/docs/'>点击我 </a>访问官方文档" />

        <!-- 使用不同的类型 -->
        <ArtTextScroll text="这是一条成功类型的滚动公告" type="success" />

        <ArtTextScroll text="这是一条警告类型的滚动公告" type="warning" />

        <ArtTextScroll text="这是一条危险类型的滚动公告" type="danger" />

        <ArtTextScroll text="这是一条信息类型的滚动公告" type="info" />

        <!-- 可关闭 -->
        <ArtTextScroll @close="handleClose" text="这是一条可关闭的滚动公告" />

        <!-- 自定义速度和方向 -->
        <ArtTextScroll :speed="30" direction="right" text="这是一条速度较慢、向右滚动的公告" type="warning" />

        <ArtTextScroll
            :alwaysScroll="false"
            @close="handleClose"
            text="这是一条文字溢出才会滚动的公告，当文本内容超出容器宽度时才会开始滚动显示，否则保持静止状态" />

        <ArtTextScroll :speed="30" direction="up" text="这是一条向上滚动的公告" type="danger" />

        <ArtTextScroll :speed="30" direction="down" text="这是一条向下滚动的公告" type="info" />
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'WidgetsTextScroll' })

/**
 * 处理关闭事件
 */
const handleClose = () => {
    console.log('文本滚动组件已关闭')
    ElMessage.info('已关闭')
}
</script>
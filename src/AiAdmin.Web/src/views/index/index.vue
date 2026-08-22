<!-- 布局容器 -->
<template>
    <div class="app-layout">
        <aside id="app-sidebar">
            <ArtSidebarMenu />
        </aside>

        <main id="app-main">
            <div id="app-header">
                <ArtHeaderBar />
            </div>
            <div id="app-content">
                <ArtPageContent />
            </div>
        </main>

        <div id="app-global">
            <ArtGlobalComponent />
        </div>
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'AppLayout' })
</script>

<style lang="scss" scoped>
@use './style';
</style>

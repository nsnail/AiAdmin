<!-- 系统logo -->
<template>
    <div class="flex-cc">
        <img :style="logoStyle" alt="logo" class="w-full h-full" src="@imgs/common/logo.webp" />
    </div>
</template>

<script lang="ts" setup>
defineOptions({ name: 'ArtLogo' })

interface Props {
    /** logo 大小 */
    size?: number | string
}

const props = withDefaults(defineProps<Props>(), {
    size: 36,
})

const logoStyle = computed(() => ({ width: `${props.size}px` }))
</script>
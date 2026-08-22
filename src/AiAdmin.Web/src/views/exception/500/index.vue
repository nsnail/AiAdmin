<!-- 500页面 -->
<template>
    <ArtException
        :data="{
            title: '500',
            desc: $t('exceptionPage.500'),
            btnText: $t('exceptionPage.gohome'),
            imgUrl,
        }" />
</template>

<script lang="ts" setup>
import imgUrl from '@imgs/svg/500.svg'
defineOptions({ name: 'Exception500' })
</script>
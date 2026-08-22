<!-- 404页面 -->
<template>
    <ArtException
        :data="{
            title: '404',
            desc: $t('exceptionPage.404'),
            btnText: $t('exceptionPage.gohome'),
            imgUrl,
        }" />
</template>

<script lang="ts" setup>
import imgUrl from '@imgs/svg/404.svg'
defineOptions({ name: 'Exception404' })
</script>
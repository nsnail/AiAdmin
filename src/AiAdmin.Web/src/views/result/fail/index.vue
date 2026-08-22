<template>
    <ArtResultPage iconCode="ri:close-fill" message="请核对并修改以下信息后，再重新提交。" title="提交失败" type="fail">
        <template #content>
            <p>您提交的内容有如下错误：</p>
            <p>
                <ArtSvgIcon class="text-red-500 mr-1" icon="ri:close-circle-line" />
                <span>您的账户已被冻结</span>
            </p>
            <p>
                <ArtSvgIcon class="text-red-500 mr-1" icon="ri:close-circle-line" />
                <span>您的账户还不具备申请资格</span>
            </p>
        </template>
        <template #buttons>
            <ElButton v-ripple type="primary">返回修改</ElButton>
            <ElButton v-ripple>查看</ElButton>
        </template>
    </ArtResultPage>
</template>

<script lang="ts" setup>
defineOptions({ name: 'ResultFail' })
</script>

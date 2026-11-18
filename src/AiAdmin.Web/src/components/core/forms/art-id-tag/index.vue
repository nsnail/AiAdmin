<!-- 编号标签：列表和详情里展示设备编号、账号编号等业务编号的统一控件。
     颜色按编号文字 Hash 取同色系（与项目里其他动态标签一致）；
     传入 clickable 时鼠标显示手型，点击触发 click 事件，由使用方决定跳转 -->
<template>
    <ElTag
        :class="{ 'art-id-tag--clickable': clickable }"
        :style="getHashTagStyle(String(value))"
        @click.stop="clickable && emit('click')"
        class="art-id-tag"
        effect="light"
        size="small"
        >{{ value }}</ElTag
    >
</template>

<script lang="ts" setup>
import { ElTag } from 'element-plus'
import { getHashTagStyle } from '@/utils/tagColor'

defineOptions({ name: 'ArtIdTag' })

withDefaults(
    defineProps<{
        /** 编号，如设备编号、账号编号 */
        value: string | number
        /** 是否可点击，可点击时鼠标显示手型 */
        clickable?: boolean
    }>(),
    { clickable: false },
)

const emit = defineEmits<{ (e: 'click'): void }>()
</script>

<style scoped>
.art-id-tag {
    max-width: 100%;
}

.art-id-tag--clickable {
    cursor: pointer;
}

.art-id-tag--clickable:hover {
    opacity: 0.8;
}
</style>
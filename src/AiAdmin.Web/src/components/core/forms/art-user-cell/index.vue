<!-- 用户头像 + 用户名：列表里展示用户（如回调所属商家）的统一控件。
     可选链接：传入 link 时用户名渲染成链接，点击触发 click 事件，由使用方决定跳转；
     用户名缺失（用户已删除）时回退显示 fallback（通常是用户编号） -->
<template>
    <div class="art-user-cell">
        <ArtUserAvatar :name="displayName" :size="size" :src="avatar" class="art-user-cell__avatar" />
        <ElLink v-if="link" @click.stop="emit('click')" class="art-user-cell__name" type="primary" underline="never">{{ displayName }}</ElLink>
        <span v-else class="art-user-cell__name">{{ displayName }}</span>
    </div>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { ElLink } from 'element-plus'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'

defineOptions({ name: 'ArtUserCell' })

const props = withDefaults(
    defineProps<{
        /** 用户名 */
        name?: string | null
        /** 头像地址，为空时显示用户名首字母 */
        avatar?: string | null
        /** 用户名缺失时的回退文本，如用户编号 */
        fallback?: string | number | null
        /** 头像尺寸，单位像素 */
        size?: number
        /** 用户名是否渲染成链接 */
        link?: boolean
    }>(),
    { name: undefined, avatar: undefined, fallback: undefined, size: 24, link: false },
)

const emit = defineEmits<{ (e: 'click'): void }>()

/** 展示用的名称：优先用户名，其次回退文本，都没有时显示 - */
const displayName = computed(() => props.name?.trim() || (props.fallback !== undefined && props.fallback !== null ? String(props.fallback) : '-'))
</script>

<style scoped>
.art-user-cell {
    display: flex;
    align-items: center;
    gap: 8px;
    min-width: 0;
}

.art-user-cell__avatar {
    flex-shrink: 0;
}

.art-user-cell__name {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
</style>
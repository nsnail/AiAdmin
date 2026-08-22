<template>
    <li>
        <div>
            <div class="flex-c">
                <div :style="{ background: randomColor() }" class="size-5 mr-2.5 text-xs font-medium text-white rounded-full flex-cc">
                    {{ comment.author.substring(0, 1) }}
                </div>
                <strong class="block text-sm font-medium">{{ comment.author }}</strong>
            </div>
            <span class="block mt-2.5 text-sm text-g-700">{{ comment.content }}</span>
            <div class="flex-c mt-2.5">
                <span class="text-xs text-g-700">{{ formatDate(comment.timestamp) }}</span>
                <div @click="toggleReply(comment.id)" class="ml-5 text-xs text-g-700 c-p select-none hover:text-theme">回复</div>
            </div>
        </div>

        <ul v-if="comment.replies.length > 0" class="pl-2.5">
            <CommentItem
                v-for="reply in comment.replies"
                :comment="reply"
                :key="reply.id"
                :show-reply-form="showReplyForm"
                @add-reply="addReply"
                @toggle-reply="toggleReply"
                class="mt-5" />
        </ul>

        <ElForm v-if="showReplyForm === comment.id" @submit.prevent="handleSubmit" class="mt-4">
            <ElFormItem prop="author">
                <ElInput v-model="replyAuthor" clearable placeholder="你的名称" />
            </ElFormItem>
            <ElFormItem prop="content">
                <ElInput v-model="replyContent" :rows="3" clearable placeholder="你的回复..." type="textarea" />
            </ElFormItem>
            <ElFormItem>
                <div class="flex justify-end gap-2 w-full">
                    <ElButton @click="toggleReply(comment.id)">取消</ElButton>
                    <ElButton @click="handleSubmit" type="primary">发布</ElButton>
                </div>
            </ElFormItem>
        </ElForm>
    </li>
</template>

<script lang="ts" setup>
import AppConfig from '@/config'
import { ref } from 'vue'

interface Comment {
    id: number
    author: string
    content: string
    timestamp: string
    replies: Comment[]
}

const props = defineProps<{
    comment: Comment
    showReplyForm: number | null
}>()

const emit = defineEmits<{
    (event: 'toggle-reply', commentId: number): void
    (event: 'add-reply', commentId: number, replyAuthor: string, replyContent: string): void
}>()

const replyAuthor = ref('')
const replyContent = ref('')

const toggleReply = (commentId: number) => {
    emit('toggle-reply', commentId)
}

const addReply = (commentId: number, author: string, content: string) => {
    emit('add-reply', commentId, author, content)
    replyAuthor.value = ''
    replyContent.value = ''
}
const handleSubmit = () => {
    if (!replyAuthor.value.trim() || !replyContent.value.trim()) {
        return
    }
    emit('add-reply', props.comment.id, replyAuthor.value, replyContent.value)
    replyAuthor.value = ''
    replyContent.value = ''
}

const formatDate = (timestamp: string) => {
    const date = new Date(timestamp)
    return date.toLocaleString()
}

let lastColor: string | null = null

const randomColor = () => {
    let newColor: string

    do {
        const index = Math.floor(Math.random() * AppConfig.systemMainColor.length)
        newColor = AppConfig.systemMainColor[index]
    } while (newColor === lastColor)

    lastColor = newColor
    return newColor
}
</script>

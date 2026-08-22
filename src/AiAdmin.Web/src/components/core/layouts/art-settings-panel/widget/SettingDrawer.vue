<template>
    <div class="setting-drawer">
        <ElDrawer
            v-model="visible"
            :before-close="handleClose"
            :destroy-on-close="false"
            :lock-scroll="true"
            :with-header="false"
            @close="handleDrawerClose"
            @open="handleOpen"
            modal-class="setting-modal"
            size="300px">
            <div class="drawer-con">
                <slot />
            </div>
        </ElDrawer>
    </div>
</template>

<script lang="ts" setup>
interface Props {
    modelValue: boolean
}

interface Emits {
    (e: 'update:modelValue', value: boolean): void
    (e: 'open'): void
    (e: 'close'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const visible = computed({
    get: () => props.modelValue,
    set: (value: boolean) => emit('update:modelValue', value),
})

const handleOpen = () => {
    emit('open')
}

const handleDrawerClose = () => {
    emit('close')
}

const handleClose = () => {
    visible.value = false
}
</script>
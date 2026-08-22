<template>
    <div>
        <div class="flex justify-end">
            <div @click="$emit('close')" class="flex-cc c-p size-7.5 !transition-all duration-200 rounded hover:bg-g-300/80">
                <ArtSvgIcon class="block text-xl text-g-600" icon="ri:close-fill" />
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
defineEmits<{
    close: []
}>()
</script>
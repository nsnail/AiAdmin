<template>
    <div class="list-id-cell">
        <span :data-query-value="id" class="list-id" data-query-field="Id" data-query-label="ID" data-query-value-type="number">{{ id }}</span>
        <span
            :data-query-value="createdAt || ''"
            class="created-at"
            data-query-field="CreatedAt"
            data-query-label="创建时间"
            data-query-value-type="date"
            >{{ formattedCreatedAt }}</span
        >
    </div>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { formatDateTime } from '@/utils/date'

defineOptions({ name: 'ArtListIdCell' })

const props = defineProps<{ id: string | number; createdAt?: string | Date | null }>()
const { locale } = useI18n()
const formattedCreatedAt = computed(() => (props.createdAt ? formatDateTime(props.createdAt, locale.value) : '-'))
</script>

<style scoped>
.list-id-cell {
    display: flex;
    flex-direction: column;
    min-width: 0;
    line-height: 20px;
}
.list-id {
    overflow: hidden;
    color: var(--el-text-color-primary);
    text-overflow: ellipsis;
    white-space: nowrap;
}
.created-at {
    overflow: hidden;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    text-overflow: ellipsis;
    white-space: nowrap;
}
</style>
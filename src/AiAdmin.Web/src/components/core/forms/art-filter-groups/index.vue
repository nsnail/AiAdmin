<template>
    <section v-if="groups.length" v-loading="loading" class="art-filter-groups">
        <div v-for="group in groups" :key="group.field" class="filter-group-row">
            <div class="filter-group-label">{{ t(group.label) }}（{{ group.total }}）</div>
            <div class="filter-group-options">
                <button
                    :class="{ active: !hasSelection(group.field) }"
                    @click="select(group.field, undefined)"
                    class="filter-group-option"
                    type="button">
                    {{ t('listFilter.group.all') }}
                </button>
                <ElBadge
                    v-for="option in group.options"
                    :hidden="option.count === 0"
                    :key="optionKey(option.value)"
                    :value="option.count"
                    class="filter-group-badge">
                    <button
                        :class="{ active: isSelected(group.field, option.value) }"
                        @click="select(group.field, option.value)"
                        class="filter-group-option"
                        type="button">
                        {{ translateOption(option.label) }}
                    </button>
                </ElBadge>
            </div>
        </div>
    </section>
</template>

<script lang="ts" setup>
import type { ListFilterGroup } from '@/api/system-manage'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'ArtFilterGroups' })

interface Props {
    groups?: ListFilterGroup[]
    loading?: boolean
    selections?: Record<string, unknown>
}

const props = withDefaults(defineProps<Props>(), {
    groups: () => [],
    loading: false,
    selections: () => ({}),
})
const emit = defineEmits<{ select: [field: string, value: unknown] }>()
const { t } = useI18n()

const optionKey = (value: unknown): string => JSON.stringify(value) ?? 'undefined'
const hasSelection = (field: string): boolean => Object.prototype.hasOwnProperty.call(props.selections, field)
const isSelected = (field: string, value: unknown): boolean =>
    hasSelection(field) && JSON.stringify(props.selections[field]) === JSON.stringify(value)
const translateOption = (label: string): string => {
    const translated = t(label)
    return translated === label ? label : translated
}
const select = (field: string, value: unknown): void => emit('select', field, value)
</script>

<style scoped>
.art-filter-groups {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    margin-bottom: 8px;
    padding: 10px 16px;
    background: var(--el-bg-color);
    border: 1px solid var(--el-border-color-lighter);
}

.filter-group-row {
    display: grid;
    grid-template-columns: minmax(110px, 180px) minmax(0, 1fr);
    gap: 10px;
    min-height: 42px;
    padding: 5px 12px 5px 0;
    border-bottom: 1px dashed var(--el-border-color-lighter);
}

.filter-group-label {
    display: flex;
    align-items: center;
    color: var(--el-text-color-secondary);
    font-size: 13px;
}

.filter-group-options {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 12px;
    align-items: center;
    min-width: 0;
}

.filter-group-option {
    min-height: 28px;
    padding: 4px 13px;
    color: var(--el-text-color-primary);
    font-size: 13px;
    line-height: 20px;
    background: var(--el-fill-color-light);
    border: 0;
    border-radius: 14px;
    cursor: pointer;
    transition:
        color 0.15s ease,
        background-color 0.15s ease;
}

.filter-group-option:hover {
    color: var(--el-color-primary);
    background: var(--el-color-primary-light-9);
}

.filter-group-option.active {
    color: var(--el-color-white);
    background: var(--el-color-primary);
}

.filter-group-badge :deep(.el-badge__content) {
    top: 2px;
    right: 5px;
}

@media (max-width: 1100px) {
    .art-filter-groups {
        grid-template-columns: 1fr;
    }
}

@media (max-width: 640px) {
    .filter-group-row {
        grid-template-columns: 1fr;
        gap: 4px;
    }
}
</style>
<template>
    <section v-if="groups.length || reserveSpace" v-loading="loading" :class="{ 'is-empty': !groups.length }" class="art-filter-groups">
        <div v-for="group in groups" :key="group.field" class="filter-group-row">
            <button :aria-expanded="isExpanded(group.field)" @click="toggleExpanded(group.field)" class="filter-group-label" type="button">
                <span>{{ t(group.label) }}（{{ group.options.length }}）</span>
                <ArtSvgIcon v-if="hasOverflow(group.field)" :icon="isExpanded(group.field) ? 'ri:arrow-up-s-line' : 'ri:arrow-down-s-line'" />
            </button>
            <div :class="{ expanded: isExpanded(group.field) }" :ref="(element) => setOptionsRef(group.field, element)" class="filter-group-options">
                <ElBadge :hidden="group.total === 0" :max="Number.MAX_SAFE_INTEGER" :value="group.total" class="filter-group-badge">
                    <button
                        :class="{ active: !hasSelection(group.field) }"
                        @click="select(group.field, undefined)"
                        class="filter-group-option"
                        type="button">
                        {{ t('listFilter.group.all') }}
                    </button>
                </ElBadge>
                <ElBadge
                    v-for="option in group.options"
                    :hidden="option.count === 0"
                    :key="optionKey(option.value)"
                    :max="Number.MAX_SAFE_INTEGER"
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
    reserveSpace?: boolean
}

const props = withDefaults(defineProps<Props>(), {
    groups: () => [],
    loading: false,
    selections: () => ({}),
    reserveSpace: false,
})
const emit = defineEmits<{ select: [field: string, value: unknown] }>()
const { t } = useI18n()
const expandedGroups = ref<Record<string, boolean>>({})
const overflowGroups = ref<Record<string, boolean>>({})
const optionsRefs = new Map<string, HTMLElement>()

const optionKey = (value: unknown): string => JSON.stringify(value) ?? 'undefined'
const hasSelection = (field: string): boolean => Object.prototype.hasOwnProperty.call(props.selections, field)
const isSelected = (field: string, value: unknown): boolean =>
    hasSelection(field) && JSON.stringify(props.selections[field]) === JSON.stringify(value)
const translateOption = (label: string): string => {
    const translated = t(label)
    return translated === label ? label : translated
}
const select = (field: string, value: unknown): void => emit('select', field, value)
const isExpanded = (field: string): boolean => expandedGroups.value[field] === true
const hasOverflow = (field: string): boolean => overflowGroups.value[field] === true
const toggleExpanded = (field: string): void => {
    if (!hasOverflow(field)) return
    expandedGroups.value[field] = !isExpanded(field)
}

const setOptionsRef = (field: string, element: Element | null): void => {
    if (element instanceof HTMLElement) optionsRefs.set(field, element)
    else optionsRefs.delete(field)
}

const measureOverflow = async (): Promise<void> => {
    await nextTick()
    const result: Record<string, boolean> = {}
    props.groups.forEach((group) => {
        const element = optionsRefs.get(group.field)
        result[group.field] = element ? element.scrollHeight > 28 : false
        if (!result[group.field]) delete expandedGroups.value[group.field]
    })
    overflowGroups.value = result
}

watch(() => props.groups, measureOverflow, { deep: true, immediate: true })
onMounted(() => window.addEventListener('resize', measureOverflow))
onBeforeUnmount(() => window.removeEventListener('resize', measureOverflow))
</script>

<style scoped>
.art-filter-groups {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    margin-bottom: 8px;
    padding: 10px 16px;
    min-height: 48px;
    background: var(--el-bg-color);
    border: 1px solid var(--el-border-color-lighter);
}

.art-filter-groups.is-empty {
    visibility: hidden;
}

.filter-group-row {
    display: grid;
    grid-template-columns: minmax(110px, 180px) minmax(0, 1fr);
    gap: 10px;
    min-height: 50px;
    align-items: center;
    padding: 5px 12px 5px 0;
    border-bottom: 1px dashed var(--el-border-color-lighter);
}

.filter-group-label {
    width: 100%;
    padding: 0;
    border: 0;
    background: transparent;
    display: flex;
    align-items: center;
    justify-content: flex-start;
    gap: 3px;
    color: var(--el-text-color-secondary);
    font-size: 13px;
    cursor: pointer;
    text-align: left;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.filter-group-label:hover {
    color: var(--el-color-primary);
}

.filter-group-label :deep(.art-svg-icon) {
    font-size: 16px;
}

.filter-group-options {
    display: flex;
    flex-wrap: wrap;
    gap: 8px 12px;
    align-items: center;
    min-width: 0;
    max-height: 28px;
    overflow: hidden;
    transition: max-height 0.2s ease;
}

.filter-group-options.expanded {
    max-height: 1000px;
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
    position: absolute;
    top: 0;
    right: 4px;
    margin: 0;
    transform: translate(50%, 0);
    font-size: 11px;
    pointer-events: none;
}

.filter-group-badge {
    position: relative;
    display: inline-flex;
    align-items: center;
    padding-right: 4px;
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
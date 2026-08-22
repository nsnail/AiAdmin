<template>
    <ElInput v-bind="$attrs" v-model="textValue" class="sc-cron-input">
        <template #append>
            <ElDropdown @command="handleCommand" trigger="click">
                <ElButton :icon="ArrowDown" aria-label="打开 Cron 快捷规则" />
                <template #dropdown>
                    <ElDropdownMenu>
                        <ElDropdownItem v-for="item in shortcuts" :command="item.value" :key="item.value">
                            {{ item.label }}
                        </ElDropdownItem>
                        <ElDropdownItem command="custom" divided>{{ t('cronEditor.custom') }}</ElDropdownItem>
                    </ElDropdownMenu>
                </template>
            </ElDropdown>
        </template>
    </ElInput>
    <div class="sc-cron-tip">{{ cronDescription }}</div>

    <ElDialog v-model="editorVisible" :title="t('cronEditor.title')" append-to-body destroy-on-close width="760px">
        <ElTabs v-model="activeField" class="sc-cron-editor">
            <ElTabPane v-for="field in fields" :key="field.key" :name="field.key">
                <template #label>
                    <div class="sc-cron-tab-label">
                        <span>{{ field.label }}</span>
                        <code>{{ expressionFor(field.key) }}</code>
                    </div>
                </template>
                <ElForm label-width="70px">
                    <ElFormItem :label="t('cronEditor.type')">
                        <ElRadioGroup v-model="state[field.key].type">
                            <ElRadioButton :value="'any'">{{ t('cronEditor.any') }}</ElRadioButton>
                            <ElRadioButton :value="'range'">{{ t('cronEditor.range') }}</ElRadioButton>
                            <ElRadioButton :value="'step'">{{ t('cronEditor.step') }}</ElRadioButton>
                            <ElRadioButton :value="'list'">{{ t('cronEditor.list') }}</ElRadioButton>
                        </ElRadioGroup>
                    </ElFormItem>
                    <ElFormItem v-if="state[field.key].type === 'range'" :label="t('cronEditor.range')">
                        <ElInputNumber v-model="state[field.key].start" :max="field.max" :min="field.min" controls-position="right" />
                        <span class="sc-cron-separator">{{ t('cronEditor.to') }}</span>
                        <ElInputNumber v-model="state[field.key].end" :max="field.max" :min="field.min" controls-position="right" />
                    </ElFormItem>
                    <ElFormItem v-if="state[field.key].type === 'step'" :label="t('cronEditor.step')">
                        <ElInputNumber v-model="state[field.key].start" :max="field.max" :min="field.min" controls-position="right" />
                        <span class="sc-cron-separator">{{ t('cronEditor.every') }}</span>
                        <ElInputNumber v-model="state[field.key].step" :max="field.max - field.min + 1" :min="1" controls-position="right" />
                        <span class="sc-cron-unit">{{ field.unit }}</span>
                    </ElFormItem>
                    <ElFormItem v-if="state[field.key].type === 'list'" :label="t('cronEditor.list')">
                        <ElSelect v-model="state[field.key].list" class="w-full" filterable multiple>
                            <ElOption v-for="value in field.options" :key="value" :label="String(value)" :value="value" />
                        </ElSelect>
                    </ElFormItem>
                </ElForm>
            </ElTabPane>
        </ElTabs>
        <div class="sc-cron-preview">
            <span>{{ t('cronEditor.currentExpression') }}</span>
            <code>{{ generatedValue }}</code>
        </div>
        <template #footer>
            <ElButton @click="editorVisible = false">{{ t('common.cancel') }}</ElButton>
            <ElButton @click="apply" type="primary">{{ t('common.confirm') }}</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import { ArrowDown } from '@element-plus/icons-vue'
import { useI18n } from 'vue-i18n'

type FieldKey = 'second' | 'minute' | 'hour' | 'day' | 'month' | 'week'
type RuleType = 'any' | 'range' | 'step' | 'list'
type RuleState = { type: RuleType; start: number; end: number; step: number; list: number[] }
type Field = {
    key: FieldKey
    label: string
    unit: string
    min: number
    max: number
    options: number[]
}
const { t } = useI18n()

const props = withDefaults(defineProps<{ modelValue?: string }>(), {
    modelValue: '0 */5 * * * *',
})
const emit = defineEmits<{ (event: 'update:modelValue', value: string): void }>()
const fields = computed<Field[]>(() => [
    {
        key: 'second',
        label: t('cronEditor.fields.second'),
        unit: t('cronEditor.units.second'),
        min: 0,
        max: 59,
        options: Array.from({ length: 60 }, (_, i) => i),
    },
    {
        key: 'minute',
        label: t('cronEditor.fields.minute'),
        unit: t('cronEditor.units.minute'),
        min: 0,
        max: 59,
        options: Array.from({ length: 60 }, (_, i) => i),
    },
    {
        key: 'hour',
        label: t('cronEditor.fields.hour'),
        unit: t('cronEditor.units.hour'),
        min: 0,
        max: 23,
        options: Array.from({ length: 24 }, (_, i) => i),
    },
    {
        key: 'day',
        label: t('cronEditor.fields.day'),
        unit: t('cronEditor.units.day'),
        min: 1,
        max: 31,
        options: Array.from({ length: 31 }, (_, i) => i + 1),
    },
    {
        key: 'month',
        label: t('cronEditor.fields.month'),
        unit: t('cronEditor.units.month'),
        min: 1,
        max: 12,
        options: Array.from({ length: 12 }, (_, i) => i + 1),
    },
    {
        key: 'week',
        label: t('cronEditor.fields.week'),
        unit: t('cronEditor.units.week'),
        min: 0,
        max: 6,
        options: Array.from({ length: 7 }, (_, i) => i),
    },
])
const shortcuts = computed(() => [
    { label: t('cronEditor.shortcuts.everySecond'), value: '* * * * * *' },
    { label: t('cronEditor.shortcuts.everyMinute'), value: '0 * * * * *' },
    { label: t('cronEditor.shortcuts.everyHour'), value: '0 0 * * * *' },
    { label: t('cronEditor.shortcuts.daily'), value: '0 0 0 * * *' },
    { label: t('cronEditor.shortcuts.weekly'), value: '0 0 0 * * 0' },
])
const createRule = (): RuleState => ({ type: 'any', start: 0, end: 1, step: 1, list: [] })
const state = reactive<Record<FieldKey, RuleState>>({
    second: createRule(),
    minute: createRule(),
    hour: createRule(),
    day: createRule(),
    month: createRule(),
    week: createRule(),
})
const editorVisible = ref(false)
const activeField = ref<FieldKey>('second')
const textValue = computed({
    get: () => props.modelValue,
    set: (value: string) => emit('update:modelValue', value),
})
const expressionFor = (key: FieldKey): string => {
    const rule = state[key]
    if (rule.type === 'range') return `${rule.start}-${rule.end}`
    if (rule.type === 'step') return `${rule.start}/${rule.step}`
    if (rule.type === 'list') return rule.list.length ? rule.list.join(',') : '*'
    return '*'
}
const generatedValue = computed(() => fields.value.map((field) => expressionFor(field.key)).join(' '))

const cronDescription = computed(() => {
    const parts = textValue.value.trim().split(/\s+/)
    if (parts.length === 5) parts.unshift('0')
    if (parts.length !== 6 || parts.some((part) => !/^[\d*/,-]+$/.test(part))) return '等待完整的 Cron 表达式'

    const [second, minute, hour, day, month, week] = parts
    if (second === '*' && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everySecond')
    if (second === '0' && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*')
        return t('cronEditor.description.everyMinute')
    if (second === '0' && minute === '0' && hour === '*' && day === '*' && month === '*' && week === '*') return t('cronEditor.description.hourly')

    const secondStep = parseStep(second)
    if (secondStep && minute === '*' && hour === '*' && day === '*' && month === '*' && week === '*') {
        return t('cronEditor.description.everySeconds', { value: secondStep })
    }

    const minuteStep = parseStep(minute)
    if (second === '0' && minuteStep && hour === '*' && day === '*' && month === '*' && week === '*') {
        return t('cronEditor.description.everyMinutes', { value: minuteStep })
    }

    const time = formatTime(hour, minute, second)
    if (time && day === '*' && month === '*' && week === '*') return t('cronEditor.description.daily', { time })
    if (time && day === '*' && month === '*' && /^\d$/.test(week)) return t(`cronEditor.description.weekly${week}`, { time })
    return t('cronEditor.description.custom', { value: textValue.value.trim() })
})

const parseStep = (value: string): number | undefined => {
    const match = value.match(/^(?:\*|\d+)\/(\d+)$/)
    const step = match ? Number(match[1]) : 0
    return step > 0 ? step : undefined
}

const formatTime = (hour: string, minute: string, second: string): string | undefined => {
    if (![hour, minute, second].every((value) => /^\d+$/.test(value))) return undefined
    return [hour, minute, second].map((value) => value.padStart(2, '0')).join(':')
}

const parseRule = (key: FieldKey, raw: string): void => {
    const rule = state[key]
    rule.type = 'any'
    rule.list = []
    if (!raw || raw === '*') return
    if (raw.includes('-')) {
        const [start, end] = raw.split('-').map(Number)
        if (Number.isFinite(start) && Number.isFinite(end)) Object.assign(rule, { type: 'range', start, end })
        return
    }
    if (raw.includes('/')) {
        const [start, step] = raw.split('/').map(Number)
        if (Number.isFinite(start) && Number.isFinite(step)) Object.assign(rule, { type: 'step', start, step })
        return
    }
    const list = raw.split(',').map(Number).filter(Number.isFinite)
    if (list.length) Object.assign(rule, { type: 'list', list })
}
const loadValue = (value: string): void => {
    const parts = value.trim().split(/\s+/)
    const normalized = parts.length === 5 ? ['0', ...parts] : parts
    fields.value.forEach((field, index) => parseRule(field.key, normalized[index] || '*'))
}
const handleCommand = (command: string): void => {
    if (command === 'custom') {
        loadValue(textValue.value)
        editorVisible.value = true
        return
    }
    textValue.value = command
}
const apply = (): void => {
    textValue.value = generatedValue.value
    editorVisible.value = false
}
</script>

<style scoped>
.sc-cron-input :deep(.el-input-group__append) {
    padding: 0;
}
.sc-cron-input :deep(.el-input-group__append .el-button) {
    margin: 0;
    border: 0;
}
.sc-cron-tip {
    min-height: 20px;
    margin-top: 4px;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    line-height: 20px;
}
.sc-cron-tab-label {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 4px;
    min-width: 78px;
}
.sc-cron-tab-label code,
.sc-cron-preview code {
    color: var(--el-color-primary);
    font-size: 12px;
}
.sc-cron-separator,
.sc-cron-unit {
    margin: 0 10px;
}
.sc-cron-preview {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-top: 14px;
    padding: 10px 12px;
    background: var(--el-fill-color-light);
    border-radius: 4px;
}
</style>

<!-- 作业报警的执行结果规则：按完整响应体匹配文本、数字或正则，命中即报警；不选操作符表示不按执行结果报警，编辑弹窗和批量设置弹窗共用 -->
<template>
    <ElFormItem :label="t('scheduledJob.alert.result.title')">
        <div class="result-rule">
            <div class="result-rule-row">
                <ElSelect v-model="operator" :placeholder="t('scheduledJob.alert.result.none')" class="result-rule-operator" clearable>
                    <ElOption v-for="item in operatorOptions" :key="item.value" :label="t(item.label)" :value="item.value" />
                </ElSelect>
                <ElRadioGroup v-if="enabled" v-model="valueType" size="small">
                    <ElRadioButton :value="1">{{ t('scheduledJob.alert.result.number') }}</ElRadioButton>
                    <ElRadioButton :value="2">{{ t('scheduledJob.alert.result.text') }}</ElRadioButton>
                </ElRadioGroup>
            </div>
            <template v-if="enabled">
                <ElInput v-model="value" :class="{ 'is-invalid': !!valueError }" :placeholder="placeholder" maxlength="500" />
                <div v-if="valueError" class="result-rule-error">{{ valueError }}</div>
                <ElCheckbox v-if="valueType === 2" v-model="caseSensitive">{{ t('scheduledJob.alert.result.caseSensitive') }}</ElCheckbox>
            </template>
            <span class="result-rule-hint">{{ t('scheduledJob.alert.result.hint') }}</span>
        </div>
    </ElFormItem>
</template>

<script lang="ts" setup>
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'

defineOptions({ name: 'AlertResultRule' })

/** 数字比较可用的操作符编号，与后端 ResultMatcher 保持一致 */
const NUMBER_OPERATORS = [1, 2, 3, 4, 5, 6]
/** 文本比较可用的操作符编号 */
const TEXT_OPERATORS = [5, 6, 7, 8, 9, 10, 11, 12]
/** 阈值为正则表达式的操作符编号 */
const REGEX_OPERATORS = [11, 12]
const OPERATOR_LABELS: Record<number, string> = {
    1: 'alertMonitor.operator.greaterThan',
    2: 'alertMonitor.operator.greaterThanOrEqual',
    3: 'alertMonitor.operator.lessThan',
    4: 'alertMonitor.operator.lessThanOrEqual',
    5: 'alertMonitor.operator.equal',
    6: 'alertMonitor.operator.notEqual',
    7: 'alertMonitor.operator.contains',
    8: 'alertMonitor.operator.notContains',
    9: 'alertMonitor.operator.startsWith',
    10: 'alertMonitor.operator.endsWith',
    11: 'alertMonitor.operator.regexMatch',
    12: 'alertMonitor.operator.regexNotMatch',
}

const { t } = useI18n()
/** 操作符，undefined 表示不按执行结果报警；比较类型 1 数字 2 文本 */
const operator = defineModel<number | null | undefined>('operator', { default: undefined })
const valueType = defineModel<number>('valueType', { default: 2 })
const value = defineModel<string>('value', { default: '' })
const caseSensitive = defineModel<boolean>('caseSensitive', { default: false })

/** 是否已选择操作符：后端未配置时为 null，清除选择时为 undefined，都表示不按执行结果报警 */
const enabled = computed(() => operator.value !== undefined && operator.value !== null)

/** 当前比较类型可选的操作符 */
const operatorOptions = computed(() =>
    (valueType.value === 1 ? NUMBER_OPERATORS : TEXT_OPERATORS).map((code) => ({ value: code, label: OPERATOR_LABELS[code] })),
)

/** 阈值输入框的占位提示：数字、正则表达式和普通文本各一种 */
const placeholder = computed(() => {
    if (valueType.value === 1) return t('alertMonitor.placeholder.thresholdNumber')
    return t(REGEX_OPERATORS.includes(operator.value ?? 0) ? 'alertMonitor.placeholder.thresholdRegex' : 'alertMonitor.placeholder.thresholdText')
})

/** 阈值错误提示：数字必须是数字，正则必须能编译，其余文本不能为空；没有选择操作符时不校验 */
const valueError = computed(() => {
    if (operator.value === undefined || operator.value === null) return ''
    const text = (value.value ?? '').trim()
    if (valueType.value === 1) {
        return /^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$/.test(text) ? '' : t('alertMonitor.rules.thresholdNumber')
    }
    if (!text) return REGEX_OPERATORS.includes(operator.value) ? t('alertMonitor.rules.thresholdRegex') : t('alertMonitor.rules.thresholdText')
    if (REGEX_OPERATORS.includes(operator.value)) {
        try {
            new RegExp(text)
        } catch {
            return t('alertMonitor.rules.thresholdRegex')
        }
    }
    return ''
})

/** 清除操作符时一并清空阈值，避免遗留无用数据 */
watch(operator, (code) => {
    if (code === undefined || code === null) {
        value.value = ''
        caseSensitive.value = false
    }
})

/** 切换比较类型时，若当前操作符不适用则回到该类型的第一个操作符 */
watch(valueType, (type) => {
    if (operator.value === undefined || operator.value === null) return
    const allowed = type === 1 ? NUMBER_OPERATORS : TEXT_OPERATORS
    if (!allowed.includes(operator.value)) operator.value = allowed[0]
})

/** 对外暴露校验结果，供父级保存前判断 */
defineExpose({ valid: computed(() => valueError.value === '') })
</script>

<style lang="scss" scoped>
.result-rule {
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
}
.result-rule-row {
    display: flex;
    align-items: center;
    gap: 12px;
}
.result-rule-operator {
    width: 180px;
}
.result-rule-error {
    color: var(--el-color-danger);
    font-size: 12px;
    line-height: 1.2;
}
.result-rule-hint {
    color: var(--el-text-color-secondary);
    font-size: 12px;
    line-height: 1.4;
}
.is-invalid :deep(.el-input__wrapper) {
    box-shadow: 0 0 0 1px var(--el-color-danger) inset;
}
</style>
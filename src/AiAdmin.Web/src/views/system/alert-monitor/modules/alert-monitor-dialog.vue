<template>
    <ElDrawer
        v-model="dialogVisible"
        :title="t(monitorData?.id ? 'alertMonitor.dialog.editTitle' : 'alertMonitor.dialog.createTitle')"
        destroy-on-close
        width="620px">
        <ElTabs v-model="activeTab">
            <ElTabPane :label="t('alertMonitor.dialog.tabs.basic')" name="basic">
                <ElForm :model="formData" :rules="rules" label-width="130px" ref="formRef">
                    <ElFormItem :label="t('alertMonitor.fields.name')" prop="name">
                        <ElInput v-model.trim="formData.name" :placeholder="t('alertMonitor.placeholder.name')" maxlength="100" show-word-limit />
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.source')" prop="source">
                        <ElRadioGroup v-model="formData.source">
                            <ElRadio :value="1">{{ t('alertMonitor.source.dictionary') }}</ElRadio>
                            <ElRadio :value="2">{{ t('alertMonitor.source.redis') }}</ElRadio>
                        </ElRadioGroup>
                    </ElFormItem>
                    <ElFormItem v-if="formData.source === 1" :label="t('alertMonitor.fields.categoryCode')" prop="categoryCode">
                        <ElInput v-model.trim="formData.categoryCode" :placeholder="t('alertMonitor.placeholder.categoryCode')" maxlength="100" />
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.target')" prop="target">
                        <ElInput
                            v-model.trim="formData.target"
                            :placeholder="t(formData.source === 1 ? 'alertMonitor.placeholder.dictionaryLabel' : 'alertMonitor.placeholder.redisKey')"
                            maxlength="500" />
                    </ElFormItem>
                    <ElFormItem v-if="formData.source === 2" :label="t('alertMonitor.fields.metric')" prop="metric">
                        <ElRadioGroup v-model="formData.metric">
                            <ElRadio :value="1">{{ t('alertMonitor.metric.value') }}</ElRadio>
                            <ElRadio :value="2">{{ t('alertMonitor.metric.length') }}</ElRadio>
                        </ElRadioGroup>
                        <div v-if="formData.metric === 2" class="monitor-hint">{{ t('alertMonitor.hints.length') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.valueType')" prop="valueType">
                        <ElRadioGroup v-model="formData.valueType" :disabled="isLength">
                            <ElRadio :value="1">{{ t('alertMonitor.valueType.number') }}</ElRadio>
                            <ElRadio :value="2">{{ t('alertMonitor.valueType.string') }}</ElRadio>
                        </ElRadioGroup>
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.operator')" prop="operator">
                        <ElSelect v-model="formData.operator" class="w-full">
                            <ElOption v-for="item in operatorOptions" :key="item.value" :label="t(item.label)" :value="item.value" />
                        </ElSelect>
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.threshold')" prop="threshold">
                        <ElInput
                            v-model="formData.threshold"
                            :placeholder="
                                t(formData.valueType === 1 ? 'alertMonitor.placeholder.thresholdNumber' : 'alertMonitor.placeholder.thresholdText')
                            "
                            maxlength="1000" />
                    </ElFormItem>
                    <ElFormItem v-if="formData.valueType === 2" :label="t('alertMonitor.fields.caseSensitive')">
                        <ElSwitch v-model="formData.caseSensitive" />
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.mentions')">
                        <ElInput v-model="formData.mentions" :placeholder="t('alertMonitor.placeholder.mentions')" maxlength="500" show-word-limit />
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.remark')">
                        <ElInput
                            v-model.trim="formData.remark"
                            :placeholder="t('alertMonitor.placeholder.remark')"
                            :rows="3"
                            maxlength="500"
                            show-word-limit
                            type="textarea" />
                    </ElFormItem>
                    <ElFormItem :label="t('alertMonitor.fields.isEnabled')">
                        <ElSwitch v-model="formData.isEnabled" />
                    </ElFormItem>
                    <ElAlert :closable="false" show-icon type="info">{{ t('alertMonitor.hints.behavior') }}</ElAlert>
                </ElForm>
            </ElTabPane>
            <ElTabPane v-if="monitorData?.id" :label="t('alertMonitor.dialog.tabs.rawData')" name="rawData">
                <ArtRawData :data="monitorData" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton :disabled="saving" @click="dialogVisible = false">{{ t('common.cancel') }}</ElButton>
            <ElButton :loading="saving" @click="submit" type="primary">{{ t('alertMonitor.actions.save') }}</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import type { AlertMonitorInfo, SaveAlertMonitor } from '@/api/system-manage'

defineOptions({ name: 'AlertMonitorDialog' })

const props = defineProps<{
    visible: boolean
    monitorData?: AlertMonitorInfo
    /** 新增时预填的字段，如从字典页或缓存页右键添加监控时带入来源和目标 */
    prefill?: Partial<SaveAlertMonitor>
    saving?: boolean
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
    (event: 'submit', value: SaveAlertMonitor): void
}>()
const { t } = useI18n()
const formRef = ref<FormInstance>()
const activeTab = ref('basic')
const dialogVisible = computed({
    get: () => props.visible,
    set: (value: boolean) => emit('update:visible', value),
})

/** 数字比较可用的操作符编号，与后端 AlertMonitorEvaluator 保持一致 */
const NUMBER_OPERATORS = [1, 2, 3, 4, 5, 6]
/** 字符串比较可用的操作符编号 */
const TEXT_OPERATORS = [5, 6, 7, 8, 9, 10]
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
}

/** 新增时的默认值：字典来源、按数字比较值、大于 */
const createForm = (): SaveAlertMonitor => ({
    name: '',
    source: 1,
    categoryCode: '',
    target: '',
    metric: 1,
    valueType: 1,
    operator: 1,
    threshold: '',
    caseSensitive: false,
    mentions: '',
    remark: '',
    isEnabled: true,
})
const formData = reactive<SaveAlertMonitor>(createForm())

/** 长度指标只能用于 Redis 且按数字比较 */
const isLength = computed(() => formData.source === 2 && formData.metric === 2)

/** 当前比较类型可选的操作符 */
const operatorOptions = computed(() =>
    (formData.valueType === 1 ? NUMBER_OPERATORS : TEXT_OPERATORS).map((value) => ({ value, label: OPERATOR_LABELS[value] })),
)

/** 阈值：数字比较必须是数字，字符串比较不能为空 */
const validateThreshold = (_rule: unknown, value: string, callback: (error?: Error) => void) => {
    const text = (value ?? '').trim()
    if (formData.valueType === 1) {
        callback(/^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$/.test(text) ? undefined : new Error(t('alertMonitor.rules.thresholdNumber')))
        return
    }
    callback(text ? undefined : new Error(t('alertMonitor.rules.thresholdText')))
}

const rules = computed<FormRules>(() => ({
    name: [{ required: true, message: t('alertMonitor.rules.name'), trigger: 'blur' }],
    categoryCode: [{ required: formData.source === 1, message: t('alertMonitor.rules.categoryCode'), trigger: 'blur' }],
    target: [{ required: true, message: t('alertMonitor.rules.target'), trigger: 'blur' }],
    threshold: [{ validator: validateThreshold, trigger: 'blur' }],
}))

/** 切换来源：字典没有长度指标，切回字典时指标回到“值” */
watch(
    () => formData.source,
    (source) => {
        if (source === 1) formData.metric = 1
    },
)

/** 长度指标固定按数字比较 */
watch(isLength, (value) => {
    if (value) formData.valueType = 1
})

/** 切换比较类型时，若当前操作符不适用则回到该类型的第一个操作符，并重新校验阈值 */
watch(
    () => formData.valueType,
    (valueType) => {
        const allowed = valueType === 1 ? NUMBER_OPERATORS : TEXT_OPERATORS
        if (!allowed.includes(formData.operator)) formData.operator = allowed[0]
        nextTick(() => formRef.value?.validateField('threshold').catch(() => undefined))
    },
)

/** 打开时按当前监控回填表单，新增时重置为默认值 */
watch(
    () => props.visible,
    (visible) => {
        if (!visible) return
        activeTab.value = 'basic'
        const row = props.monitorData
        Object.assign(
            formData,
            row
                ? {
                      id: row.id,
                      name: row.name,
                      source: row.source,
                      categoryCode: row.categoryCode,
                      target: row.target,
                      metric: row.metric,
                      valueType: row.valueType,
                      operator: row.operator,
                      threshold: row.threshold,
                      caseSensitive: row.caseSensitive,
                      mentions: row.mentions,
                      remark: row.remark,
                      isEnabled: row.isEnabled,
                  }
                : { ...createForm(), ...props.prefill, id: undefined },
        )
        nextTick(() => formRef.value?.clearValidate())
    },
)

/** 校验通过后提交保存 */
const submit = async (): Promise<void> => {
    if (!(await formRef.value?.validate().catch(() => false))) return
    emit('submit', { ...formData, categoryCode: formData.source === 1 ? formData.categoryCode : '' })
}
</script>

<style scoped>
.monitor-hint {
    width: 100%;
    font-size: 12px;
    line-height: 1.5;
    color: var(--el-text-color-secondary);
}
</style>
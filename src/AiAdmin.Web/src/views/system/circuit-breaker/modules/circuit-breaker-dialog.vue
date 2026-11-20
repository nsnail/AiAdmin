<template>
    <ElDrawer
        v-model="dialogVisible"
        :title="t(ruleData?.id ? 'circuitBreaker.dialog.editTitle' : 'circuitBreaker.dialog.createTitle')"
        destroy-on-close
        width="620px">
        <ElTabs v-model="activeTab">
            <ElTabPane :label="t('circuitBreaker.dialog.tabs.basic')" name="basic">
                <ElForm :model="formData" :rules="rules" label-width="150px" ref="formRef">
                    <ElFormItem :label="t('circuitBreaker.fields.api')" prop="apiEndpointId">
                        <ElSelect
                            v-model="formData.apiEndpointId"
                            :loading="apiLoading"
                            :placeholder="t('circuitBreaker.placeholder.api')"
                            :remote-method="searchApis"
                            class="w-full"
                            filterable
                            remote>
                            <ElOption v-for="item in apiOptions" :key="item.id" :label="`${item.method} ${item.path}`" :value="item.id">
                                <span class="api-option-method">{{ item.method }}</span>
                                <span>{{ item.path }}</span>
                            </ElOption>
                        </ElSelect>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.windowSeconds')" prop="windowSeconds">
                        <ElInputNumber v-model="formData.windowSeconds" :max="3600" :min="1" class="w-full" />
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.minimumRequests')" prop="minimumRequests">
                        <ElInputNumber v-model="formData.minimumRequests" :max="1000000" :min="1" class="w-full" />
                        <div class="rule-hint">{{ t('circuitBreaker.hints.minimumRequests') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.failureRatePercent')" prop="failureRatePercent">
                        <ElInputNumber v-model="formData.failureRatePercent" :max="100" :min="1" class="w-full" />
                        <div class="rule-hint">{{ t('circuitBreaker.hints.failureRate') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.slowCallMs')" prop="slowCallMs">
                        <ElInputNumber v-model="formData.slowCallMs" :max="600000" :min="0" class="w-full" />
                        <div class="rule-hint">{{ t('circuitBreaker.hints.slowCall') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.openSeconds')" prop="openSeconds">
                        <ElInputNumber v-model="formData.openSeconds" :max="3600" :min="1" class="w-full" />
                        <div class="rule-hint">{{ t('circuitBreaker.hints.openSeconds') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.halfOpenRequests')" prop="halfOpenRequests">
                        <ElInputNumber v-model="formData.halfOpenRequests" :max="100" :min="1" class="w-full" />
                        <div class="rule-hint">{{ t('circuitBreaker.hints.halfOpen') }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.remark')">
                        <ElInput
                            v-model.trim="formData.remark"
                            :placeholder="t('circuitBreaker.placeholder.remark')"
                            :rows="3"
                            maxlength="500"
                            show-word-limit
                            type="textarea" />
                    </ElFormItem>
                    <ElFormItem :label="t('circuitBreaker.fields.isEnabled')">
                        <ElSwitch v-model="formData.isEnabled" />
                    </ElFormItem>
                    <ElAlert :closable="false" show-icon type="info">{{ t('circuitBreaker.hints.behavior') }}</ElAlert>
                </ElForm>
            </ElTabPane>
            <ElTabPane v-if="ruleData?.id" :label="t('circuitBreaker.dialog.tabs.rawData')" name="rawData">
                <ArtRawData :data="ruleData" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton :disabled="saving" @click="dialogVisible = false">{{ t('common.cancel') }}</ElButton>
            <ElButton :loading="saving" @click="submit" type="primary">{{ t('circuitBreaker.actions.save') }}</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { fetchGetApiEndpointList, type CircuitBreakerRuleInfo, type SaveCircuitBreakerRule } from '@/api/system-manage'

defineOptions({ name: 'CircuitBreakerDialog' })

const props = defineProps<{
    visible: boolean
    ruleData?: CircuitBreakerRuleInfo
    /** 新增时预选的接口，从接口页右键添加时传入；编辑已有规则时忽略 */
    presetApi?: { id: string; method: string; path: string }
    saving?: boolean
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
    (event: 'submit', value: SaveCircuitBreakerRule): void
}>()
const { t } = useI18n()
const formRef = ref<FormInstance>()
const activeTab = ref('basic')
const dialogVisible = computed({
    get: () => props.visible,
    set: (value: boolean) => emit('update:visible', value),
})

interface ApiOption {
    id: string
    method: string
    path: string
}
const apiOptions = ref<ApiOption[]>([])
const apiLoading = ref(false)

/** 新增时的默认值：60 秒窗口内至少 20 个请求且失败率达到 50% 时熔断 30 秒，半开放行 3 个试探请求，不统计慢调用 */
const createForm = (): SaveCircuitBreakerRule => ({
    apiEndpointId: '',
    failureRatePercent: 50,
    minimumRequests: 20,
    windowSeconds: 60,
    openSeconds: 30,
    halfOpenRequests: 3,
    slowCallMs: 0,
    isEnabled: true,
    remark: '',
})
const formData = reactive<SaveCircuitBreakerRule>(createForm())

const rules = computed<FormRules>(() => ({
    apiEndpointId: [{ required: true, message: t('circuitBreaker.placeholder.api'), trigger: 'change' }],
}))

/** 远程搜索接口，按路径筛选，最多取 30 条 */
const searchApis = async (query: string): Promise<void> => {
    apiLoading.value = true
    try {
        const filter: DynamicFilter | undefined = query ? { field: 'Path', operator: 'Contains', value: query } : undefined
        const rows = await fetchGetApiEndpointList(filter, 'Path', 'asc')
        apiOptions.value = rows.slice(0, 30).map((x) => ({ id: x.id, method: x.method, path: x.path }))
    } finally {
        apiLoading.value = false
    }
}

/** 打开时按当前规则回填表单，并把已选接口放进下拉选项，使其能显示路径而不是主键 */
watch(
    () => props.visible,
    async (visible) => {
        if (!visible) return
        activeTab.value = 'basic'
        const row = props.ruleData
        Object.assign(
            formData,
            row
                ? {
                      id: row.id,
                      apiEndpointId: row.apiEndpointId,
                      failureRatePercent: row.failureRatePercent,
                      minimumRequests: row.minimumRequests,
                      windowSeconds: row.windowSeconds,
                      openSeconds: row.openSeconds,
                      halfOpenRequests: row.halfOpenRequests,
                      slowCallMs: row.slowCallMs,
                      isEnabled: row.isEnabled,
                      remark: row.remark,
                  }
                : { ...createForm(), id: undefined, apiEndpointId: props.presetApi?.id ?? '' },
        )
        apiOptions.value = row ? [{ id: row.apiEndpointId, method: row.method, path: row.path }] : props.presetApi ? [props.presetApi] : []
        await searchApis('')
        // 搜索结果会覆盖选项，预选的接口不在前 30 条里时补回，保证下拉里仍能显示路径
        if (!row && props.presetApi && !apiOptions.value.some((x) => x.id === props.presetApi!.id)) {
            apiOptions.value.unshift(props.presetApi)
        }
        // 搜索结果会覆盖选项，已选项不在前 30 条里时补回，保证下拉里仍能显示路径
        if (row && !apiOptions.value.some((x) => x.id === row.apiEndpointId)) {
            apiOptions.value.unshift({ id: row.apiEndpointId, method: row.method, path: row.path })
        }
        nextTick(() => formRef.value?.clearValidate())
    },
)

/** 校验通过后提交保存 */
const submit = async (): Promise<void> => {
    if (!(await formRef.value?.validate().catch(() => false))) return
    emit('submit', { ...formData })
}
</script>

<style scoped>
.rule-hint {
    width: 100%;
    font-size: 12px;
    line-height: 1.5;
    color: var(--el-text-color-secondary);
}
.api-option-method {
    display: inline-block;
    width: 56px;
    color: var(--el-text-color-secondary);
}
</style>
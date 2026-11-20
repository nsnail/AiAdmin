<template>
    <ElDrawer
        v-model="dialogVisible"
        :title="t(ruleData?.id ? 'rateLimit.dialog.editTitle' : 'rateLimit.dialog.createTitle')"
        destroy-on-close
        width="620px">
        <ElTabs v-model="activeTab">
            <ElTabPane :label="t('rateLimit.dialog.tabs.basic')" name="basic">
                <ElForm :model="formData" :rules="rules" label-width="130px" ref="formRef">
                    <ElFormItem :label="t('rateLimit.fields.api')" prop="apiEndpointId">
                        <ElSelect
                            v-model="formData.apiEndpointId"
                            :loading="apiLoading"
                            :placeholder="t('rateLimit.placeholder.api')"
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
                    <ElFormItem :label="t('rateLimit.fields.algorithm')" prop="algorithm">
                        <ElRadioGroup v-model="formData.algorithm">
                            <ElRadio :value="1">{{ t('rateLimit.algorithm.slidingWindow') }}</ElRadio>
                            <ElRadio :value="2">{{ t('rateLimit.algorithm.leakyBucket') }}</ElRadio>
                            <ElRadio :value="3">{{ t('rateLimit.algorithm.tokenBucket') }}</ElRadio>
                        </ElRadioGroup>
                        <div class="rule-hint">{{ algorithmHint }}</div>
                    </ElFormItem>
                    <ElFormItem :label="t('rateLimit.fields.scope')" prop="scope">
                        <ElRadioGroup v-model="formData.scope">
                            <ElRadio :value="1">{{ t('rateLimit.scope.global') }}</ElRadio>
                            <ElRadio :value="2">{{ t('rateLimit.scope.user') }}</ElRadio>
                            <ElRadio :value="3">{{ t('rateLimit.scope.role') }}</ElRadio>
                        </ElRadioGroup>
                        <div class="rule-hint">{{ t('rateLimit.hints.scope') }}</div>
                    </ElFormItem>
                    <ElFormItem v-if="formData.scope === 2" :label="t('rateLimit.fields.user')">
                        <ElSelect
                            v-model="formData.userId"
                            :loading="userLoading"
                            :placeholder="t('rateLimit.placeholder.user')"
                            :remote-method="searchUsers"
                            class="w-full"
                            clearable
                            filterable
                            remote>
                            <ElOption v-for="item in userOptions" :key="item.id" :label="item.userName" :value="item.id" />
                        </ElSelect>
                    </ElFormItem>
                    <ElFormItem v-if="formData.scope === 3" :label="t('rateLimit.fields.role')" prop="roleCode">
                        <ElSelect v-model="formData.roleCode" :placeholder="t('rateLimit.placeholder.role')" class="w-full" filterable>
                            <ElOption
                                v-for="item in roleOptions"
                                :key="item.roleCode"
                                :label="`${item.roleName} (${item.roleCode})`"
                                :value="item.roleCode" />
                        </ElSelect>
                    </ElFormItem>
                    <ElFormItem :label="t('rateLimit.fields.limit')" prop="limit">
                        <ElInputNumber
                            v-model="formData.limit"
                            :max="1000000"
                            :min="1"
                            :placeholder="t('rateLimit.placeholder.limit')"
                            class="w-full" />
                    </ElFormItem>
                    <ElFormItem :label="t('rateLimit.fields.windowSeconds')" prop="windowSeconds">
                        <ElInputNumber
                            v-model="formData.windowSeconds"
                            :max="86400"
                            :min="1"
                            :placeholder="t('rateLimit.placeholder.windowSeconds')"
                            class="w-full" />
                    </ElFormItem>
                    <ElFormItem :label="t('rateLimit.fields.remark')">
                        <ElInput
                            v-model.trim="formData.remark"
                            :placeholder="t('rateLimit.placeholder.remark')"
                            :rows="3"
                            maxlength="500"
                            show-word-limit
                            type="textarea" />
                    </ElFormItem>
                    <ElFormItem :label="t('rateLimit.fields.isEnabled')">
                        <ElSwitch v-model="formData.isEnabled" />
                    </ElFormItem>
                    <ElAlert :closable="false" show-icon type="info">{{ t('rateLimit.hints.multiple') }}</ElAlert>
                </ElForm>
            </ElTabPane>
            <ElTabPane v-if="ruleData?.id" :label="t('rateLimit.dialog.tabs.rawData')" name="rawData">
                <ArtRawData :data="ruleData" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton :disabled="saving" @click="dialogVisible = false">{{ t('common.cancel') }}</ElButton>
            <ElButton :loading="saving" @click="submit" type="primary">{{ t('rateLimit.actions.save') }}</ElButton>
        </template>
    </ElDrawer>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { FormInstance, FormRules } from 'element-plus'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import type { DynamicFilter } from '@/components/core/forms/art-dynamic-query-drawer/types'
import { fetchGetApiEndpointList, fetchGetRoleList, fetchGetUserList, type RateLimitRuleInfo, type SaveRateLimitRule } from '@/api/system-manage'

defineOptions({ name: 'RateLimitDialog' })

const props = defineProps<{
    visible: boolean
    ruleData?: RateLimitRuleInfo
    /** 新增时预选的接口，从接口页右键添加时传入；编辑已有规则时忽略 */
    presetApi?: { id: string; method: string; path: string }
    saving?: boolean
}>()
const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
    (event: 'submit', value: SaveRateLimitRule): void
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
interface UserOption {
    id: string
    userName: string
}
const apiOptions = ref<ApiOption[]>([])
const userOptions = ref<UserOption[]>([])
const roleOptions = ref<{ roleCode: string; roleName: string }[]>([])
const apiLoading = ref(false)
const userLoading = ref(false)

/** 新增时的默认值：全局、滑动窗口，每分钟 60 次 */
const createForm = (): SaveRateLimitRule => ({
    apiEndpointId: '',
    algorithm: 1,
    scope: 1,
    roleCode: null,
    userId: null,
    limit: 60,
    windowSeconds: 60,
    isEnabled: true,
    remark: '',
})
const formData = reactive<SaveRateLimitRule>(createForm())

/** 按当前参数把算法说明里的速率换算成可读文本，速率最多保留两位小数 */
const algorithmHint = computed(() => {
    const seconds = formData.windowSeconds || 1
    const limit = formData.limit || 0
    const rate = Number((limit / seconds).toFixed(2))
    const key = ['slidingWindow', 'leakyBucket', 'tokenBucket'][formData.algorithm - 1] ?? 'slidingWindow'
    return t(`rateLimit.hints.${key}`, { seconds, limit, rate })
})

const rules = computed<FormRules>(() => ({
    apiEndpointId: [{ required: true, message: t('rateLimit.placeholder.api'), trigger: 'change' }],
    roleCode: [{ required: formData.scope === 3, message: t('rateLimit.placeholder.role'), trigger: 'change' }],
    limit: [{ required: true, type: 'number', min: 1, message: t('rateLimit.placeholder.limit'), trigger: 'blur' }],
    windowSeconds: [{ required: true, type: 'number', min: 1, message: t('rateLimit.placeholder.windowSeconds'), trigger: 'blur' }],
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

/** 远程搜索用户，按用户名筛选，最多取 30 条 */
const searchUsers = async (query: string): Promise<void> => {
    userLoading.value = true
    try {
        const filter: DynamicFilter | undefined = query ? { field: 'UserName', operator: 'Contains', value: query } : undefined
        const result = await fetchGetUserList({ current: 1, size: 30, dynamicFilter: filter })
        userOptions.value = result.records.map((x) => ({ id: x.id, userName: x.userName }))
    } finally {
        userLoading.value = false
    }
}

/** 加载角色下拉选项，接口每页最多 100 条，按总数翻页取全 */
const loadRoles = async (): Promise<void> => {
    const pageSize = 100
    const roles: { roleCode: string; roleName: string }[] = []
    for (let current = 1; ; current++) {
        const result = await fetchGetRoleList({ current, size: pageSize })
        roles.push(...result.records.map((x) => ({ roleCode: x.roleCode, roleName: x.roleName })))
        if (roles.length >= result.total || result.records.length === 0) break
    }
    roleOptions.value = roles
}

/** 切换范围时清掉不再适用的用户或角色，避免残留 */
watch(
    () => formData.scope,
    (scope) => {
        if (scope !== 2) formData.userId = null
        if (scope !== 3) formData.roleCode = null
    },
)

/** 打开时按当前规则回填表单，并把已选接口和用户放进下拉选项，使其能显示名称而不是主键 */
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
                      algorithm: row.algorithm,
                      scope: row.scope,
                      roleCode: row.roleCode,
                      userId: row.userId,
                      limit: row.limit,
                      windowSeconds: row.windowSeconds,
                      isEnabled: row.isEnabled,
                      remark: row.remark,
                  }
                : { ...createForm(), id: undefined, apiEndpointId: props.presetApi?.id ?? '' },
        )
        apiOptions.value = row ? [{ id: row.apiEndpointId, method: row.method, path: row.path }] : props.presetApi ? [props.presetApi] : []
        userOptions.value = row?.userId ? [{ id: row.userId, userName: row.userName ?? row.userId }] : []
        await Promise.all([searchApis(''), searchUsers(''), loadRoles()])
        // 搜索结果会覆盖选项，预选的接口不在前 30 条里时补回，保证下拉里仍能显示路径
        if (!row && props.presetApi && !apiOptions.value.some((x) => x.id === props.presetApi!.id)) {
            apiOptions.value.unshift(props.presetApi)
        }
        if (row) {
            // 搜索结果会覆盖选项，已选项不在前 30 条里时补回，保证下拉里仍能显示名称
            if (!apiOptions.value.some((x) => x.id === row.apiEndpointId)) {
                apiOptions.value.unshift({ id: row.apiEndpointId, method: row.method, path: row.path })
            }
            if (row.userId && !userOptions.value.some((x) => x.id === row.userId)) {
                userOptions.value.unshift({ id: row.userId, userName: row.userName ?? row.userId })
            }
        }
        nextTick(() => formRef.value?.clearValidate())
    },
)

/** 校验通过后提交保存 */
const submit = async (): Promise<void> => {
    if (!(await formRef.value?.validate().catch(() => false))) return
    emit('submit', {
        ...formData,
        userId: formData.scope === 2 ? formData.userId : null,
        roleCode: formData.scope === 3 ? formData.roleCode : null,
    })
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
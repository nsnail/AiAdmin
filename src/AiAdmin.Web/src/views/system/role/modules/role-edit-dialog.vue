<template>
    <ElDialog
        v-model="visible"
        :title="t(dialogType === 'add' ? 'roleManagement.dialog.addTitle' : 'roleManagement.dialog.editTitle')"
        @close="handleClose"
        align-center
        width="min(520px, 92vw)">
        <ElTabs v-model="activeTab">
            <ElTabPane :label="t('roleManagement.dialog.basicInfo')" name="form">
                <ElForm :model="form" :rules="rules" label-width="120px" ref="formRef">
                    <ElFormItem :label="t('listFilter.role.name')" prop="roleName">
                        <ElInput v-model="form.roleName" :placeholder="t('listFilter.placeholder.roleName')" />
                    </ElFormItem>
                    <ElFormItem :label="t('listFilter.role.code')" prop="roleCode">
                        <ElInput v-model="form.roleCode" :placeholder="t('listFilter.placeholder.roleCode')" />
                    </ElFormItem>
                    <ElFormItem :label="t('listFilter.role.description')" prop="description">
                        <ElInput v-model="form.description" :placeholder="t('listFilter.placeholder.description')" :rows="3" type="textarea" />
                    </ElFormItem>
                    <ElFormItem :label="t('listFilter.role.dataScope')" prop="dataScope">
                        <ElSelect v-model="form.dataScope" class="w-full" filterable>
                            <ElOption :label="t('listFilter.option.allData')" value="all" />
                            <ElOption :label="t('listFilter.option.departmentData')" value="department" />
                            <ElOption :label="t('listFilter.option.departmentAndChildren')" value="department_and_children" />
                            <ElOption :label="t('listFilter.option.ownData')" value="self" />
                        </ElSelect>
                    </ElFormItem>
                    <ElFormItem :label="t('listFilter.common.status')">
                        <ElSwitch v-model="form.enabled" />
                    </ElFormItem>
                </ElForm>
            </ElTabPane>
            <ElTabPane v-if="props.dialogType === 'edit'" :label="t('roleManagement.dialog.rawData')" name="raw-data">
                <ArtRawData :data="rawData" />
            </ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton :disabled="saving" @click="handleClose">{{ t('common.cancel') }}</ElButton>
            <ElButton :loading="saving" @click="handleSubmit" type="primary">{{ t('table.form.submit') }}</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import type { FormInstance, FormRules } from 'element-plus'
import { fetchCreateRole, fetchUpdateRole } from '@/api/system-manage'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { useI18n } from 'vue-i18n'

type RoleListItem = Api.SystemManage.RoleListItem

interface Props {
    modelValue: boolean
    dialogType: 'add' | 'edit'
    roleData?: RoleListItem
}

interface Emits {
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
}

const props = withDefaults(defineProps<Props>(), {
    modelValue: false,
    dialogType: 'add',
    roleData: undefined,
})

const emit = defineEmits<Emits>()
const { t } = useI18n()

const formRef = ref<FormInstance>()
const activeTab = ref('form')
const saving = ref(false)

/**
 * 弹窗显示状态双向绑定
 */
const visible = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value),
})

/**
 * 表单验证规则
 */
const rules = computed<FormRules>(() => ({
    roleName: [
        { required: true, message: t('roleManagement.validation.nameRequired'), trigger: 'blur' },
        { min: 2, max: 50, message: t('roleManagement.validation.nameLength'), trigger: 'blur' },
    ],
    roleCode: [
        { required: true, message: t('roleManagement.validation.codeRequired'), trigger: 'blur' },
        { min: 2, max: 50, message: t('roleManagement.validation.codeLength'), trigger: 'blur' },
        { pattern: /^[A-Z][A-Z0-9_]*$/, message: t('roleManagement.validation.codeFormat'), trigger: 'blur' },
    ],
    dataScope: [{ required: true, message: t('roleManagement.validation.dataScopeRequired'), trigger: 'change' }],
}))

/**
 * 表单数据
 */
const form = reactive<RoleListItem>({
    roleId: '',
    roleName: '',
    roleCode: '',
    description: '',
    dataScope: 'self',
    createTime: '',
    updateTime: null,
    enabled: true,
})
const rawData = computed(() => (props.dialogType === 'edit' ? props.roleData : form))

/**
 * 监听弹窗打开，初始化表单数据
 */
watch(
    () => props.modelValue,
    (newVal) => {
        if (newVal) {
            activeTab.value = 'form'
            initForm()
        }
    },
)

/**
 * 监听角色数据变化，更新表单
 */
watch(
    () => props.roleData,
    (newData) => {
        if (newData && props.modelValue) initForm()
    },
    { deep: true },
)

/**
 * 初始化表单数据
 * 根据弹窗类型填充表单或重置表单
 */
const initForm = () => {
    if (props.dialogType === 'edit' && props.roleData) {
        Object.assign(form, props.roleData)
    } else {
        Object.assign(form, {
            roleId: '',
            roleName: '',
            roleCode: '',
            description: '',
            dataScope: 'self',
            createTime: '',
            updateTime: null,
            enabled: true,
        })
    }
}

/**
 * 关闭弹窗并重置表单
 */
const handleClose = () => {
    if (saving.value) return
    visible.value = false
    formRef.value?.resetFields()
}

/**
 * 提交表单
 * 验证通过后调用接口保存数据
 */
const handleSubmit = async () => {
    if (!formRef.value || saving.value) return

    try {
        await formRef.value.validate()
        saving.value = true
        const data: Api.SystemManage.SaveRoleParams = {
            roleName: form.roleName,
            roleCode: form.roleCode,
            description: form.description,
            dataScope: form.dataScope,
            enabled: form.enabled,
        }
        if (props.dialogType === 'edit') {
            await fetchUpdateRole(form.roleId, data)
        } else {
            await fetchCreateRole(data)
        }
        ElMessage.success(t(props.dialogType === 'add' ? 'roleManagement.message.created' : 'roleManagement.message.updated'))
        emit('success')
        handleClose()
    } catch (error) {
        console.error('Role form validation failed:', error)
    } finally {
        saving.value = false
    }
}
</script>
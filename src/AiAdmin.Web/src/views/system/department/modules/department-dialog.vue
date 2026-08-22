<template>
    <ElDialog v-model="dialogVisible" :title="dialogType === 'add' ? '新增部门' : '编辑部门'" align-center width="560px">
        <ElTabs v-model="activeTab">
            <ElTabPane label="基本信息" name="form">
                <ElForm :model="formData" :rules="rules" label-width="90px" ref="formRef">
                    <ElFormItem label="上级部门" prop="parentId">
                        <ElTreeSelect
                            v-model="formData.parentId"
                            :data="availableParents"
                            :props="{ label: 'name', children: 'children' }"
                            check-strictly
                            class="w-full"
                            clearable
                            filterable
                            node-key="id"
                            placeholder="不选择则为根部门" />
                    </ElFormItem>
                    <ElFormItem label="部门名称" prop="name">
                        <ElInput v-model.trim="formData.name" maxlength="100" placeholder="请输入部门名称" />
                    </ElFormItem>
                    <ElFormItem label="部门编码" prop="code">
                        <ElInput v-model.trim="formData.code" maxlength="50" placeholder="请输入唯一部门编码" />
                    </ElFormItem>
                    <ElFormItem label="显示顺序" prop="sort">
                        <ElInputNumber v-model="formData.sort" :max="9999" :min="0" controls-position="right" />
                    </ElFormItem>
                    <ElFormItem label="负责人" prop="leader">
                        <ElInput v-model.trim="formData.leader" maxlength="50" placeholder="请输入负责人" />
                    </ElFormItem>
                    <ElFormItem label="联系电话" prop="phone">
                        <ElInput v-model.trim="formData.phone" maxlength="20" placeholder="请输入联系电话" />
                    </ElFormItem>
                    <ElFormItem label="邮箱" prop="email">
                        <ElInput v-model.trim="formData.email" maxlength="100" placeholder="请输入邮箱" />
                    </ElFormItem>
                    <ElFormItem label="是否启用">
                        <ElSwitch v-model="formData.isEnabled" active-text="启用" inactive-text="停用" />
                    </ElFormItem>
                </ElForm>
            </ElTabPane>
            <ElTabPane v-if="props.type === 'edit'" :label="t('rawData')" name="raw-data"><ArtRawData :data="rawData" /></ElTabPane>
        </ElTabs>
        <template #footer>
            <ElButton :disabled="props.saving" @click="dialogVisible = false">取消</ElButton>
            <ElButton :loading="props.saving" @click="handleSubmit" type="primary">保存</ElButton>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import type { FormInstance, FormRules } from 'element-plus'
import { useI18n } from 'vue-i18n'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'

type Department = Api.SystemManage.DepartmentTreeItem
type SaveDepartment = Api.SystemManage.SaveDepartmentParams

const props = defineProps<{
    visible: boolean
    type: 'add' | 'edit'
    departmentData?: Partial<Department>
    departments: Department[]
    saving?: boolean
}>()

const emit = defineEmits<{
    (event: 'update:visible', value: boolean): void
    (event: 'submit', value: SaveDepartment): void
}>()

const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value),
})
const dialogType = computed(() => props.type)
const { t } = useI18n()
const defaultDepartmentCode = 'DEFAULT'
const formRef = ref<FormInstance>()
const activeTab = ref('form')
const formData = reactive<SaveDepartment>({
    name: '',
    code: '',
    parentId: null,
    sort: 0,
    leader: '',
    phone: '',
    email: '',
    isEnabled: true,
})
const rawData = computed(() => (props.type === 'edit' ? props.departmentData : formData))
const localizedTree = (items: Department[]): Department[] =>
    items.map((item) => ({
        ...item,
        name: item.code === defaultDepartmentCode ? t('userManagement.defaultDepartment') : item.name,
        children: localizedTree(item.children),
    }))
const rules: FormRules = {
    name: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
    code: [{ required: true, message: '请输入部门编码', trigger: 'blur' }],
    email: [{ type: 'email', message: '邮箱格式不正确', trigger: 'blur' }],
}

const collectDescendantIds = (node: Department | undefined): Set<string> => {
    const result = new Set<string>()
    const visit = (current: Department) => {
        result.add(current.id)
        current.children.forEach(visit)
    }
    if (node) visit(node)
    return result
}

const findDepartment = (items: Department[], id: string): Department | undefined => {
    for (const item of items) {
        if (item.id === id) return item
        const child = findDepartment(item.children, id)
        if (child) return child
    }
    return undefined
}

const availableParents = computed(() => {
    if (props.type !== 'edit' || !props.departmentData?.id) return localizedTree(props.departments)
    const excludedIds = collectDescendantIds(findDepartment(props.departments, props.departmentData.id))
    const filterTree = (items: Department[]): Department[] =>
        items.filter((item) => !excludedIds.has(item.id)).map((item) => ({ ...item, children: filterTree(item.children) }))
    return filterTree(localizedTree(props.departments))
})

watch(
    () => props.visible,
    (visible) => {
        if (!visible) return
        activeTab.value = 'form'
        const row = props.departmentData
        Object.assign(formData, {
            name: props.type === 'edit' ? (row?.name ?? '') : '',
            code: props.type === 'edit' ? (row?.code ?? '') : '',
            parentId: row?.parentId ?? null,
            sort: props.type === 'edit' ? (row?.sort ?? 0) : 0,
            leader: props.type === 'edit' ? (row?.leader ?? '') : '',
            phone: props.type === 'edit' ? (row?.phone ?? '') : '',
            email: props.type === 'edit' ? (row?.email ?? '') : '',
            isEnabled: props.type === 'edit' ? (row?.isEnabled ?? true) : true,
        })
        nextTick(() => formRef.value?.clearValidate())
    },
)

const handleSubmit = async (): Promise<void> => {
    if (props.saving) return
    if (formRef.value && (await formRef.value.validate())) {
        emit('submit', { ...formData })
    }
}
</script>

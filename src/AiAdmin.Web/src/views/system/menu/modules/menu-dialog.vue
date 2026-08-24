<template>
    <ElDialog
        :model-value="visible"
        :title="dialogTitle"
        @closed="handleClosed"
        @update:model-value="handleCancel"
        align-center
        class="menu-dialog"
        width="860px">
        <ElTabs v-model="activeTab">
            <ElTabPane label="基本信息" name="form">
                <ArtForm
                    v-model="form"
                    :gutter="20"
                    :items="formItems"
                    :rules="rules"
                    :show-reset="false"
                    :show-submit="false"
                    :span="width > 640 ? 12 : 24"
                    label-width="100px"
                    ref="formRef">
                </ArtForm>
            </ElTabPane>
            <ElTabPane v-if="props.editData?.id" label="原始数据" name="raw-data"><ArtRawData :data="rawData" /></ElTabPane>
        </ElTabs>

        <template #footer>
            <span class="dialog-footer">
                <ElButton :disabled="props.saving" @click="handleCancel">取 消</ElButton>
                <ElButton :loading="props.saving" @click="handleSubmit" type="primary">确 定</ElButton>
            </span>
        </template>
    </ElDialog>
</template>

<script lang="ts" setup>
import type { FormRules } from 'element-plus'
import { ElIcon, ElTooltip } from 'element-plus'
import { QuestionFilled } from '@element-plus/icons-vue'
import { formatMenuTitle } from '@/utils/router'
import type { AppRouteRecord } from '@/types/router'
import type { FormItem } from '@/components/core/forms/art-form/index.vue'
import ArtForm from '@/components/core/forms/art-form/index.vue'
import ArtRawData from '@/components/core/others/art-raw-data/index.vue'
import { useWindowSize } from '@vueuse/core'
import { useI18n } from 'vue-i18n'

const { width } = useWindowSize()
const { t } = useI18n()

/**
 * 创建带 tooltip 的表单标签
 * @param label 标签文本
 * @param tooltip 提示文本
 * @returns 渲染函数
 */
const createLabelTooltip = (label: string, tooltip: string) => {
    return () =>
        h('span', { class: 'flex items-center' }, [
            h('span', label),
            h(
                ElTooltip,
                {
                    content: tooltip,
                    placement: 'top',
                },
                () => h(ElIcon, { class: 'ml-0.5 cursor-help' }, () => h(QuestionFilled)),
            ),
        ])
}

interface MenuFormData {
    id: string
    name: string
    path: string
    label: string
    parentName: string
    component: string
    icon: string
    isEnable: boolean
    sort: number
    isMenu: boolean
    keepAlive: boolean
    isHide: boolean
    isHideTab: boolean
    link: string
    isIframe: boolean
    showBadge: boolean
    showTextBadge: string
    fixedTab: boolean
    activePath: string
    isFullPage: boolean
    authName: string
    authLabel: string
    authIcon: string
}

interface Props {
    visible: boolean
    editData?: AppRouteRecord | any
    type?: 'menu' | 'button'
    saving?: boolean
    menus?: AppRouteRecord[]
}

interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit', data: MenuFormData): void
}

const props = withDefaults(defineProps<Props>(), {
    visible: false,
    type: 'menu',
})

const emit = defineEmits<Emits>()

const formRef = ref()
const isEdit = ref(false)
const activeTab = ref('form')

const form = reactive<MenuFormData & { menuType: 'menu' | 'button' }>({
    menuType: 'menu',
    id: '',
    name: '',
    path: '',
    label: '',
    parentName: '',
    component: '',
    icon: '',
    isEnable: true,
    sort: 1,
    isMenu: true,
    keepAlive: true,
    isHide: false,
    isHideTab: false,
    link: '',
    isIframe: false,
    showBadge: false,
    showTextBadge: '',
    fixedTab: false,
    activePath: '',
    isFullPage: false,
    authName: '',
    authLabel: '',
    authIcon: '',
})
const rawData = computed(() => props.editData ?? form)

const rules = reactive<FormRules>({
    name: [
        { required: true, message: '请输入菜单名称', trigger: 'blur' },
        { min: 2, max: 20, message: '长度在 2 到 20 个字符', trigger: 'blur' },
    ],
    path: [{ required: true, message: '请输入路由地址', trigger: 'blur' }],
    label: [{ required: true, message: '输入权限标识', trigger: 'blur' }],
    authName: [{ required: true, message: '请输入权限名称', trigger: 'blur' }],
    authLabel: [{ required: true, message: '请输入权限标识', trigger: 'blur' }],
})

interface ParentMenuOption {
    label: string
    value: string
    children: ParentMenuOption[]
}

const buildParentMenuOptions = (): ParentMenuOption[] => {
    const excludedNames = new Set<string>()
    const collectExcluded = (item?: AppRouteRecord): void => {
        if (!item) return
        if (item.name) excludedNames.add(String(item.name))
        item.children?.forEach(collectExcluded)
    }
    collectExcluded(props.editData)

    const convert = (items: AppRouteRecord[], ancestors = new Set<string>()): ParentMenuOption[] =>
        items
            .filter((item) => !item.meta?.isAuthButton && !excludedNames.has(String(item.name)))
            .flatMap((item) => {
                const value = String(item.name || '')
                if (!value || ancestors.has(value)) return []
                const nextAncestors = new Set(ancestors)
                nextAncestors.add(value)
                return [
                    {
                        label: formatMenuTitle(item.meta?.title || value),
                        value,
                        children: convert(item.children || [], nextAncestors),
                    },
                ]
            })

    return convert(props.menus || [])
}
const parentMenuOptions = shallowRef<ParentMenuOption[]>([])
watch(
    [() => props.menus, () => props.editData],
    () => {
        parentMenuOptions.value = buildParentMenuOptions()
    },
    { immediate: true },
)

/**
 * 表单项配置
 */
const formItems = computed<FormItem[]>(() => {
    // Switch 组件的 span：小屏幕 12，大屏幕 6
    const switchSpan = width.value < 640 ? 12 : 6

    if (form.menuType === 'menu') {
        return [
            { label: '菜单名称', key: 'name', type: 'input', props: { placeholder: '菜单名称' } },
            {
                label: t('menuManagement.fields.parent'),
                key: 'parentName',
                type: 'treeselect',
                props: {
                    data: parentMenuOptions.value,
                    props: { label: 'label', value: 'value', children: 'children' },
                    checkStrictly: true,
                    clearable: true,
                    filterable: true,
                    placeholder: t('menuManagement.placeholder.parent'),
                    style: { width: '100%' },
                },
            },
            {
                label: createLabelTooltip('路由地址', '一级菜单：以 / 开头的绝对路径（如 /dashboard）\n二级及以下：相对路径（如 console、user）'),
                key: 'path',
                type: 'input',
                props: { placeholder: '如：/dashboard 或 console' },
            },
            { label: '权限标识', key: 'label', type: 'input', props: { placeholder: '如：User' } },
            {
                label: createLabelTooltip('组件路径', '一级父级菜单：填写 /index/index\n具体页面：填写组件路径（如 /system/user）\n目录菜单：留空'),
                key: 'component',
                type: 'input',
                props: { placeholder: '如：/system/user 或留空' },
            },
            { label: '图标', key: 'icon', type: 'input', props: { placeholder: '如：ri:user-line' } },
            {
                label: '菜单排序',
                key: 'sort',
                type: 'number',
                props: { min: 1, controlsPosition: 'right', style: { width: '100%' } },
            },
            {
                label: '外部链接',
                key: 'link',
                type: 'input',
                props: { placeholder: '如：https://www.example.com' },
            },
            {
                label: '文本徽章',
                key: 'showTextBadge',
                type: 'input',
                props: { placeholder: '如：New、Hot' },
            },
            {
                label: createLabelTooltip('激活路径', '用于详情页等隐藏菜单，指定高亮显示的父级菜单路径\n例如：用户详情页高亮显示"用户管理"菜单'),
                key: 'activePath',
                type: 'input',
                props: { placeholder: '如：/system/user' },
            },
            { label: '是否启用', key: 'isEnable', type: 'switch', span: switchSpan },
            { label: '页面缓存', key: 'keepAlive', type: 'switch', span: switchSpan },
            { label: '隐藏菜单', key: 'isHide', type: 'switch', span: switchSpan },
            { label: '是否内嵌', key: 'isIframe', type: 'switch', span: switchSpan },
            { label: '显示徽章', key: 'showBadge', type: 'switch', span: switchSpan },
            { label: '固定标签', key: 'fixedTab', type: 'switch', span: switchSpan },
            { label: '标签隐藏', key: 'isHideTab', type: 'switch', span: switchSpan },
            { label: '全屏页面', key: 'isFullPage', type: 'switch', span: switchSpan },
        ]
    } else {
        return [
            {
                label: '权限名称',
                key: 'authName',
                type: 'input',
                props: { placeholder: '如：新增、编辑、删除' },
            },
            {
                label: '权限标识',
                key: 'authLabel',
                type: 'input',
                props: { placeholder: '如：add、edit、delete' },
            },
        ]
    }
})

const dialogTitle = computed(() => {
    const type = form.menuType === 'menu' ? '菜单' : '按钮'
    return isEdit.value ? `编辑${type}` : `新建${type}`
})

/**
 * 重置表单数据
 */
const resetForm = (): void => {
    formRef.value?.reset()
    form.menuType = 'menu'
}

/**
 * 加载表单数据（编辑模式）
 */
const loadFormData = (): void => {
    if (!props.editData) return

    isEdit.value = true

    if (form.menuType === 'menu') {
        const row = props.editData as AppRouteRecord & { parentName?: string; sort?: number }
        form.id = row.id || 0
        form.name = formatMenuTitle(row.meta?.title || '')
        form.path = row.path || ''
        form.label = row.name || ''
        form.component = row.component || ''
        form.icon = row.meta?.icon || ''
        form.sort = row.meta?.sort || 1
        form.isMenu = row.meta?.isMenu ?? true
        form.keepAlive = row.meta?.keepAlive ?? false
        form.isHide = row.meta?.isHide ?? false
        form.isHideTab = row.meta?.isHideTab ?? false
        form.isEnable = row.meta?.isEnable ?? true
        form.link = row.meta?.link || ''
        form.isIframe = row.meta?.isIframe ?? false
        form.showBadge = row.meta?.showBadge ?? false
        form.showTextBadge = row.meta?.showTextBadge || ''
        form.fixedTab = row.meta?.fixedTab ?? false
        form.activePath = row.meta?.activePath || ''
        form.parentName = row.parentName || ''
        form.isFullPage = row.meta?.isFullPage ?? false
    } else {
        const row = props.editData
        form.authName = row.title || ''
        form.authLabel = row.authMark || ''
        form.authIcon = row.icon || ''
    }
}

/**
 * 提交表单
 */
const handleSubmit = async (): Promise<void> => {
    if (!formRef.value || props.saving) return

    try {
        await formRef.value.validate()
        emit('submit', { ...form })
    } catch {
        ElMessage.error('表单校验失败，请检查输入')
    }
}

/**
 * 取消操作
 */
const handleCancel = (): void => {
    emit('update:visible', false)
}

/**
 * 对话框关闭后的回调
 */
const handleClosed = (): void => {
    resetForm()
    isEdit.value = false
}

/**
 * 监听对话框显示状态
 */
watch(
    () => props.visible,
    (newVal) => {
        if (newVal) {
            activeTab.value = 'form'
            form.menuType = props.type
            nextTick(() => {
                if (props.editData) {
                    loadFormData()
                }
            })
        }
    },
)

/**
 * 监听菜单类型变化
 */
watch(
    () => props.type,
    (newType) => {
        if (props.visible) {
            form.menuType = newType
        }
    },
)
</script>
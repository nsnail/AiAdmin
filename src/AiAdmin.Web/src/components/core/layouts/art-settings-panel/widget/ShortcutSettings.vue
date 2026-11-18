<!-- 快捷键配置：点击输入框后直接按下组合键录入，Backspace / Delete 清除，Esc 取消 -->
<template>
    <div>
        <SectionTitle :title="$t('setting.shortcuts.title')" class="mt-10" />
        <div v-for="item in items" :key="item.key" class="flex-cb mb-4 last:mb-2">
            <span class="text-sm">{{ item.label }}</span>
            <div class="flex-c gap-1.5">
                <ElInput
                    :class="{ 'is-recording': recordingKey === item.key }"
                    :model-value="displayValue(item.key)"
                    :placeholder="$t('setting.shortcuts.recordHint')"
                    @blur="stopRecording(item.key)"
                    @focus="recordingKey = item.key"
                    @keydown="handleKeydown(item.key, $event)"
                    class="shortcut-input"
                    readonly
                    size="small" />
                <ElButton :disabled="!values[item.key]" @click="applyShortcut(item.key, '')" link size="small" type="primary">{{
                    $t('setting.shortcuts.clear')
                }}</ElButton>
            </div>
        </div>
        <p class="mt-2 text-xs text-g-500">{{ $t('setting.shortcuts.tip') }}</p>
    </div>
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import { ElMessage } from 'element-plus'
import SectionTitle from './SectionTitle.vue'
import { useSettingStore, type ShortcutKey } from '@/store/modules/setting'
import { eventToShortcut, findShortcutConflict } from '@/utils/sys/shortcut'

defineOptions({ name: 'ShortcutSettings' })

const { t } = useI18n()
const settingStore = useSettingStore()
const { shortcutCloseCurrentTab, shortcutCloseOtherTabs, shortcutCloseAllTabs, shortcutQuickSearch } = storeToRefs(settingStore)

/** 配置项：功能标识与显示名称 */
const items = computed<Array<{ key: ShortcutKey; label: string }>>(() => [
    { key: 'closeCurrentTab', label: t('setting.shortcuts.closeCurrentTab') },
    { key: 'closeOtherTabs', label: t('setting.shortcuts.closeOtherTabs') },
    { key: 'closeAllTabs', label: t('setting.shortcuts.closeAllTabs') },
    { key: 'quickSearch', label: t('setting.shortcuts.quickSearch') },
])

/** 各功能当前的快捷键 */
const values = computed<Record<ShortcutKey, string>>(() => ({
    closeCurrentTab: shortcutCloseCurrentTab.value,
    closeOtherTabs: shortcutCloseOtherTabs.value,
    closeAllTabs: shortcutCloseAllTabs.value,
    quickSearch: shortcutQuickSearch.value,
}))

/** 正在录入的功能，为空表示没有输入框处于录入状态 */
const recordingKey = ref<ShortcutKey | ''>('')

/** 输入框显示值：录入中提示按键，否则显示当前快捷键或“未设置” */
const displayValue = (key: ShortcutKey): string =>
    recordingKey.value === key ? t('setting.shortcuts.recording') : values.value[key] || t('setting.shortcuts.unset')

/** 失去焦点时结束录入 */
const stopRecording = (key: ShortcutKey): void => {
    if (recordingKey.value === key) recordingKey.value = ''
}

/** 浏览器会在页面之前处理的快捷键，页面无法拦截，配置了也不会生效 */
const BROWSER_RESERVED = new Set([
    'Ctrl+W',
    'Ctrl+T',
    'Ctrl+N',
    'Ctrl+Shift+W',
    'Ctrl+Shift+T',
    'Ctrl+Shift+N',
    'Ctrl+Tab',
    'Ctrl+Shift+Tab',
    'Alt+F4',
])

/**
 * 保存快捷键：先检查与其他功能的冲突，冲突时不保存并提示
 * @param key 功能标识
 * @param value 快捷键文本，空字符串表示清除
 */
const applyShortcut = (key: ShortcutKey, value: string): void => {
    const conflict = findShortcutConflict(values.value, key, value)
    if (conflict) {
        const name = items.value.find((item) => item.key === conflict)?.label ?? conflict
        ElMessage.warning(t('setting.shortcuts.conflict', { name }))
        return
    }

    settingStore.setShortcut(key, value)
    if (BROWSER_RESERVED.has(value)) ElMessage.warning(t('setting.shortcuts.browserReserved', { shortcut: value }))
}

/**
 * 录入快捷键：Backspace、Delete 清除，Esc 取消，其余带 Ctrl、Alt 或 Meta 的组合键保存
 * @param key 功能标识
 * @param event 键盘事件
 */
const handleKeydown = (key: ShortcutKey, event: KeyboardEvent): void => {
    if (event.isComposing) return
    event.preventDefault()
    event.stopPropagation()
    if (event.key === 'Escape') {
        ;(event.target as HTMLElement | null)?.blur()
        return
    }

    if (event.key === 'Backspace' || event.key === 'Delete') {
        applyShortcut(key, '')
        return
    }

    const shortcut = eventToShortcut(event)
    if (!shortcut) {
        ElMessage.info(t('setting.shortcuts.needModifier'))
        return
    }

    applyShortcut(key, shortcut)
    ;(event.target as HTMLElement | null)?.blur()
}
</script>

<style scoped>
.shortcut-input {
    width: 130px;
}

.shortcut-input.is-recording :deep(.el-input__wrapper) {
    box-shadow: 0 0 0 1px var(--el-color-primary) inset;
}

.shortcut-input :deep(.el-input__inner) {
    text-align: center;
    cursor: pointer;
}
</style>
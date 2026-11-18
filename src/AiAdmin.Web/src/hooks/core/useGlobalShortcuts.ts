/**
 * useGlobalShortcuts - 全局可配置快捷键
 *
 * 监听设置面板“快捷键配置”里保存的组合键：关闭当前标签页、关闭其他标签页、关闭所有标签页、打开顶部快速搜索。
 * 快捷键存放在设置 store 中，修改后立即生效，无需刷新。仅在应用根组件调用一次即可。
 *
 * @module useGlobalShortcuts
 */
import { useRoute } from 'vue-router'
import { useSettingStore } from '@/store/modules/setting'
import { useWorktabStore } from '@/store/modules/worktab'
import mittBus from '@/utils/sys/mittBus'
import { matchShortcut } from '@/utils/sys/shortcut'

/**
 * 判断当前是否有需要先处理的浮层（抽屉、弹窗、消息框）：此时关闭标签页会让用户困惑，交给浮层自己的 Esc 处理
 * @returns 有可见浮层时为 true
 */
const hasOpenOverlay = (): boolean =>
    Array.from(document.querySelectorAll<HTMLElement>('.el-overlay')).some((el) => el.getClientRects().length > 0 && el.style.display !== 'none')

export const useGlobalShortcuts = () => {
    const settingStore = useSettingStore()
    const worktabStore = useWorktabStore()
    const route = useRoute()

    /**
     * 键盘事件处理：命中配置的快捷键时执行对应功能，并阻止浏览器默认行为
     * @param event 键盘事件
     */
    const handleKeydown = (event: KeyboardEvent): void => {
        if (matchShortcut(event, settingStore.shortcutQuickSearch)) {
            event.preventDefault()
            mittBus.emit('openSearchDialog')
            return
        }

        // 关闭标签页类快捷键：有浮层打开时不处理，避免误关用户正在看的页面
        if (hasOpenOverlay()) return
        if (matchShortcut(event, settingStore.shortcutCloseCurrentTab)) {
            event.preventDefault()
            worktabStore.removeTab(route.path)
        } else if (matchShortcut(event, settingStore.shortcutCloseOtherTabs)) {
            event.preventDefault()
            worktabStore.removeOthers(route.path)
        } else if (matchShortcut(event, settingStore.shortcutCloseAllTabs)) {
            event.preventDefault()
            worktabStore.removeAll()
        }
    }

    onMounted(() => window.addEventListener('keydown', handleKeydown))
    onBeforeUnmount(() => window.removeEventListener('keydown', handleKeydown))
}
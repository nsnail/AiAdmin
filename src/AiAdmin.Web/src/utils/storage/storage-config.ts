/**
 * 存储配置管理模块
 *
 * 提供统一的本地存储配置和工具方法
 *
 * ## 主要功能
 *
 * - 存储键名生成（固定前缀，不含版本号）
 * - 存储键匹配的正则表达式生成
 * - 登出延迟配置
 * - 主题、上次登录用户等存储键配置
 *
 * 存储键格式：sys-{storeId}
 * 例如：sys-user, sys-setting
 *
 * 前端不再维护独立的版本号，版本变化由后端通过 WebSocket 推送，页面刷新即升级
 *
 * @module utils/storage/storage-config
 * @author AiAdmin Team
 */
export class StorageConfig {
    /** 存储键前缀 */
    static readonly STORAGE_PREFIX = 'sys-'

    /** 主题键名（index.html中使用了，如果修改，需要同步修改） */
    static readonly THEME_KEY = 'sys-theme'

    /** 上次登录用户ID键名（用于判断是否为同一用户登录） */
    static readonly LAST_USER_ID_KEY = 'sys-last-user-id'

    /** 响应式布局切换时暂存桌面端菜单类型 */
    static readonly RESPONSIVE_MENU_TYPE_KEY = 'sys-responsive-menu-type'

    /** 登出延迟时间（毫秒） */
    static readonly LOGOUT_DELAY = 1000

    /**
     * 生成存储键名
     * @param storeId 存储ID
     */
    static generateStorageKey(storeId: string): string {
        return `${this.STORAGE_PREFIX}${storeId}`
    }

    /**
     * 创建指定存储ID的存储键匹配的正则表达式
     * @param storeId 存储ID
     */
    static createKeyPattern(storeId: string): RegExp {
        return new RegExp(`^${this.STORAGE_PREFIX}${storeId}$`)
    }
}
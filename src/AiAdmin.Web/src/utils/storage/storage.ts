/**
 * 存储兼容性管理模块
 *
 * 提供本地存储数据检查和验证功能
 *
 * ## 主要功能
 *
 * - 存储数据存在性检测
 * - 存储异常自动恢复（清理+登出）
 * - 登录状态验证
 *
 * ## 使用场景
 *
 * - 应用启动时检查存储数据有效性
 * - 路由守卫中验证登录状态
 * - 存储异常时的自动恢复
 *
 * @module utils/storage/storage
 * @author AiAdmin Team
 */
import { router } from '@/router'
import { useUserStore } from '@/store/modules/user'
import { StorageConfig } from '@/utils/storage/storage-config'

/**
 * 存储兼容性管理器
 * 负责本地存储的数据检查和异常恢复
 */
class StorageCompatibilityManager {
    /**
     * 检查是否存在系统存储数据
     */
    private hasStorage(): boolean {
        return Object.keys(localStorage).some((key) => key.startsWith(StorageConfig.STORAGE_PREFIX) && localStorage.getItem(key) !== null)
    }

    /**
     * 显示存储错误消息
     */
    private showStorageError(): void {
        ElMessage({
            type: 'error',
            offset: 40,
            duration: 5000,
            message: '系统检测到本地数据异常，请重新登录系统恢复使用！',
        })
    }

    /**
     * 执行系统登出
     */
    private performSystemLogout(): void {
        setTimeout(() => {
            try {
                localStorage.clear()
                useUserStore().logOut()
                router.push({ name: 'Login' })
                console.info('[Storage] 已执行系统登出')
            } catch (error) {
                console.error('[Storage] 系统登出失败:', error)
            }
        }, StorageConfig.LOGOUT_DELAY)
    }

    /**
     * 处理存储异常
     */
    private handleStorageError(): void {
        this.showStorageError()
        this.performSystemLogout()
    }

    /**
     * 验证存储数据完整性
     * @param requireAuth 是否需要验证登录状态（默认 false）
     */
    validateStorageData(requireAuth: boolean = false): boolean {
        try {
            if (this.hasStorage()) {
                return true
            }

            // 只有在需要验证登录状态时才执行登出操作；首次访问或访问静态路由不需要登出
            if (requireAuth) {
                console.warn('[Storage] 未发现任何存储数据，需要重新登录')
                this.performSystemLogout()
                return false
            }
            return true
        } catch (error) {
            console.error('[Storage] 存储数据验证失败:', error)
            // 只有在需要验证登录状态时才处理错误
            if (requireAuth) {
                this.handleStorageError()
                return false
            }
            return true
        }
    }

    /**
     * 检查存储是否为空
     */
    isStorageEmpty(): boolean {
        return !this.hasStorage()
    }

    /**
     * 检查存储兼容性
     * @param requireAuth 是否需要验证登录状态（默认 false）
     */
    checkCompatibility(requireAuth: boolean = false): boolean {
        try {
            const isValid = this.validateStorageData(requireAuth)
            const isEmpty = this.isStorageEmpty()

            if (isValid || isEmpty) {
                return true
            }

            console.warn('[Storage] 存储兼容性检查失败')
            return false
        } catch (error) {
            console.error('[Storage] 兼容性检查异常:', error)
            return false
        }
    }
}

// 创建存储兼容性管理器实例
const storageManager = new StorageCompatibilityManager()

/**
 * 验证本地存储数据
 * @param requireAuth 是否需要验证登录状态（默认 false）
 */
export function validateStorageData(requireAuth: boolean = false): boolean {
    return storageManager.validateStorageData(requireAuth)
}

/**
 * 检查存储兼容性
 * @param requireAuth 是否需要验证登录状态（默认 false）
 */
export function checkStorageCompatibility(requireAuth: boolean = false): boolean {
    return storageManager.checkCompatibility(requireAuth)
}
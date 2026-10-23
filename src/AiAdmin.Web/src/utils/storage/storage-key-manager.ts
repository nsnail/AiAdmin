/**
 * 存储键名管理器模块
 *
 * 提供 Pinia Store 持久化存储键的生成
 *
 * ## 使用场景
 *
 * - Pinia Store 持久化插件中获取存储键
 *
 * @module utils/storage/storage-key-manager
 * @author AiAdmin Team
 */
import { StorageConfig } from '@/utils/storage/storage-config'

/**
 * 存储键名管理器
 * 负责生成持久化存储键名
 */
export class StorageKeyManager {
    /**
     * 获取持久化存储的键名
     * @param storeId 存储ID
     */
    getStorageKey(storeId: string): string {
        return StorageConfig.generateStorageKey(storeId)
    }
}
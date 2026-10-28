/**
 * 锁屏状态：独立成无依赖模块，供路由守卫和 HTTP 层读取，避免与用户 store、实时连接形成循环依赖
 */
import { ref } from 'vue'

/** 页面是否处于锁定状态，驱动锁屏遮罩显示 */
export const isScreenLocked = ref(false)
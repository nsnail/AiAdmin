<!-- 用户菜单 -->
<template>
    <ElPopover
        :hide-after="0"
        :offset="10"
        :show-arrow="false"
        :width="240"
        placement="bottom-end"
        popper-class="user-menu-popover"
        popper-style="padding: 5px 16px;"
        ref="userMenuPopover"
        trigger="hover">
        <template #reference>
            <ArtUserAvatar :name="userInfo.userName" :src="userInfo.avatar" class="size-8.5 mr-5 c-p max-sm:w-6.5 max-sm:h-6.5 max-sm:mr-[16px]" />
        </template>
        <template #default>
            <div class="pt-3">
                <div class="flex-c pb-1 px-0">
                    <ArtUserAvatar
                        :name="userInfo.userName"
                        :src="userInfo.avatar"
                        class="w-10 h-10 mr-3 ml-0 overflow-hidden rounded-full float-left" />
                    <div class="w-[calc(100%-60px)] h-full">
                        <span class="block text-sm font-medium text-g-800 truncate">{{ userInfo.userName }}</span>
                        <span class="block mt-0.5 text-xs text-g-500 truncate">{{ userInfo.email }}</span>
                    </div>
                </div>
                <ul class="py-4 mt-3 border-t border-g-300/80">
                    <li @click="goPage('/system/user-center')" class="btn-item">
                        <ArtSvgIcon icon="ri:user-3-line" />
                        <span>{{ $t('topBar.user.userCenter') }}</span>
                    </li>
                    <li @click="toDocs()" class="btn-item">
                        <ArtSvgIcon icon="ri:book-2-line" />
                        <span>{{ $t('topBar.user.docs') }}</span>
                    </li>
                    <li @click="toGithub()" class="btn-item">
                        <ArtSvgIcon icon="ri:github-line" />
                        <span>{{ $t('topBar.user.github') }}</span>
                    </li>
                    <li @click="lockScreen()" class="btn-item">
                        <ArtSvgIcon icon="ri:lock-line" />
                        <span>{{ $t('topBar.user.lockScreen') }}</span>
                    </li>
                    <li @click="clearCache()" class="btn-item">
                        <ArtSvgIcon icon="ri:delete-bin-line" />
                        <span>{{ $t('topBar.user.clearCache') }}</span>
                    </li>
                    <div class="w-full h-px my-2 bg-g-300/80"></div>
                    <div @click="loginOut" class="log-out c-p">
                        {{ $t('topBar.user.logout') }}
                    </div>
                </ul>
            </div>
        </template>
    </ElPopover>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { useUserStore } from '@/store/modules/user'
import { WEB_LINKS } from '@/utils/constants'
import { mittBus } from '@/utils/sys'
import ArtUserAvatar from '@/components/core/forms/art-user-avatar/index.vue'

defineOptions({ name: 'ArtUserMenu' })

const router = useRouter()
const { t } = useI18n()
const userStore = useUserStore()

const { getUserInfo: userInfo } = storeToRefs(userStore)
const userMenuPopover = ref()

/**
 * 页面跳转
 * @param {string} path - 目标路径
 */
const goPage = (path: string): void => {
    router.push(path)
}

/**
 * 打开文档页面
 */
const toDocs = (): void => {
    window.open(WEB_LINKS.DOCS)
}

/**
 * 打开 GitHub 页面
 */
const toGithub = (): void => {
    window.open(WEB_LINKS.GITHUB)
}

/**
 * 打开锁屏功能
 */
const lockScreen = (): void => {
    mittBus.emit('openLockScreen')
}

/**
 * 清理浏览器本地缓存并重新初始化系统
 */
const clearCache = (): void => {
    closeUserMenu()
    setTimeout(() => {
        ElMessageBox.confirm(t('topBar.user.clearCacheTips'), t('common.tips'), {
            confirmButtonText: t('common.confirm'),
            cancelButtonText: t('common.cancel'),
            type: 'warning',
            customClass: 'login-out-dialog',
        }).then(async () => {
            localStorage.clear()
            sessionStorage.clear()

            // 删除当前域名下可由脚本访问的 Cookie，HttpOnly Cookie 由浏览器安全策略保护
            document.cookie.split(';').forEach((cookie) => {
                const name = cookie.split('=')[0]?.trim()
                if (name) document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/`
            })

            if ('caches' in window) {
                const cacheNames = await window.caches.keys()
                await Promise.all(cacheNames.map((name) => window.caches.delete(name)))
            }

            if ('indexedDB' in window && 'databases' in indexedDB) {
                const databases = await indexedDB.databases()
                databases.forEach((database) => {
                    if (database.name) indexedDB.deleteDatabase(database.name)
                })
            }

            window.location.replace(`${window.location.origin}${window.location.pathname}#/auth/login`)
        })
    }, 200)
}

/**
 * 用户登出确认
 */
const loginOut = (): void => {
    closeUserMenu()
    setTimeout(() => {
        ElMessageBox.confirm(t('common.logOutTips'), t('common.tips'), {
            confirmButtonText: t('common.confirm'),
            cancelButtonText: t('common.cancel'),
            customClass: 'login-out-dialog',
        }).then(() => {
            userStore.logOut()
        })
    }, 200)
}

/**
 * 关闭用户菜单弹出层
 */
const closeUserMenu = (): void => {
    setTimeout(() => {
        userMenuPopover.value.hide()
    }, 100)
}
</script>

<style scoped>
@reference '@styles/core/tailwind.css';

@layer components {
    .btn-item {
        @apply flex items-center p-2 mb-3 select-none rounded-md cursor-pointer last:mb-0;

        span {
            @apply text-sm;
        }

        .art-svg-icon {
            @apply mr-2 text-base;
        }

        &:hover {
            background-color: var(--art-gray-200);
        }
    }
}

.log-out {
    @apply py-1.5
    mt-5
    text-xs
    text-center
    border
    border-g-400
    rounded-md
    transition-all
    duration-200
    hover:shadow-xl;
}
</style>
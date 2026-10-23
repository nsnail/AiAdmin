<!-- 版本升级提示：后端版本变化时在右上角弹出浮窗，展示版本号和更新内容，倒计时结束无操作则自动刷新页面升级 -->
<template>
    <Transition name="version-upgrade">
        <div v-if="info" @mouseenter="paused = true" @mouseleave="paused = false" class="version-upgrade">
            <div class="version-upgrade-header">
                <ArtSvgIcon class="text-xl text-theme" icon="ri:rocket-2-line" />
                <span class="version-upgrade-title">{{ t('versionUpgrade.title', { version: info.version }) }}</span>
                <ElButton @click="dismiss" link size="small">
                    <ArtSvgIcon icon="ri:close-line" />
                </ElButton>
            </div>
            <div v-if="info.releaseDate" class="version-upgrade-date">{{ info.releaseDate }}</div>
            <div class="version-upgrade-notes">
                <ReleaseNotes :info="info" />
            </div>
            <div class="version-upgrade-question">{{ t('versionUpgrade.question') }}</div>
            <ElProgress :percentage="percentage" :show-text="false" :stroke-width="4" />
            <div class="version-upgrade-footer">
                <span class="version-upgrade-countdown">
                    {{ paused ? t('versionUpgrade.paused') : t('versionUpgrade.countdown', { seconds: secondsLeft }) }}
                </span>
                <div>
                    <ElButton @click="dismiss" size="small">{{ t('versionUpgrade.later') }}</ElButton>
                    <ElButton @click="upgrade" size="small" type="primary">{{ t('versionUpgrade.upgrade') }}</ElButton>
                </div>
            </div>
        </div>
    </Transition>
</template>

<script lang="ts" setup>
import { useI18n } from 'vue-i18n'
import type { DashboardSystemInfo } from '@/api/dashboard'
import mittBus from '@/utils/sys/mittBus'
import ReleaseNotes from '@/views/dashboard/console/widgets/release-notes.vue'

defineOptions({ name: 'ArtVersionUpgrade' })

/** 自动升级倒计时总时长（毫秒） */
const COUNTDOWN_MS = 5000
/** 倒计时刷新间隔（毫秒） */
const TICK_MS = 100

const { t } = useI18n()
/** 需要升级到的新版本信息，为空时不显示浮窗 */
const info = ref<DashboardSystemInfo | null>(null)
/** 页面加载后首次收到的后端版本，作为判断后端是否升级的基准 */
let baseline: string | null = null
/** 已提示过的版本，避免同一版本重复弹出 */
let notified: string | null = null
const elapsed = ref(0)
/** 鼠标停留在浮窗上时暂停倒计时，视为用户正在操作 */
const paused = ref(false)
let timer: ReturnType<typeof setInterval> | undefined

const percentage = computed(() => Math.min(100, (elapsed.value / COUNTDOWN_MS) * 100))
const secondsLeft = computed(() => Math.max(0, Math.ceil((COUNTDOWN_MS - elapsed.value) / 1000)))

/** 停止倒计时 */
function stopTimer() {
    if (timer) clearInterval(timer)
    timer = undefined
}

/** 升级：刷新整个页面以加载最新前端资源 */
function upgrade() {
    stopTimer()
    window.location.reload()
}

/** 暂不升级：关闭浮窗，同一版本不再提示 */
function dismiss() {
    stopTimer()
    info.value = null
}

/** 开始自动升级倒计时 */
function startTimer() {
    stopTimer()
    elapsed.value = 0
    timer = setInterval(() => {
        if (paused.value) return
        elapsed.value += TICK_MS
        if (elapsed.value >= COUNTDOWN_MS) upgrade()
    }, TICK_MS)
}

/**
 * 处理后端推送的系统信息：首次记录基准版本，之后版本发生变化则弹出升级提示
 * @param latest 后端当前系统信息
 */
function onSystemInfo(latest: DashboardSystemInfo) {
    if (baseline === null) {
        baseline = latest.version
        return
    }
    if (latest.version === baseline || latest.version === notified) return
    notified = latest.version
    paused.value = false
    info.value = latest
    startTimer()
}

onMounted(() => mittBus.on('realtimeSystemInfo', onSystemInfo))
onBeforeUnmount(() => {
    mittBus.off('realtimeSystemInfo', onSystemInfo)
    stopTimer()
})
</script>

<style scoped>
.version-upgrade {
    position: fixed;
    top: 70px;
    right: 16px;
    z-index: 3500;
    width: 360px;
    max-width: calc(100vw - 32px);
    padding: 14px 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: 8px;
    box-shadow: var(--el-box-shadow);
}

.version-upgrade-header {
    display: flex;
    gap: 8px;
    align-items: center;
}

.version-upgrade-title {
    flex: 1;
    font-size: 15px;
    font-weight: 500;
}

.version-upgrade-date {
    margin: 2px 0 0 28px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.version-upgrade-notes {
    max-height: 12rem;
    padding: 8px 0;
    margin: 8px 0;
    overflow-y: auto;
    font-size: 12px;
    border-top: 1px solid var(--el-border-color-lighter);
    border-bottom: 1px solid var(--el-border-color-lighter);
}

.version-upgrade-question {
    margin-bottom: 8px;
    font-size: 13px;
}

.version-upgrade-footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-top: 10px;
}

.version-upgrade-countdown {
    font-size: 12px;
    color: var(--el-text-color-secondary);
}

.version-upgrade-enter-active,
.version-upgrade-leave-active {
    transition:
        opacity 0.25s,
        transform 0.25s;
}

.version-upgrade-enter-from,
.version-upgrade-leave-to {
    opacity: 0;
    transform: translateX(24px);
}
</style>
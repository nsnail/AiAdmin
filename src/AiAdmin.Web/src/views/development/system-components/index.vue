<!-- 系统组件：展示前端 npm 依赖（运行依赖与开发依赖）以及后端运行时、已加载程序集和 NuGet 包，均含版本号 -->
<template>
    <div class="components-page art-full-height">
        <ElCard class="art-table-card">
            <template #header>
                <div class="flex items-center justify-between gap-3">
                    <ElInput v-model="keyword" :placeholder="t('systemComponents.search')" clearable>
                        <template #prefix><ArtSvgIcon icon="ri:search-line" /></template>
                    </ElInput>
                    <ElButton :loading="loading" :title="t('systemComponents.refresh')" @click="loadBackend" circle text>
                        <ArtSvgIcon icon="ri:refresh-line" />
                    </ElButton>
                </div>
            </template>
            <ElTabs v-model="activeTab" class="components-tabs" type="card">
                <ElTabPane :label="t('systemComponents.tabs.frontend')" name="frontend">
                    <div class="split">
                        <section v-for="group in frontendGroups" :key="group.key" class="group">
                            <h4 class="group-title">
                                {{ group.title }}<span class="count">{{ t('systemComponents.count', { count: group.rows.length }) }}</span>
                            </h4>
                            <ArtTable :data="group.rows" height="100%">
                                <ElTableColumn
                                    :label="t('systemComponents.fields.name')"
                                    :sort-method="byName"
                                    min-width="200"
                                    prop="name"
                                    show-overflow-tooltip
                                    sortable>
                                    <template #default="{ row }">
                                        <ElLink
                                            v-if="npmUrl(row.name)"
                                            :href="npmUrl(row.name)"
                                            rel="noopener noreferrer"
                                            target="_blank"
                                            type="primary"
                                            >{{ row.name }}</ElLink
                                        >
                                        <span v-else>{{ row.name }}</span>
                                    </template>
                                </ElTableColumn>
                                <ElTableColumn
                                    :label="t('systemComponents.fields.declared')"
                                    :sort-method="byDeclared"
                                    prop="declared"
                                    sortable
                                    width="140" />
                                <ElTableColumn
                                    :label="t('systemComponents.fields.installed')"
                                    :sort-method="byInstalled"
                                    prop="installed"
                                    sortable
                                    width="140">
                                    <template #default="{ row }">{{ row.installed || '-' }}</template>
                                </ElTableColumn>
                                <ElTableColumn
                                    :label="t('systemComponents.fields.size')"
                                    :sort-method="bySize"
                                    align="right"
                                    prop="sizeBytes"
                                    sortable
                                    width="120">
                                    <template #default="{ row }">{{ formatKb(row.sizeBytes) }}</template>
                                </ElTableColumn>
                            </ArtTable>
                        </section>
                    </div>
                </ElTabPane>
                <ElTabPane :label="t('systemComponents.tabs.backend')" name="backend">
                    <div class="split">
                        <section class="group">
                            <h4 class="group-title">
                                {{ t('systemComponents.backend.assemblies') }}
                                <span class="count">{{ t('systemComponents.count', { count: assemblies.length }) }}</span>
                                <span v-if="components?.runtimeVersion" class="count">{{ components.runtimeVersion }}</span>
                            </h4>
                            <ArtTable :data="assemblies" :loading="loading" height="100%">
                                <ElTableColumn
                                    :label="t('systemComponents.fields.name')"
                                    :sort-method="byName"
                                    min-width="220"
                                    prop="name"
                                    show-overflow-tooltip
                                    sortable>
                                    <template #default="{ row }">
                                        <ElLink
                                            v-if="assemblyUrl(row)"
                                            :href="assemblyUrl(row)"
                                            rel="noopener noreferrer"
                                            target="_blank"
                                            type="primary"
                                            >{{ row.name }}</ElLink
                                        >
                                        <span v-else>{{ row.name }}</span>
                                    </template>
                                </ElTableColumn>
                                <ElTableColumn
                                    :label="t('systemComponents.fields.version')"
                                    :sort-method="byVersion"
                                    prop="version"
                                    sortable
                                    width="140" />
                                <ElTableColumn
                                    :label="t('systemComponents.fields.productVersion')"
                                    :sort-method="byProductVersion"
                                    min-width="140"
                                    prop="productVersion"
                                    show-overflow-tooltip
                                    sortable>
                                    <template #default="{ row }">{{ row.productVersion || '-' }}</template>
                                </ElTableColumn>
                                <ElTableColumn
                                    :label="t('systemComponents.fields.size')"
                                    :sort-method="bySize"
                                    align="right"
                                    prop="sizeBytes"
                                    sortable
                                    width="120">
                                    <template #default="{ row }">{{ formatKb(row.sizeBytes) }}</template>
                                </ElTableColumn>
                                <ElTableColumn :label="t('systemComponents.fields.kind')" :sort-method="byKind" prop="kind" sortable width="120">
                                    <template #default="{ row }">{{ t(`systemComponents.kind.${row.kind}`) }}</template>
                                </ElTableColumn>
                            </ArtTable>
                        </section>
                        <section class="group">
                            <h4 class="group-title">
                                {{ t('systemComponents.backend.packages') }}
                                <span class="count">{{ t('systemComponents.count', { count: packages.length }) }}</span>
                            </h4>
                            <ArtTable :data="packages" :loading="loading" height="100%">
                                <ElTableColumn
                                    :label="t('systemComponents.fields.name')"
                                    :sort-method="byName"
                                    min-width="240"
                                    prop="name"
                                    show-overflow-tooltip
                                    sortable>
                                    <template #default="{ row }">
                                        <ElLink
                                            v-if="nugetUrl(row.name)"
                                            :href="nugetUrl(row.name)"
                                            rel="noopener noreferrer"
                                            target="_blank"
                                            type="primary"
                                            >{{ row.name }}</ElLink
                                        >
                                        <span v-else>{{ row.name }}</span>
                                    </template>
                                </ElTableColumn>
                                <ElTableColumn
                                    :label="t('systemComponents.fields.version')"
                                    :sort-method="byVersion"
                                    prop="version"
                                    sortable
                                    width="160" />
                                <ElTableColumn
                                    :label="t('systemComponents.fields.size')"
                                    :sort-method="bySize"
                                    align="right"
                                    prop="sizeBytes"
                                    sortable
                                    width="120">
                                    <template #default="{ row }">{{ formatKb(row.sizeBytes) }}</template>
                                </ElTableColumn>
                            </ArtTable>
                        </section>
                    </div>
                </ElTabPane>
            </ElTabs>
        </ElCard>
    </div>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { fetchGetSystemComponents, type SystemAssemblyItem, type SystemComponents } from '@/api/system-manage'

defineOptions({ name: 'SystemComponents' })

const { t } = useI18n()
const activeTab = ref('frontend')
const keyword = ref('')
const loading = ref(false)
const components = ref<SystemComponents>()

/**
 * 按名称关键字过滤，不区分大小写
 * @param rows 待过滤的行
 * @returns 名称包含关键字的行
 */
function filterByName<T extends { name: string }>(rows: T[]): T[] {
    const text = keyword.value.trim().toLowerCase()
    return text ? rows.filter((x) => x.name.toLowerCase().includes(text)) : rows
}

const frontendGroups = computed(() => [
    { key: 'runtime', title: t('systemComponents.frontend.runtime'), rows: filterByName(__NPM_PACKAGES__.dependencies) },
    { key: 'dev', title: t('systemComponents.frontend.dev'), rows: filterByName(__NPM_PACKAGES__.devDependencies) },
])
const assemblies = computed(() => filterByName(components.value?.assemblies ?? []))
/** 后端 NuGet 包名称集合，小写，用于判断程序集是否来自 NuGet 包 */
const packageNames = computed(() => new Set((components.value?.packages ?? []).map((x) => x.name.toLowerCase())))

/**
 * npm 包页面地址，作用域包（如 @vueuse/core）保留斜杠
 * @param name 包名称
 * @returns npmjs.com 上的包页面地址
 */
function npmUrl(name: string): string {
    return `https://www.npmjs.com/package/${name}`
}

/**
 * NuGet 包页面地址
 * @param name 包名称
 * @returns nuget.org 上的包页面地址
 */
function nugetUrl(name: string): string {
    return `https://www.nuget.org/packages/${encodeURIComponent(name)}`
}

/**
 * Google 搜索地址
 * @param keyword 搜索关键字
 * @returns Google 搜索结果页地址
 */
function googleUrl(keyword: string): string {
    return `https://www.google.com/search?q=${encodeURIComponent(keyword)}`
}

/**
 * 程序集的外部链接：本项目程序集没有链接，来自 NuGet 包的链到 nuget.org，其余（运行时与共享框架程序集）用 Google 搜索
 * @param row 程序集行
 * @returns 外部链接，不需要链接时为空字符串
 */
function assemblyUrl(row: SystemAssemblyItem): string {
    if (row.kind === 'application') return ''
    return packageNames.value.has(row.name.toLowerCase()) ? nugetUrl(row.name) : googleUrl(`${row.name} .NET assembly`)
}
const packages = computed(() => filterByName(components.value?.packages ?? []))

/**
 * 版本号比较：按点分隔的数字段逐段比较，非数字段按文本比较，预发布版本（含连字符）排在同一正式版本之前，空值排在最前
 * @param a 版本号 a
 * @param b 版本号 b
 * @returns 负数表示 a 在前，正数表示 b 在前，0 表示相等
 */
function compareVersion(a: string | undefined, b: string | undefined): number {
    const left = a ?? ''
    const right = b ?? ''
    if (!left || !right) return Number(!!left) - Number(!!right)
    const [leftMain, leftPre = ''] = left.split(/-(.*)/s)
    const [rightMain, rightPre = ''] = right.split(/-(.*)/s)
    const leftParts = leftMain.split('.')
    const rightParts = rightMain.split('.')
    for (let i = 0; i < Math.max(leftParts.length, rightParts.length); i++) {
        const diff = leftParts[i] === undefined || rightParts[i] === undefined ? 0 : compareSegment(leftParts[i], rightParts[i])
        if (diff !== 0) return diff
        if (leftParts[i] === undefined) return -1
        if (rightParts[i] === undefined) return 1
    }
    if (leftPre === rightPre) return 0
    if (!leftPre) return 1
    if (!rightPre) return -1
    return leftPre.localeCompare(rightPre, undefined, { numeric: true })
}

/**
 * 比较单个版本段，两边都是纯数字时按数值比较，否则按文本比较
 * @param a 版本段 a
 * @param b 版本段 b
 * @returns 比较结果
 */
function compareSegment(a: string, b: string): number {
    return /^\d+$/.test(a) && /^\d+$/.test(b) ? Number(a) - Number(b) : a.localeCompare(b)
}

/**
 * 去掉声明版本前面的范围符号，例如 ^1.2.3、~1.2.3、>=1.2.3 只保留 1.2.3
 * @param declared 声明版本
 * @returns 去掉范围符号后的版本
 */
function stripRange(declared: string): string {
    return declared.replace(/^[\^~><=\s]+/, '')
}

/** 名称列排序，不区分大小写 */
const byName = (a: { name: string }, b: { name: string }) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
/** 声明版本列排序，^、~ 等前缀不参与比较，next、latest 等标签按文本比较 */
const byDeclared = (a: { declared: string }, b: { declared: string }) => compareVersion(stripRange(a.declared), stripRange(b.declared))
/** 安装版本列排序 */
const byInstalled = (a: { installed: string }, b: { installed: string }) => compareVersion(a.installed, b.installed)
/** 程序集版本和包版本列排序 */
const byVersion = (a: { version: string }, b: { version: string }) => compareVersion(a.version, b.version)
/** 产品版本列排序 */
const byProductVersion = (a: { productVersion: string }, b: { productVersion: string }) => compareVersion(a.productVersion, b.productVersion)
/**
 * 把字节数格式化为整数 KB 并带 KB 后缀，带千分位，不足 1 KB 的非空文件按 1 KB 显示；没有数据时显示 -
 * @param bytes 字节数
 * @returns 例如 1,234 KB
 */
function formatKb(bytes: number | null | undefined): string {
    if (bytes == null) return '-'
    const kb = bytes > 0 ? Math.max(1, Math.round(bytes / 1024)) : 0
    return `${kb.toLocaleString()} KB`
}

/** 大小列排序，没有数据的排在最前 */
const bySize = (a: { sizeBytes?: number | null }, b: { sizeBytes?: number | null }) => (a.sizeBytes ?? -1) - (b.sizeBytes ?? -1)
/** 类别列排序，按页面显示的文字排序 */
const byKind = (a: { kind: string }, b: { kind: string }) => t(`systemComponents.kind.${a.kind}`).localeCompare(t(`systemComponents.kind.${b.kind}`))

/** 加载后端程序集与 NuGet 包，失败时保留已有数据 */
async function loadBackend(): Promise<void> {
    loading.value = true
    try {
        components.value = await fetchGetSystemComponents()
    } catch {
        // 请求失败由全局请求拦截器提示
    } finally {
        loading.value = false
    }
}

onMounted(() => void loadBackend())
</script>

<style scoped>
.components-page {
    display: flex;
    flex-direction: column;
}
.components-page > .art-table-card {
    flex: 1;
    min-height: 0;
}
.components-page > .art-table-card :deep(.el-card__body) {
    height: calc(100% - 58px);
}
.components-page :deep(.el-card__header) {
    padding: 14px 16px;
}
.components-page :deep(.el-input) {
    max-width: 420px;
}
.components-tabs {
    display: flex;
    flex-direction: column;
    height: 100%;
}
.components-tabs :deep(.el-tabs__content),
.components-tabs :deep(.el-tab-pane) {
    flex: 1;
    height: 100%;
    min-height: 0;
}
.split {
    display: flex;
    gap: 16px;
    height: 100%;
}
.group {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
}
.group-title {
    margin: 0 0 8px;
    font-size: 14px;
    font-weight: 500;
}
.count {
    margin-left: 8px;
    font-size: 12px;
    font-weight: 400;
    color: var(--el-text-color-secondary);
}
</style>
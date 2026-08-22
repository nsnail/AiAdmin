<template>
    <div class="py-5">
        <h1 class="page-title">统计卡片（文字）</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in statsCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtStatsCard
                    :description="card.description"
                    :icon="card.icon"
                    :iconStyle="card.iconStyle"
                    :showArrow="card.showArrow"
                    :title="card.title" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">统计卡片（数字滚动）</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in statsCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtStatsCard
                    :count="card.count"
                    :decimals="0"
                    :description="card.description"
                    :icon="card.icon"
                    :iconStyle="card.iconStyle"
                    :showArrow="card.showArrow"
                    separator="," />
            </ElCol>
        </ElRow>

        <h1 class="page-title">统计卡片（自定义样式）</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in statsCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtStatsCard
                    :boxStyle="card.boxStyle"
                    :description="card.description"
                    :icon="card.icon"
                    :iconStyle="card.customIconStyle"
                    :showArrow="card.showArrow"
                    :textColor="card.textColor"
                    :title="card.title" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">进度卡片</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in progressCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtProgressCard :color="card.color" :percentage="card.percentage" :title="card.title" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">进度卡片（icon）</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in progressCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtProgressCard
                    :color="card.color"
                    :icon="card.icon"
                    :iconStyle="card.iconStyle"
                    :percentage="card.percentage"
                    :title="card.title" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">图表卡片（小图表）</h1>
        <ElRow :gutter="20">
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtLineChartCard
                    :chartData="[120, 132, 101, 134, 90, 230, 210]"
                    :height="9.5"
                    :isMiniChart="true"
                    :percentage="1.2"
                    :value="2545"
                    date="过去7天"
                    label="新用户" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtBarChartCard
                    :barWidth="'45%'"
                    :chartData="[120, 100, 150, 140, 90, 120, 130]"
                    :height="9.5"
                    :isMiniChart="true"
                    :percentage="-4.15"
                    :value="15480"
                    date="过去 14 天"
                    label="浏览量" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtLineChartCard
                    :chartData="[150, 180, 160, 200, 180, 220, 240]"
                    :height="9.5"
                    :isMiniChart="true"
                    :percentage="1.2"
                    :showAreaColor="true"
                    :value="2545"
                    date="过去 30 天"
                    label="粉丝数" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtDonutChartCard
                    :data="[50, 40]"
                    :height="9.5"
                    :percentage="18"
                    :radius="['50%', '70%']"
                    :value="36358"
                    currentValue="2022"
                    percentageLabel="较去年"
                    previousValue="2021"
                    title="粉丝量" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">图表卡片（大图表）</h1>
        <ElRow :gutter="20">
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtLineChartCard :chartData="[120, 132, 101, 134, 90, 230, 210]" :height="11" :percentage="1.2" :value="2545" label="新用户" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtBarChartCard
                    :chartData="[120, 100, 150, 140, 90, 120, 130, 110]"
                    :height="11"
                    :percentage="-4.15"
                    :value="15480"
                    label="浏览量" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtLineChartCard
                    :chartData="[150, 180, 160, 200, 180, 220, 240]"
                    :height="11"
                    :percentage="1.2"
                    :showAreaColor="true"
                    :value="2545"
                    label="粉丝数" />
            </ElCol>
            <ElCol :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtDonutChartCard
                    :data="[70, 30]"
                    :height="11"
                    :percentage="-18"
                    :value="36358"
                    currentValue="12月"
                    percentageLabel="较2021年"
                    previousValue="11月"
                    title="粉丝量" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">数据列表卡片</h1>
        <ElRow :gutter="20">
            <ElCol :lg="8" :sm="12" :xs="24" class="mb-5">
                <ArtDataListCard :list="dataList" subtitle="今日待处理任务" title="待办事项" />
            </ElCol>
            <ElCol :lg="8" :sm="12" :xs="24" class="mb-5">
                <ArtDataListCard :list="dataList" :maxCount="4" :showMoreButton="true" @more="handleMore" subtitle="近期活动列表" title="最近活动" />
            </ElCol>
            <ElCol :lg="8" :sm="12" :xs="24" class="mb-5">
                <ArtTimelineListCard :list="timelineData" subtitle="2024年12月20日" title="最近交易" />
            </ElCol>
        </ElRow>

        <h1 class="page-title">图片卡片</h1>
        <ElRow :gutter="20">
            <ElCol v-for="card in imageCards" :key="card.id" :md="6" :sm="12" :xs="24" class="mb-5">
                <ArtImageCard
                    :category="card.category"
                    :comments="card.comments"
                    :date="card.date"
                    :imageUrl="card.imageUrl"
                    :readTime="card.readTime"
                    :title="card.title"
                    :views="card.views"
                    @click="handleImageCardClick(card)" />
            </ElCol>
        </ElRow>
    </div>
</template>

<script lang="ts" setup>
import cover1 from '@imgs/cover/img1.webp'
import cover2 from '@imgs/cover/img2.webp'
import cover3 from '@imgs/cover/img3.webp'
import cover4 from '@imgs/cover/img4.webp'

defineOptions({ name: 'TemplateCards' })

/**
 * 统计卡片数据配置
 */
const statsCards = [
    {
        id: 1,
        title: '销售产品',
        count: 1235,
        description: '鞋子、牛仔裤、派对服装、手表',
        icon: 'ri:bar-chart-box-line',
        boxStyle: '!bg-theme/10',
        customIconStyle: '!text-theme !text-3xl',
        iconStyle: 'bg-info',
        textColor: 'var(--theme-color)',
        showArrow: false,
    },
    {
        id: 2,
        title: '活跃用户',
        count: 5000,
        description: '日活跃用户超过5,000+',
        icon: 'ri:account-box-2-line',
        boxStyle: '!bg-warning/10',
        customIconStyle: '!text-warning !text-3xl',
        iconStyle: 'bg-warning',
        textColor: 'var(--art-warning)',
        showArrow: false,
    },
    {
        id: 3,
        title: '总收入',
        count: 35000,
        description: '月收入超过¥350,000+',
        icon: 'ri:money-cny-box-line',
        boxStyle: '!bg-secondary/10',
        customIconStyle: '!text-secondary !text-3xl',
        iconStyle: 'bg-secondary',
        textColor: 'var(--art-secondary)',
        showArrow: false,
    },
    {
        id: 4,
        title: '客户评价',
        count: 4800,
        description: '平均评分4.8/5',
        icon: 'ri:message-3-line',
        boxStyle: '!bg-error/10',
        customIconStyle: '!text-error !text-3xl',
        iconStyle: 'bg-error',
        textColor: 'var(--art-error)',
        showArrow: false,
    },
]

const progressCards = [
    {
        id: 1,
        title: '完成进度',
        percentage: 75,
        color: 'var(--art-success)',
        icon: 'ri:arrow-up-circle-line',
        iconStyle: 'bg-success/12 text-success',
    },
    {
        id: 2,
        title: '项目进度',
        percentage: 65,
        color: 'var(--theme-color)',
        icon: 'ri:twitch-line',
        iconStyle: 'bg-theme/12 text-theme',
    },
    {
        id: 3,
        title: '学习进度',
        percentage: 45,
        color: 'var(--art-error)',
        icon: 'ri:game-line',
        iconStyle: '!bg-error/12 text-error',
    },
    {
        id: 4,
        title: '任务进度',
        percentage: 90,
        color: 'var(--art-secondary)',
        icon: 'ri:flag-2-line',
        iconStyle: 'bg-secondary/12 text-secondary',
    },
]

const imageCards = [
    {
        id: 1,
        imageUrl: cover1,
        title: 'AI技术在医疗领域的创新应用与发展前景',
        category: '社交',
        readTime: '2分钟',
        views: 9125,
        comments: 3,
        date: '12月19日 周一',
    },
    {
        id: 2,
        imageUrl: cover2,
        title: '大数据分析助力企业决策的实践案例',
        category: '技术',
        readTime: '3分钟',
        views: 7234,
        comments: 5,
        date: '12月20日 周二',
    },
    {
        id: 3,
        imageUrl: cover3,
        title: '区块链技术在供应链管理中的应用',
        category: '科技',
        readTime: '4分钟',
        views: 5678,
        comments: 8,
        date: '12月21日 周三',
    },
    {
        id: 4,
        imageUrl: cover4,
        title: '云计算技术发展趋势与未来展望',
        category: '云技术',
        readTime: '5分钟',
        views: 4321,
        comments: 6,
        date: '12月22日 周四',
    },
]

const dataList = [
    {
        title: '新加坡之行',
        status: '进行中',
        time: '5分钟',
        class: 'bg-theme/12 text-theme',
        icon: 'ri:camera-4-line',
    },
    {
        title: '归档数据',
        status: '进行中',
        time: '10分钟',
        class: 'bg-secondary/12 text-secondary',
        icon: 'ri:bar-chart-box-line',
    },
    {
        title: '客户会议',
        status: '待处理',
        time: '15分钟',
        class: 'bg-warning/12 text-warning',
        icon: 'ri:user-3-line',
    },
    {
        title: '筛选任务团队',
        status: '进行中',
        time: '20分钟',
        class: 'bg-error/12 text-error',
        icon: 'ri:account-circle-line',
    },
    {
        title: '发送信封给小王',
        status: '已完成',
        time: '20分钟',
        class: 'bg-success/12 text-success',
        icon: 'ri:message-3-line',
    },
]

const timelineData = [
    {
        time: '上午 09:30',
        status: 'rgb(73, 190, 255)',
        content: '收到 John Doe 支付的 385.90 美元',
    },
    {
        time: '上午 10:00',
        status: 'rgb(54, 158, 255)',
        content: '新销售记录',
        code: 'ML-3467',
    },
    {
        time: '上午 12:00',
        status: 'rgb(103, 232, 207)',
        content: '向 Michael 支付了 64.95 美元',
    },
    {
        time: '下午 14:30',
        status: 'rgb(255, 193, 7)',
        content: '系统维护通知',
        code: 'MT-2023',
    },
    {
        time: '下午 15:45',
        status: 'rgb(255, 105, 105)',
        content: '紧急订单取消提醒',
        code: 'OR-9876',
    },
    {
        time: '下午 17:00',
        status: 'rgb(103, 232, 207)',
        content: '完成每日销售报表',
    },
]

/**
 * 处理查看更多按钮点击
 */
const handleMore = () => {
    ElMessage.info('查看更多')
}

/**
 * 图片卡片类型定义
 */
interface ImageCard {
    id: number
    imageUrl: string
    title: string
    category: string
    readTime: string
    views: number
    comments: number
    date: string
}

/**
 * 处理图片卡片点击事件
 * @param card 卡片数据
 */
const handleImageCardClick = (card: ImageCard) => {
    console.log('点击卡片:', card)
}
</script>

<style scoped>
@reference '@styles/core/tailwind.css';

.page-title {
    @apply my-5 text-xl font-medium first:mt-0;
}
</style>

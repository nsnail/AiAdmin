<template>
    <div class="art-card h-53 p-5 pt-3.5 mb-5 max-sm:mb-4">
        <div class="art-card-header">
            <div class="title">
                <h4 class="text-2xl font-medium">55,231</h4>
                <p>商品总数</p>
            </div>
        </div>

        <ArtBarChart
            :data="[50, 80, 40, 90, 60, 70]"
            :showAxisLabel="false"
            :showAxisLine="false"
            :showSplitLine="false"
            barWidth="18px"
            height="7rem" />
    </div>
</template>

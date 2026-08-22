<template>
    <div class="art-card p-5 pt-3.5 mb-5 h-46 max-sm:mb-4">
        <div class="art-card-header">
            <div class="title">
                <h4 class="text-2xl">
                    <span>12%</span>
                    <ArtSvgIcon class="text-success text-lg ml-1" icon="ri:arrow-right-up-line" />
                </h4>
                <p>增长</p>
            </div>
        </div>

        <ArtLineChart
            :data="[50, 85, 65, 95, 75, 130, 180]"
            :showAreaColor="true"
            :showAxisLabel="false"
            :showAxisLine="false"
            :showSplitLine="false"
            barWidth="16px"
            height="5rem" />
    </div>
</template>
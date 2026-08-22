<!-- 工作台页面 -->
<template>
    <div>
        <CardList></CardList>

        <ElRow :gutter="20">
            <ElCol :lg="10" :md="12" :sm="24">
                <ActiveUser />
            </ElCol>
            <ElCol :lg="14" :md="12" :sm="24">
                <SalesOverview />
            </ElCol>
        </ElRow>

        <ElRow :gutter="20">
            <ElCol :lg="12" :md="24" :sm="24">
                <NewUser />
            </ElCol>
            <ElCol :lg="6" :md="12" :sm="24">
                <Dynamic />
            </ElCol>
            <ElCol :lg="6" :md="12" :sm="24">
                <TodoList />
            </ElCol>
        </ElRow>

        <AboutProject />
    </div>
</template>

<script lang="ts" setup>
import CardList from './modules/card-list.vue'
import ActiveUser from './modules/active-user.vue'
import SalesOverview from './modules/sales-overview.vue'
import NewUser from './modules/new-user.vue'
import Dynamic from './modules/dynamic-stats.vue'
import TodoList from './modules/todo-list.vue'
import AboutProject from './modules/about-project.vue'

defineOptions({ name: 'Console' })
</script>
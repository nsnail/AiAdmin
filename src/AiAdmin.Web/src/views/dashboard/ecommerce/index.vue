<!-- 电子商务页面 -->
<template>
    <div class="ecommerce">
        <ElRow :gutter="20">
            <ElCol :lg="16" :md="24" :sm="24">
                <Banner />
            </ElCol>
            <ElCol :lg="4" :md="12" :sm="12">
                <TotalOrderVolume />
            </ElCol>
            <ElCol :lg="4" :md="12" :sm="12">
                <TotalProducts />
            </ElCol>
        </ElRow>

        <ElRow :gutter="20">
            <ElCol :lg="8" :md="12" :sm="12">
                <SalesTrend />
            </ElCol>
            <ElCol :lg="8" :md="12" :sm="12">
                <SalesClassification />
            </ElCol>
            <ElCol :lg="8" :md="24" :sm="24">
                <ElRow :gutter="20">
                    <ElCol :lg="12" :md="12" :sm="24">
                        <ProductSales />
                    </ElCol>
                    <ElCol :lg="12" :md="12" :sm="24">
                        <SalesGrowth />
                    </ElCol>
                    <ElCol :span="24" class="no-margin-bottom">
                        <CartConversionRate />
                    </ElCol>
                </ElRow>
            </ElCol>
        </ElRow>

        <ElRow :gutter="20">
            <ElCol :lg="8" :md="12" :sm="24">
                <HotCommodity />
            </ElCol>
            <ElCol :lg="8" :md="12" :sm="24">
                <AnnualSales />
            </ElCol>
            <ElCol :lg="8" :md="24" :sm="24">
                <TransactionList />
            </ElCol>
        </ElRow>

        <ElRow :gutter="20">
            <ElCol :lg="8" :md="24">
                <RecentTransaction />
            </ElCol>
            <ElCol :lg="16" :md="24" class="no-margin-bottom">
                <HotProductsList />
            </ElCol>
        </ElRow>
    </div>
</template>

<script lang="ts" setup>
import Banner from './modules/banner.vue'
import TotalOrderVolume from './modules/total-order-volume.vue'
import TotalProducts from './modules/total-products.vue'
import SalesTrend from './modules/sales-trend.vue'
import SalesClassification from './modules/sales-classification.vue'
import TransactionList from './modules/transaction-list.vue'
import HotCommodity from './modules/hot-commodity.vue'
import RecentTransaction from './modules/recent-transaction.vue'
import AnnualSales from './modules/annual-sales.vue'
import ProductSales from './modules/product-sales.vue'
import SalesGrowth from './modules/sales-growth.vue'
import CartConversionRate from './modules/cart-conversion-rate.vue'
import HotProductsList from './modules/hot-products-list.vue'

defineOptions({ name: 'Ecommerce' })
</script>
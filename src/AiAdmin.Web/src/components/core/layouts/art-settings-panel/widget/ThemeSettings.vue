<template>
    <SectionTitle :title="$t('setting.theme.title')" />
    <div class="setting-box-wrap">
        <div v-for="(item, index) in configOptions.themeList" :key="item.theme" @click="switchThemeStyles(item.theme)" class="setting-item">
            <div :class="{ 'is-active': item.theme === systemThemeMode }" class="box">
                <img :src="item.img" />
            </div>
            <p class="name">{{ $t(`setting.theme.list[${index}]`) }}</p>
        </div>
    </div>
</template>

<script lang="ts" setup>
import SectionTitle from './SectionTitle.vue'
import { useSettingStore } from '@/store/modules/setting'
import { useSettingsConfig } from '../composables/useSettingsConfig'
import { useTheme } from '@/hooks/core/useTheme'

const settingStore = useSettingStore()
const { systemThemeMode } = storeToRefs(settingStore)
const { configOptions } = useSettingsConfig()
const { switchThemeStyles } = useTheme()
</script>

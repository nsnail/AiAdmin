<template>
    <div>
        <SectionTitle :title="$t('setting.color.title')" class="mt-10" />
        <div class="-mr-4">
            <div class="flex flex-wrap">
                <div
                    v-for="color in configOptions.mainColors"
                    :key="color"
                    :style="{ background: `${color} !important` }"
                    @click="colorHandlers.selectColor(color)"
                    class="flex items-center justify-center size-[23px] mr-4 mb-2.5 cursor-pointer rounded-full transition-all duration-200 hover:opacity-85">
                    <ArtSvgIcon v-show="color === systemThemeColor" class="text-base !text-white" icon="ri:check-fill" />
                </div>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import SectionTitle from './SectionTitle.vue'
import { useSettingStore } from '@/store/modules/setting'
import { useSettingsConfig } from '../composables/useSettingsConfig'
import { useSettingsHandlers } from '../composables/useSettingsHandlers'
import { storeToRefs } from 'pinia'

const settingStore = useSettingStore()
const { systemThemeColor } = storeToRefs(settingStore)
const { configOptions } = useSettingsConfig()
const { colorHandlers } = useSettingsHandlers()
</script>
<template>
  <div class="referral-page art-full-height">
    <ElCard class="art-table-card">
      <template #header>
        <div class="referral-header">
          <div class="invitation-code">
            <span class="invitation-label">{{ t('referralManagement.invitationCode') }}</span>
            <strong>{{ referralData.invitationCode || '-' }}</strong>
            <ElTooltip :content="t('referralManagement.copy')">
              <ElButton
                text
                circle
                :disabled="!referralData.invitationCode"
                :aria-label="t('referralManagement.copy')"
                @click="copyInvitationCode"
              >
                <ArtSvgIcon icon="ri:file-copy-line" />
              </ElButton>
            </ElTooltip>
          </div>
          <ElTag type="info">{{ t('referralManagement.total', { count: totalCount }) }}</ElTag>
        </div>
      </template>

      <ArtTable
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data="referralData.children"
        :stripe="false"
        :tree-props="{ children: 'children' }"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { ElMessage } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import { useTableColumns } from '@/hooks/core/useTableColumns'
  import { fetchGetReferralTree } from '@/api/system-manage'

  defineOptions({ name: 'MyReferrals' })

  type Referral = Api.SystemManage.ReferralTreeItem
  const { t } = useI18n()
  const loading = ref(false)
  const referralData = reactive<Api.SystemManage.ReferralTreeResult>({
    invitationCode: '',
    children: []
  })

  const countChildren = (items: Referral[]): number =>
    items.reduce((total, item) => total + 1 + countChildren(item.children), 0)

  const totalCount = computed(() => countChildren(referralData.children))
  const { columns } = useTableColumns(() => [
    { prop: 'userName', label: t('referralManagement.fields.userName'), minWidth: 180 },
    { prop: 'email', label: t('referralManagement.fields.email'), minWidth: 220 },
    { prop: 'invitationCode', label: t('referralManagement.fields.invitationCode'), minWidth: 150 }
  ])

  const loadReferrals = async (): Promise<void> => {
    loading.value = true
    try {
      const result = await fetchGetReferralTree()
      referralData.invitationCode = result.invitationCode
      referralData.children = result.children
    } finally {
      loading.value = false
    }
  }

  const copyInvitationCode = async (): Promise<void> => {
    await navigator.clipboard.writeText(referralData.invitationCode)
    ElMessage.success(t('referralManagement.copied'))
  }

  onMounted(loadReferrals)
</script>

<style scoped>
  .referral-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    min-height: 32px;
    gap: 16px;
  }

  .invitation-code {
    display: flex;
    align-items: center;
    min-width: 0;
    gap: 8px;
  }

  .invitation-label {
    color: var(--el-text-color-secondary);
  }

  @media (max-width: 640px) {
    .referral-header {
      align-items: flex-start;
      flex-direction: column;
    }
  }
</style>

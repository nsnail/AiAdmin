<template>
  <ElDrawer
    v-model="visible"
    title="高级查询"
    direction="rtl"
    size="min(680px, 100%)"
    destroy-on-close
  >
    <div class="drawer-content">
      <div class="saved-row">
        <ElSelect
          v-model="selectedSaved"
          clearable
          filterable
          placeholder="已保存查询"
          class="saved-select"
          @change="loadSaved"
        >
          <ElOption
            v-for="item in savedQueries"
            :key="item.id"
            :label="item.isGlobal ? `${item.name} (${globalLabel})` : item.name"
            :value="String(item.id)"
          />
        </ElSelect>
        <ElTooltip content="保存当前查询"
          ><ElButton circle @click="saveQuery"><ArtSvgIcon icon="ri:save-line" /></ElButton
        ></ElTooltip>
        <ElTooltip content="删除已保存查询"
          ><ElButton
            circle
            :disabled="!selectedSaved || (selectedQueryIsGlobal && !isSuperAdmin)"
            @click="deleteSaved"
            ><ArtSvgIcon icon="ri:delete-bin-line" /></ElButton
        ></ElTooltip>
      </div>
      <DynamicQueryGroup v-model="draft" :fields="fields" />
      <section class="preview">
        <div class="preview-title">
          <span>查询 JSON</span>
          <div class="preview-actions">
            <span v-if="jsonError" class="json-error">{{ jsonError }}</span>
            <ElTooltip content="格式化 JSON">
              <ElButton text circle aria-label="格式化 JSON" @click="formatJsonText">
                <ArtSvgIcon icon="ri:code-s-slash-line" />
              </ElButton>
            </ElTooltip>
          </div>
        </div>
        <div class="json-editor" :class="{ 'is-invalid': jsonError }">
          <ArtJsonEditor
            v-model="jsonText"
            aria-label="查询 JSON"
            @input="validateJsonText"
            @blur="syncDraftFromJson(true)"
          />
        </div>
      </section>
    </div>
    <template #footer
      ><ElButton @click="visible = false">取消</ElButton
      ><ElButton type="primary" @click="apply">应用查询</ElButton></template
    >
  </ElDrawer>
</template>

<script setup lang="ts">
  import { ElMessage, ElMessageBox, ElSwitch } from 'element-plus'
  import {
    fetchDeleteSavedQuery,
    fetchGetSavedQueries,
    fetchSaveQuery,
    type SavedQuery
  } from '@/api/system-manage'
  import { useRoute } from 'vue-router'
  import { defineComponent, h } from 'vue'
  import { useI18n } from 'vue-i18n'
  import { useUserStore } from '@/store/modules/user'
  import DynamicQueryGroup from './dynamic-query-group.vue'
  import ArtJsonEditor from '../art-json-editor/index.vue'
  import type { DynamicFilter, DynamicQueryField, QueryGroup, QueryNode } from './types'

  const visible = defineModel<boolean>('visible', { default: false })
  const props = defineProps<{ fields: DynamicQueryField[]; modelValue?: DynamicFilter }>()
  const emit = defineEmits<{ apply: [filter: DynamicFilter | undefined] }>()
  const route = useRoute()
  const { t } = useI18n()
  const userStore = useUserStore()
  const isSuperAdmin = computed(() => userStore.getUserInfo.roles?.includes('R_SUPER') === true)
  const createGlobalSwitch = (state: { value: boolean }) =>
    defineComponent({
      setup: () => () =>
        h(ElSwitch, {
          modelValue: state.value,
          'onUpdate:modelValue': (value: boolean) => (state.value = value),
          activeText: t('table.searchBar.globalQuery')
        })
    })
  const globalLabel = computed(() => t('table.searchBar.globalQuery'))
  const draft = ref<QueryGroup>({ logic: 'And', filters: [] })
  const selectedSaved = ref('')
  const savedQueries = ref<SavedQuery[]>([])
  const selectedQueryIsGlobal = computed(
    () =>
      savedQueries.value.find((item) => String(item.id) === selectedSaved.value)?.isGlobal === true
  )
  const jsonText = ref('{}')
  const jsonError = ref('')
  const highlightRef = ref<HTMLElement>()
  let syncingFromJson = false

  const convertValue = (
    value: unknown,
    field: DynamicQueryField | undefined,
    operator: string
  ): unknown => {
    const values = ['Range', 'DateRange', 'Any', 'NotAny'].includes(operator)
      ? Array.isArray(value)
        ? value
        : String(value ?? '')
            .split(',')
            .map((item) => item.trim())
            .filter(Boolean)
      : undefined
    const convert = (item: unknown) =>
      field?.type === 'number' ? Number(item) : field?.type === 'boolean' ? item === 'true' : item
    return values ? values.map(convert) : convert(value)
  }
  const toFilter = (group: QueryGroup): DynamicFilter | undefined => {
    const filters = group.filters
      .map((node): DynamicFilter | undefined => {
        if (node.kind === 'group') return toFilter(node.group)
        const hasValue = Array.isArray(node.value)
          ? node.value.length > 0
          : node.value !== undefined && node.value !== null && String(node.value).trim() !== ''
        if (!node.field || !node.operator || !hasValue) return undefined
        return {
          field: node.field,
          operator: node.operator,
          value: convertValue(
            node.value,
            props.fields.find((field) => field.field === node.field),
            node.operator
          )
        }
      })
      .filter((node): node is DynamicFilter => Boolean(node))
    return filters.length ? { logic: group.logic, filters } : undefined
  }
  const fromFilter = (filter?: DynamicFilter): QueryGroup => ({
    logic: filter?.logic || 'And',
    filters: (filter?.filters || []).map((item): QueryNode =>
      item.filters?.length
        ? { id: crypto.randomUUID(), kind: 'group', group: fromFilter(item) }
        : {
            id: crypto.randomUUID(),
            kind: 'condition',
            field: item.field || '',
            operator: item.operator || 'Contains',
            value: item.value ?? ''
          }
    )
  })
  const formatFilterJson = (filter?: DynamicFilter) => JSON.stringify(filter, null, 2) ?? '{}'
  // 先转义用户输入，再为 JSON 标记语法类型，避免预览内容被浏览器当作 HTML 执行。
  const highlightedPreview = computed(() => {
    const escaped = jsonText.value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')

    return escaped.replace(
      /(&quot;(?:\\.|[^&])*?&quot;)(?=\s*:)|(&quot;(?:\\.|[^&])*?&quot;)|\b(true|false|null)\b|-?\b\d+(?:\.\d+)?\b/g,
      (match, key, stringValue, literal) => {
        if (key) return `<span class="json-key">${key}</span>`
        if (stringValue) return `<span class="json-string">${stringValue}</span>`
        if (literal === 'true' || literal === 'false')
          return `<span class="json-boolean">${literal}</span>`
        if (literal === 'null') return `<span class="json-null">${literal}</span>`
        return `<span class="json-number">${match}</span>`
      }
    )
  })

  const isDynamicFilter = (value: unknown): value is DynamicFilter => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return false
    const filter = value as Record<string, unknown>
    if (filter.logic !== undefined && filter.logic !== 'And' && filter.logic !== 'Or') return false
    if (filter.filters !== undefined) {
      if (!Array.isArray(filter.filters) || !filter.filters.every(isDynamicFilter)) return false
    }
    if (filter.field !== undefined && filter.field !== null && typeof filter.field !== 'string')
      return false
    if (
      filter.operator !== undefined &&
      filter.operator !== null &&
      typeof filter.operator !== 'string'
    )
      return false
    return true
  }

  const parseJsonText = (): DynamicFilter | undefined => {
    const text = jsonText.value.trim()
    if (!text || text === '{}') return undefined
    try {
      const parsed: unknown = JSON.parse(text)
      if (!isDynamicFilter(parsed)) {
        jsonError.value = 'JSON 不是有效的动态查询结构'
        return undefined
      }
      jsonError.value = ''
      return parsed
    } catch {
      jsonError.value = 'JSON 格式不正确'
      return undefined
    }
  }

  const validateJsonText = () => {
    parseJsonText()
  }
  const formatJsonText = () => {
    const filter = parseJsonText()
    if (jsonError.value) {
      ElMessage.error(jsonError.value)
      return
    }
    jsonText.value = formatFilterJson(filter)
    syncDraftFromJson()
  }
  const syncDraftFromJson = (showError = false): DynamicFilter | undefined => {
    const filter = parseJsonText()
    if (jsonError.value) {
      if (showError) ElMessage.error(jsonError.value)
      return undefined
    }
    syncingFromJson = true
    draft.value = fromFilter(filter)
    nextTick(() => {
      syncingFromJson = false
    })
    return filter
  }
  const setEditorFilter = (filter?: DynamicFilter) => {
    syncingFromJson = true
    draft.value = fromFilter(filter)
    jsonText.value = formatFilterJson(filter)
    jsonError.value = ''
    nextTick(() => {
      syncingFromJson = false
    })
  }
  const syncEditorScroll = (event: Event) => {
    const editor = event.target as HTMLTextAreaElement
    if (highlightRef.value) {
      highlightRef.value.scrollTop = editor.scrollTop
      highlightRef.value.scrollLeft = editor.scrollLeft
    }
  }
  const insertJsonIndent = (event: KeyboardEvent) => {
    const editor = event.target as HTMLTextAreaElement
    const start = editor.selectionStart
    const end = editor.selectionEnd
    jsonText.value = `${jsonText.value.slice(0, start)}  ${jsonText.value.slice(end)}`
    nextTick(() => {
      editor.selectionStart = start + 2
      editor.selectionEnd = start + 2
      validateJsonText()
    })
  }

  watch(
    draft,
    (group) => {
      if (!syncingFromJson) {
        jsonText.value = formatFilterJson(toFilter(group))
        jsonError.value = ''
      }
    },
    { deep: true, immediate: true }
  )
  watch(
    () => props.modelValue,
    (filter) => {
      setEditorFilter(filter)
    },
    { immediate: true }
  )
  const loadSaved = (id?: string | number) => {
    const item = savedQueries.value.find((query) => String(query.id) === String(id))
    if (item) {
      setEditorFilter(item.dynamicFilter)
    }
  }
  const loadSavedQueries = async () => {
    savedQueries.value = await fetchGetSavedQueries(route.path)
  }
  const saveQuery = async () => {
    const dynamicFilter = syncDraftFromJson(true)
    if (!dynamicFilter) return
    const currentQuery = savedQueries.value.find(
      (query) => String(query.id) === selectedSaved.value
    )
    if (currentQuery?.isGlobal && !isSuperAdmin.value) {
      ElMessage.warning(t('table.searchBar.globalQueryReadOnly'))
      return
    }
    const global = ref(currentQuery?.isGlobal === true)
    const { value } = await ElMessageBox({
      title: t('table.searchBar.saveQueryTitle'),
      message: isSuperAdmin.value ? h('div', [h(createGlobalSwitch(global))]) : undefined,
      showInput: true,
      inputValue: currentQuery?.name || '',
      inputPlaceholder: t('table.searchBar.queryNamePlaceholder'),
      inputPattern: /\S+/,
      inputErrorMessage: t('table.searchBar.queryNameRequired'),
      showCancelButton: true
    })
    const saved = await fetchSaveQuery({
      name: value.trim(),
      route: route.path,
      dynamicFilter,
      isGlobal: global.value
    })
    savedQueries.value = [
      saved,
      ...savedQueries.value.filter(
        (item) => String(item.id) !== String(saved.id) && item.name !== saved.name
      )
    ]
    selectedSaved.value = String(saved.id)
  }
  const deleteSaved = async () => {
    if (!selectedSaved.value || (selectedQueryIsGlobal.value && !isSuperAdmin.value)) return
    await fetchDeleteSavedQuery(selectedSaved.value)
    savedQueries.value = savedQueries.value.filter(
      (item) => String(item.id) !== selectedSaved.value
    )
    selectedSaved.value = ''
  }
  const apply = () => {
    const filter = syncDraftFromJson(true)
    if (jsonError.value) return
    emit('apply', filter)
    visible.value = false
  }
  watch(visible, async (isVisible) => {
    if (isVisible) await loadSavedQueries()
  })
  onMounted(loadSavedQueries)
</script>

<style scoped lang="scss">
  .drawer-content {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
  }
  .saved-row {
    display: flex;
    gap: 8px;
    margin-bottom: 16px;
  }
  .saved-select {
    flex: 1;
  }
  .preview {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-height: 280px;
    margin-top: 20px;
    font-size: 13px;
    color: var(--el-text-color-regular);
  }
  .preview-title {
    display: flex;
    min-height: 32px;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
  }
  .preview-actions {
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .json-error {
    color: var(--el-color-danger);
  }
  .json-editor {
    position: relative;
    flex: 1;
    min-height: 240px;
    margin-top: 8px;
    border: 1px solid var(--el-border-color);
    background: var(--el-fill-color-light);

    &.is-invalid {
      border-color: var(--el-color-danger);
    }

    pre,
    textarea {
      position: absolute;
      inset: 0;
      width: 100%;
      height: 100%;
      padding: 12px;
      margin: 0;
      overflow: auto;
      border: 0;
      font:
        13px/1.5 ui-monospace,
        SFMono-Regular,
        Consolas,
        monospace;
      letter-spacing: 0;
      tab-size: 2;
      white-space: pre;
    }

    pre {
      color: var(--el-text-color-primary);
      pointer-events: none;
    }

    textarea {
      z-index: 1;
      resize: none;
      outline: none;
      background: transparent;
      color: transparent;
      caret-color: var(--el-text-color-primary);
      -webkit-text-fill-color: transparent;
    }

    :deep(.json-key) {
      color: var(--el-color-primary);
    }
    :deep(.json-string) {
      color: var(--el-color-success);
    }
    :deep(.json-number) {
      color: var(--el-color-warning);
    }
    :deep(.json-boolean),
    :deep(.json-null) {
      color: var(--el-color-danger);
    }
  }
</style>

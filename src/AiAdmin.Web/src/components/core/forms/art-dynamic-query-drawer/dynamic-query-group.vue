<template>
    <div class="query-group">
        <div class="group-toolbar">
            <ElSegmented v-model="model.logic" :options="logicOptions" size="small" />
            <ElButton @click="addCondition" text type="primary">添加条件</ElButton>
            <ElButton @click="addGroup" text type="primary">添加条件组</ElButton>
            <ElButton v-if="removable" @click="$emit('remove')" text type="danger">删除组</ElButton>
        </div>

        <div v-for="(item, index) in model.filters" :key="item.id" class="query-node">
            <DynamicQueryGroup
                v-if="item.kind === 'group'"
                v-model="item.group"
                :fields="fields"
                @remove="model.filters.splice(index, 1)"
                removable />
            <div v-else class="condition-row">
                <ElSelect v-model="item.field" class="field-select" filterable placeholder="字段">
                    <ElOption v-for="field in fields" :key="field.field" :label="field.label" :value="field.field" />
                </ElSelect>
                <ElSelect v-model="item.operator" class="operator-select" filterable placeholder="操作符">
                    <ElOption v-for="operator in operators" :key="operator.value" :label="operator.label" :value="operator.value" />
                </ElSelect>
                <ElSelect v-if="selectedField(item.field)?.type === 'boolean'" v-model="item.value" class="value-input" filterable placeholder="值">
                    <ElOption label="true" value="true" />
                    <ElOption label="false" value="false" />
                </ElSelect>
                <ElDatePicker
                    v-else-if="selectedField(item.field)?.type === 'date'"
                    v-model="item.value"
                    :type="item.operator === 'DateRange' ? 'datetimerange' : 'datetime'"
                    class="value-input"
                    clearable
                    end-placeholder="结束时间"
                    placeholder="时间"
                    range-separator="至"
                    start-placeholder="开始时间"
                    value-format="YYYY-MM-DDTHH:mm:ss.SSSZ" />
                <ElInput v-else v-model="item.value" class="value-input" placeholder="值，Range/Any 使用逗号分隔" />
                <ElButton @click="model.filters.splice(index, 1)" aria-label="删除条件" circle text type="danger">
                    <ArtSvgIcon icon="ri:delete-bin-line" />
                </ElButton>
            </div>
        </div>
    </div>
</template>

<script lang="ts" setup>
import type { DynamicQueryField, QueryGroup } from './types'

defineOptions({ name: 'DynamicQueryGroup' })

const model = defineModel<QueryGroup>({ required: true })
const props = defineProps<{ fields: DynamicQueryField[]; removable?: boolean }>()
defineEmits<{ remove: [] }>()

const logicOptions = [
    { label: 'AND', value: 'And' },
    { label: 'OR', value: 'Or' },
]
const operators = [
    { label: '包含', value: 'Contains' },
    { label: '不包含', value: 'NotContains' },
    { label: '等于', value: 'Equal' },
    { label: '不等于', value: 'NotEqual' },
    { label: '大于', value: 'GreaterThan' },
    { label: '大于等于', value: 'GreaterThanOrEqual' },
    { label: '小于', value: 'LessThan' },
    { label: '小于等于', value: 'LessThanOrEqual' },
    { label: '范围', value: 'Range' },
    { label: '日期范围', value: 'DateRange' },
    { label: '任一匹配', value: 'Any' },
    { label: '均不匹配', value: 'NotAny' },
    { label: '开头是', value: 'StartsWith' },
    { label: '结尾是', value: 'EndsWith' },
]

const selectedField = (fieldName: string) => props.fields.find((field) => field.field === fieldName)
const addCondition = () =>
    model.value.filters.push({
        id: crypto.randomUUID(),
        kind: 'condition',
        field: '',
        operator: 'Contains',
        value: '',
    })
const addGroup = () =>
    model.value.filters.push({
        id: crypto.randomUUID(),
        kind: 'group',
        group: { logic: 'And', filters: [] },
    })
</script>

<style lang="scss" scoped>
.query-group {
    border-left: 2px solid var(--el-border-color);
    padding: 10px 0 0 12px;
}
.group-toolbar,
.condition-row {
    display: flex;
    gap: 8px;
    align-items: center;
    flex-wrap: wrap;
}
.query-node {
    margin-top: 10px;
}
.field-select {
    width: 150px;
}
.operator-select {
    width: 140px;
}
.value-input {
    flex: 1;
    min-width: 180px;
}
</style>
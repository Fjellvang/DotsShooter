<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  v-if="fieldSchema.typeKind === EGeneratedUiTypeKind.Class && fieldSchema.fields"
  class="tw-space-y-2"
  )
  MCallout(
    v-if="shouldShowPlayersLogicVersionMismatchWarning"
    title="Logic Version Mismatch"
    variant="warning"
    class="tw-mt-3"
    ) Some members are not available in this player's logic version, as such these members are disabled.
  generated-ui-form-dynamic-component(
    v-for="field in editableFields"
    :key="field.fieldName"
    v-bind="props"
    :fieldInfo="field"
    :value="hasValue(newtonsoftCamelCase(field.fieldName)) ? value[newtonsoftCamelCase(field.fieldName)] : (field.default)"
    @input="(newVal: any) => update(newtonsoftCamelCase(field.fieldName), newVal)"
    :fieldPath="fieldPath.length === 0 ? field.fieldName : (fieldPath + '/' + field.fieldName)"
    :contextObj="fieldSchema.useAsContext ? value : contextObj"
    :logicVersion="props.logicVersion"
    :disabled="!isAvailableInLogicVersion(field)"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    )
div(v-else) {{displayName}}: {{value}}
</template>

<script lang="ts" setup>
import { computed, onMounted } from 'vue'

import { MCallout } from '@metaplay/meta-ui-next'

import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import { EGeneratedUiTypeKind, type IGeneratedUiFieldInfo } from '../../generatedUiTypes'
import { isVersionInRange, newtonsoftCamelCase } from '../../generatedUiUtils'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: () => ({}),
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, update: emitUpdate } = useGeneratedUiFieldForm(props, emit)

// TODO: Improve the prop typings to avoid the need for these non-null assertions.
const editableFields = computed(() =>
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  props.fieldSchema.fields!.filter((x: IGeneratedUiFieldInfo) => !x.notEditable)
)
const schemaFields = computed(
  () =>
    new Set(
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      props.fieldSchema.fields!.map((x: any) =>
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        newtonsoftCamelCase(x.fieldName)
      )
    )
)
const valueAsInput = computed(() =>
  Object.fromEntries(Object.entries(props.value).filter(([k, v]) => k === '$type' || schemaFields.value.has(k)))
)

onMounted(() => {
  if (
    Object.entries(props.value).some(([k, v]) => !(k === '$type' || schemaFields.value.has(k))) ||
    Object.entries(props.value).length <= 1
  ) {
    // Update the value after a timeout to avoid two updates messing eachother up
    setTimeout(() => {
      emitUpdate(valueAsInput.value)
    }, 10)
  }
})

const hasValue = (fieldName: string): boolean => {
  return fieldName in props.value && props.value[fieldName] !== undefined
}

const shouldShowPlayersLogicVersionMismatchWarning = computed(() => {
  if (props.isTargetingMultiplePlayers) return false
  if (props.disabled) return false

  return editableFields.value.some((x) => !isAvailableInLogicVersion(x))
})

const isAvailableInLogicVersion = (field: IGeneratedUiFieldInfo): boolean =>
  !props.disabled &&
  isVersionInRange(props.fieldSchema, props.logicVersion) &&
  isVersionInRange(field, props.logicVersion)

const update = (fieldName: string, newValue: any): void => {
  emitUpdate({
    ...valueAsInput.value,
    [fieldName]: newValue,
  })
}
</script>

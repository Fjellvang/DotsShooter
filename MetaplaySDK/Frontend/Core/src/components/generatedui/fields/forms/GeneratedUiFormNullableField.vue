<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Loading indicator
b-skeleton(v-if="!fieldSchema || !valueTypeSchema")

div(
  v-else
  class="tw-rounded-md tw-bg-neutral-100 tw-px-3 tw-py-2.5"
  )
  div(class="tw-flex tw-items-center tw-justify-between tw-font-semibold")
    LabelWithLogicVersion(
      :fieldInfo="fieldInfo"
      :is-header="false"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      ) {{ nullableSwitch ? 'Disable' : 'Enable' }} {{ displayName }}
    MInputSwitch(
      :model-value="nullableSwitch"
      size="small"
      :disabled="disabled"
      @update:model-value="onUpdateNullableToggle"
      )
  generated-ui-form-dynamic-component(
    v-if="nullableSwitch"
    v-bind="props"
    :fieldInfo="{ ...fieldInfo, fieldType: fieldSchema.valueType ?? '' }"
    :value="value"
    :logic-version="logicVersion"
    :disabled="disabled"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    @input="update"
    )
</template>

<script lang="ts" setup>
import { onMounted, ref } from 'vue'

import { MInputSwitch } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import { EGeneratedUiTypeKind } from '../../generatedUiTypes'
import { GetTypeSchemaForTypeName } from '../../getGeneratedUiTypeSchema'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: null,
    default: () => null,
  },
})

/**
 * Model for the toggle switch.
 */
const nullableSwitch = ref(false)

function onUpdateNullableToggle(newValue: boolean): void {
  nullableSwitch.value = newValue
  updateNullableToggle(newValue)
}

const emit = defineEmits(generatedUiFieldFormEmits)

const valueCache = ref()

const valueTypeSchema = ref()

const { displayName, displayHint, update: emitUpdate } = useGeneratedUiFieldForm(props, emit)

function update(newValue: any): void {
  if (newValue === null) {
    emitUpdate(null)
  } else {
    if (valueTypeSchema.value.typeKind === EGeneratedUiTypeKind.Primitive) {
      valueCache.value = newValue
      emitUpdate(newValue)
    } else {
      const typedValue = { ...newValue, $type: valueTypeSchema.value.jsonType }
      valueCache.value = typedValue
      emitUpdate(typedValue)
    }
  }
}

onMounted(async () => {
  valueTypeSchema.value = await GetTypeSchemaForTypeName(props.fieldSchema.valueType ?? '')
  update(props.value)

  if (!props.value) {
    nullableSwitch.value = false
  }
})

/**
 * Updates value to null when toggle is turned on.
 * @param newValue
 */
function updateNullableToggle(newValue: any): void {
  if (newValue) {
    emitUpdate(valueCache.value)
  } else {
    emitUpdate(null)
  }
}
</script>

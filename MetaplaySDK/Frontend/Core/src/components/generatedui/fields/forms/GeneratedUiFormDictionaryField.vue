<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  div.mb-1
    LabelWithLogicVersion(
      :fieldInfo="fieldInfo"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      ) {{ displayName }}
      MTooltip.ml-2(v-if="displayHint" :content="displayHint" noUnderline): MBadge(shape="pill") ?

    div.mb-1(
      v-for="([key, val], idx) in Object.entries(value)"
      :key="idx"
      )
      div
        generated-ui-view-dynamic-component(
          v-if="keyType"
          v-bind="$props"
          :fieldInfo="copyFieldInfoAndOverwrite('Key', keyType, EGeneratedUiTypeKind.Primitive)"
          :value="key"
          :fieldPath="fieldPath + '/$key$/' + key"
          :logic-version="logicVersion"
          :disabled="disabled"
          :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
          )
      div(class="tw-text-right")
        MButton(
          @click="remove(key)"
          variant="danger"
          size="small"
          :disabled-tooltip="disabled ? 'This button has been disabled remotely.' : undefined"
          ) Remove

      div.pl-3.pr-3.pt-3.pb-2.bg-light.rounded.border
        generated-ui-form-dynamic-component(
          v-if="valueType && valueTypeKind"
          v-bind="props"
          :fieldInfo="copyFieldInfoAndOverwrite('Value', valueType, valueTypeKind)"
          :value="val"
          @input="(newVal: any) => updateValue(key, newVal)"
          :fieldPath="fieldPath + '/' + key"
          :logic-version="logicVersion"
          :disabled="disabled"
          :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
          )
  div
    generated-ui-form-dynamic-component(
      v-if="keyType"
      v-bind="props"
      :fieldInfo="copyFieldInfoAndOverwrite('KeyToAdd', keyType, EGeneratedUiTypeKind.Primitive)"
      :value="newKey"
      @input="updateKey"
      :logic-version="logicVersion"
      :disabled="disabled"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      )
    div(class="tw-text-right")
      MButton(
        :disabled-tooltip="disabled ? 'This button has been disabled remotely.' : undefined"
        @click="add"
        size="small"
        ) Add New
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue'

import { MBadge, MButton, MTooltip } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import { EGeneratedUiTypeKind, type IGeneratedUiFieldInfo } from '../../generatedUiTypes'
import { GetTypeSchemaForTypeName } from '../../getGeneratedUiTypeSchema'
import GeneratedUiViewDynamicComponent from '../views/GeneratedUiViewDynamicComponent.vue'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

// Use form props but override default value
const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Object,
    default: () => ({}),
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, displayHint, update: emitUpdate } = useGeneratedUiFieldForm(props, emit)

const keyType = ref('')
const valueType = ref('')
const valueTypeKind = ref<EGeneratedUiTypeKind>()
const newKey = ref<any>(undefined)

onMounted(() => {
  if (!props.fieldInfo.typeParams || props.fieldInfo.typeParams.length < 2) {
    throw new Error('Dictionary field must have at least 2 type params')
  }

  keyType.value = props.fieldInfo.typeParams[0]
  valueType.value = props.fieldInfo.typeParams[1]

  GetTypeSchemaForTypeName(valueType.value)
    .then((schema) => {
      valueTypeKind.value = schema.typeKind
    })
    .catch((error) => {
      console.error(error)
    })
})

function copyFieldInfoAndOverwrite(
  fieldName: string,
  fieldType: string,
  typeKind: EGeneratedUiTypeKind
): IGeneratedUiFieldInfo {
  const newFieldInfo = { ...props.fieldInfo }
  newFieldInfo.fieldName = fieldName
  newFieldInfo.fieldType = fieldType
  newFieldInfo.typeKind = typeKind
  return newFieldInfo
}

function updateKey(key: any): void {
  newKey.value = key
}
function updateValue(key: any, newValue: any): void {
  emitUpdate({
    ...props.value,
    [key]: newValue,
  })
}
function add(): void {
  emitUpdate({
    ...props.value,
    [newKey.value]: undefined,
  })
}
function remove(key: any): void {
  // Copy props.value and filter out the key
  const newVal = Object.fromEntries(Object.entries(props.value).filter(([k, v]) => k !== key))
  emitUpdate(newVal)
}
</script>

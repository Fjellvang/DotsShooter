<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-space-y-2")
  //- List header.
  div
    LabelWithLogicVersion(
      :fieldInfo="fieldInfo"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    ) {{ displayName }}
    p(
      v-if="displayHint"
      class="tw-text-xs tw-text-neutral-500 tw-m-0"
      ) {{ displayHint }}

  //- List items.
  MList(
    class="tw-bg-neutral-50"
    showBorder
    )
    div(v-if="value.length === 0" class="tw-p-3 tw-text-neutral-500 tw-text-xs tw-italic") No {{ displayName?.toLocaleLowerCase() }} added yet.

    div(
      v-for="(val, idx) in value"
      :key="idx"
      class="tw-p-3 tw-space-y-3"
      :data-testid="`${sentenceCaseToKebabCase(displayName ?? 'generated-ui-field')}-form`"
      )
      generated-ui-form-dynamic-component(
        v-if="childArrayType"
        v-bind="props"
        :fieldInfo="arrayFieldInfo"
        :value="val"
        @input="(newVal: any) => update(idx, newVal)"
        :fieldPath="fieldPath + '/' + idx"
        :logic-version="logicVersion"
        :disabled="disabled"
        :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
        )

      div(class="tw-text-right")
        MButton(
          @click="remove(idx)"
          variant="danger"
          size="small"
          data-testid="remove-button"
          :disabled-tooltip="disabled ? 'This button has been disabled remotely.' : undefined"
          ) Remove

  //- Add button.
  div(class="tw-text-right")
    MButton(
      @click="add"
      size="small"
      :data-testid="`add-${sentenceCaseToKebabCase(displayName ?? '')}-button`"
      :disabled-tooltip="disabled ? 'This button has been disabled remotely.' : undefined"
      ) Add {{ displayName }}
</template>

<script lang="ts" setup>
import { ref, onMounted, type PropType, computed } from 'vue'

import { MButton, MList } from '@metaplay/meta-ui-next'
import { sentenceCaseToKebabCase } from '@metaplay/meta-utilities'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'
import { EGeneratedUiTypeKind, type IGeneratedUiFieldInfo } from '../../generatedUiTypes'
import GeneratedUiFormDynamicComponent from './GeneratedUiFormDynamicComponent.vue'

// Use form props but override default value
const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Array as PropType<any[]>,
    default: () => [],
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, displayHint, update: emitUpdate } = useGeneratedUiFieldForm(props, emit)

/**
 * The type of the contained array element. May take some time to load.
 */
const childArrayType = ref<string>()

const arrayFieldInfo = computed<IGeneratedUiFieldInfo>(() => {
  const arrayFieldInfo = { ...props.fieldInfo }
  arrayFieldInfo.fieldName = ''
  arrayFieldInfo.fieldType = childArrayType.value ?? ''
  arrayFieldInfo.typeKind = EGeneratedUiTypeKind.ValueCollection
  return arrayFieldInfo
})

onMounted(() => {
  if (!props.fieldInfo.typeParams) {
    throw new Error('Array field must have type params')
  }
  childArrayType.value = props.fieldInfo.typeParams[0]
})

function update(idx: any, newValue: any): void {
  emitUpdate(
    props.value.map((val: any, i: any) => {
      if (i === idx) {
        return newValue
      }
      return val
    })
  )
}

function add(): void {
  emitUpdate(props.value.concat(undefined))
}

function remove(idx: any): void {
  emitUpdate(props.value.filter((_val, i) => idx !== i))
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- TODO: Consider implementing a number range input component.
LabelWithLogicVersion(
  :fieldInfo="fieldInfo"
  :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
  ) {{ displayName }}
MInputNumber(
  v-if="fieldInfo.fieldTypeHint && fieldInfo.fieldTypeHint.type === 'range'"
  :model-value="value"
  :min="fieldInfo.fieldTypeHint.props.min"
  :max="fieldInfo.fieldTypeHint.props.max"
  :placeholder="formInputPlaceholder"
  :variant="isValid !== undefined ? (isValid ? 'success' : 'danger') : 'default'"
  :hint-message="validationError ? validationError : displayHint"
  :disabled="disabled"
  @update:model-value="update(String($event))"
  :data-testid="dataTestid + '-input'"
  )
MInputNumber(
  v-else
  :model-value="value"
  :placeholder="formInputPlaceholder"
  :variant="isValid !== undefined ? (isValid ? 'success' : 'danger') : 'default'"
  :disabled="disabled"
  :hint-message="validationError ? validationError : displayHint"
  @update:model-value="update(String($event))"
  :data-testid="dataTestid + '-input'"
  )
</template>

<script lang="ts" setup>
import { MInputNumber } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Number,
    default: 0,
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const {
  displayName,
  displayHint,
  formInputPlaceholder,
  isValid,
  validationError,
  update: emitUpdate,
  dataTestid,
} = useGeneratedUiFieldForm(props, emit)

const update = (newValue: string): void => {
  emitUpdate(Number(newValue))
}
</script>

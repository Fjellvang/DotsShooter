<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    ) {{ displayName }}
  MInputTextArea(
    v-if="fieldInfo.fieldTypeHint && fieldInfo.fieldTypeHint.type === 'textArea'"
    :model-value="value"
    :placeholder="formInputPlaceholder"
    :variant="isValid !== undefined ? (isValid ? 'success' : 'danger') : 'default'"
    :hint-message="!isValid ? validationError : displayHint"
    :disabled="disabled"
    @update:model-value="update"
    :data-testid="dataTestid + '-input'"
    )
  MInputText(
    v-else
    :model-value="value"
    :placeholder="formInputPlaceholder"
    :variant="isValid !== undefined ? (isValid ? 'success' : 'danger') : 'default'"
    :hint-message="!isValid ? validationError : displayHint"
    :disabled="disabled"
    @update:model-value="update"
    :data-testid="dataTestid + '-input'"
    )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MInputText, MInputTextArea } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const {
  displayName,
  displayHint,
  formInputPlaceholder,
  isValid: serverIsValid,
  getServerValidationError,
  update,
  dataTestid,
} = useGeneratedUiFieldForm(props, emit)

const notEmptyRule = computed(() => {
  return props.fieldInfo.validationRules
    ? props.fieldInfo.validationRules.find((rule: any) => rule.type === 'notEmpty')
    : null
})

const isValid = computed(() => {
  if (notEmptyRule.value && props.value.length === 0) {
    return false
  } else {
    return serverIsValid.value
  }
})

const validationError = computed((): string | undefined => {
  if (notEmptyRule.value && props.value.length === 0) {
    return notEmptyRule.value.props.message
  } else {
    return (getServerValidationError as any)()
  }
})
</script>

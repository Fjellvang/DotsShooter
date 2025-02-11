<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  LabelWithLogicVersion(
    :fieldInfo="fieldInfo"
    :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
    ) {{ displayName }}
    MTooltip(
      v-if="displayHint"
      :content="displayHint"
      noUnderline
      class="ml-2"
      ): MBadge(shape="pill") ?
  meta-input-select(
    :value="value"
    :options="options"
    :state="isValid"
    no-clear
    :disabled="disabled"
    @input="update"
    :data-testid="dataTestid + '-input'"
    )
  div(
    v-if="!isValid"
    class="tw-text-red-500"
    ) {{ validationError }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MTooltip, MBadge } from '@metaplay/meta-ui-next'

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

const { displayName, displayHint, isValid, validationError, update, dataTestid, useDefault } = useGeneratedUiFieldForm(
  props,
  emit
)

// TODO: Improve the prop typings so we don't need to use non-null assertions.
// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
useDefault('', props.fieldSchema.possibleValues![0])

const options = computed((): Array<MetaInputSelectOption<string>> => {
  if (!props.fieldSchema.possibleValues) {
    return []
  }

  return props.fieldSchema.possibleValues.map((value) => {
    return {
      value,
      id: value,
    }
  })
})
</script>

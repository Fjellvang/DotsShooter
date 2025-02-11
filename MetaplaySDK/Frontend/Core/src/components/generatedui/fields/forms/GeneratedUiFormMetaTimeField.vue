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
  MInputDateTime(
    :model-value="DateTime.fromISO(value)"
    :variant="validationError ? 'danger' : undefined"
    :hint-message="validationError ? validationError : undefined"
    :disabled="disabled"
    @update:model-value="onDateTimeChange"
    :data-testid="dataTestid + '-input'"
    )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'

import { MInputDateTime, MTooltip, MBadge } from '@metaplay/meta-ui-next'

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

/**
 * Utility function to prevent undefined inputs.
 */
function onDateTimeChange(value?: DateTime): void {
  if (!value) return
  update(value.toISO())
}

const { displayName, displayHint, isValid, validationError, update, dataTestid } = useGeneratedUiFieldForm(props, emit)
</script>

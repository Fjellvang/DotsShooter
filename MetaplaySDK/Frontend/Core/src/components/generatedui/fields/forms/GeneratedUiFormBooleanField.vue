<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  div(class="tw-flex tw-items-center tw-justify-between tw-space-x-2")
    LabelWithLogicVersion(
      :fieldInfo="fieldInfo"
      :isTargetingMultiplePlayers="isTargetingMultiplePlayers"
      ) {{ displayName }}

    MInputSwitch(
      :model-value="value"
      size="small"
      :variant="isValid ? 'primary' : 'danger'"
      :disabled="props.disabled"
      @update:model-value="update"
      :data-testid="dataTestid + '-input'"
      )

  p(
    v-if="displayHint"
    class="tw-m-0 tw-text-xs tw-text-neutral-500"
    ) {{ displayHint }}

  div(
    v-if="!isValid"
    class="tw-text-red-500"
    ) {{ validationError }}
</template>

<script lang="ts" setup>
import { onMounted } from 'vue'

import { MInputSwitch } from '@metaplay/meta-ui-next'

import LabelWithLogicVersion from '../../components/LabelWithLogicVersion.vue'
import { generatedUiFieldFormEmits, generatedUiFieldFormProps, useGeneratedUiFieldForm } from '../../generatedFieldBase'

const props = defineProps({
  ...generatedUiFieldFormProps,
  value: {
    type: Boolean,
    default: false,
  },
})

const emit = defineEmits(generatedUiFieldFormEmits)

const { displayName, displayHint, isValid, validationError, update, dataTestid } = useGeneratedUiFieldForm(props, emit)

onMounted(() => {
  update(props.value)
})
</script>

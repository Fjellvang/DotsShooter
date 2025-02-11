<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MInputSingleSelectDropdownEx(
  v-bind="$attrs"
  :model-value="modelValue"
  :label="label"
  :placeholder="placeholder"
  :options="options"
  :disabled="disabled"
  :variant="variant"
  :hint-message="hintMessage"
  :teleport-to-body="teleportToBody"
  :show-clear-button="showClearButton"
  @update:model-value="(event: TValue) => updateSelection(event)"
  :data-testid="dataTestid"
  )
  //- We're using `MInputSingleSelectDropdownEx` as the base components and it has custom rendering slots. We don't
  //- want to expose these slots to the user in this component, so we'll hide them.
  template(#selection)
  template(#option)
</template>

<script setup lang="ts" generic="TValue">
import type { Variant } from '../utils/types'
import type { MInputSingleSelectDropdownExOption } from './MInputSingleSelectDropdownEx.vue'
import MInputSingleSelectDropdownEx from './MInputSingleSelectDropdownEx.vue'

export interface MInputSimpleSelectDropdownOption<TValue> {
  label: string
  value: TValue
  disabled?: boolean
}

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input.
     */
    modelValue: TValue
    /**
     * The collection of items to show in the select.
     */
    options: Array<MInputSimpleSelectDropdownOption<TValue>>
    /**
     * Optional: Show a label for the input.
     */
    label?: string
    /**
     * Optional: Disable the input. Defaults to `false`.
     */
    disabled?: boolean
    /**
     * Optional: Visual variant of the input. Defaults to `neutral`.
     */
    variant?: Variant
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Placeholder text to show in the input. Defaults to "Select option".
     */
    placeholder?: string
    /**
     * Optional: Whether to teleport the options popover to the HTML body. Defaults to `false`.
     * This is an advanced option that can help with z-index issues.
     */
    teleportToBody?: boolean
    /**
     * Optional: Add a button to clear the selection to `undefined`.
     */
    showClearButton?: boolean
    /**
     * Optional: Add a `data-testid` attribute to the dropdown element.
     */
    dataTestid?: string
  }>(),
  {
    label: undefined,
    variant: 'neutral',
    hintMessage: undefined,
    placeholder: 'Select option',
    dataTestid: undefined,
    teleportToBody: false,
  }
)

const emit = defineEmits<{
  'update:modelValue': [TValue]
}>()

function updateSelection(value: TValue): void {
  emit('update:modelValue', value)
}
</script>

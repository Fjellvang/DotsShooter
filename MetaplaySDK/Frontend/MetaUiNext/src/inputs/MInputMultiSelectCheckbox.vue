<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
div
  //- Label.
  div(
    v-if="label"
    role="heading"
    :class="['tw-block tw-text-sm tw-leading-6', { 'tw-text-neutral-400': disabled, 'tw-font-bold tw-mb-0.5': props.size === 'default', 'tw-font-semibold': props.size === 'small' }]"
    ) {{ label }}

  //- Input.
  div(
    v-bind="$attrs"
    :class="['tw-flex tw-gap-0.5', { 'tw-flex-col': props.vertical, 'tw-gap-x-2 tw-flex-wrap': !props.vertical }]"
    )
    MInputCheckbox(
      v-for="option in options"
      :key="option.label"
      :model-value="props.modelValue?.includes(option.value)"
      :disabled="props.disabled || option.disabled"
      :variant="props.variant"
      :size="props.size"
      @update:model-value="(value) => emit('update:modelValue', value ? [...props.modelValue, option.value] : props.modelValue.filter((v) => v !== option.value))"
      :data-testid="`checkbox-${option.label}`"
      ) {{ option.label }}

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts" generic="T extends string | number">
import { watch } from 'vue'

import MInputCheckbox from './MInputCheckbox.vue'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input.
     */
    modelValue: T[]
    /**
     * The collection of items to show in the select.
     */
    options: Array<{ label: string; value: T; disabled?: boolean }>
    /**
     * Optional: Show a label for the input. Defaults to undefined.
     */
    label?: string
    /**
     * Optional: Disable the input. Defaults to false.
     */
    disabled?: boolean
    /**
     * Optional: Visual variant of the input. Defaults to 'default'.
     */
    variant?: 'default' | 'danger' | 'success'
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Stack the radio buttons vertically instead of horizontally. Defaults to false.
     */
    vertical?: boolean
    /**
     * Optional: Set the size of the radio buttons to fit with the surrounding content. Defaults to 'default'.
     */
    size?: 'small' | 'default'
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    hintMessage: undefined,
    size: 'default',
  }
)

const emit = defineEmits<{
  'update:modelValue': [value: T[]]
}>()

// Internal state ------------------------------------------------------------------------------------------------------------

// Watch for prop updates.
watch(
  () => props.options,
  (newValue) => {
    // If there are two options with the same label, throw an error.
    const labels = newValue.map((option) => option.label)
    if (labels.length !== new Set(labels).size) {
      console.warn(
        'Duplicate labels found in the options array of a checkbox group. This is confusing for users. Options: ',
        labels
      )
    }
  }
)
</script>

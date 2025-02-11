<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container. Z-index here sets a new stacking context for the indicator.
div(class="tw-relative tw-z-0 tw-hidden sm:tw-block")
  //- Label.
  label(
    v-if="label"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  //- Body.
  div(
    class="tw-my-1"
    :data-testid="dataTestid"
    )
    //- Switch outline.
    div(
      v-bind="api.getRootProps()"
      :class="['tw-relative tw-mx-0.5 tw-inline-flex tw-px-0.5 tw-rounded-full tw-border', bodyVariantClasses]"
      :style="'box-shadow: inset 0 1px 2px 0 rgba(0,0,0,.1), inset 0 -1px 2px 0 rgba(255,255,255,.8);' + bodySizeStyles"
      )
      div(class="tw-absolute tw-inset-0 -tw-z-20 tw-rounded-full tw-bg-neutral-50")

      //- Selection indicator.
      //- NOTE: The width, height, and left properties are supposed to be set by Zag but this was broken as of 2024-2-15 so we're setting them manually.
      div(
        v-bind="api.getIndicatorProps()"
        :class="['-tw-z-10 tw-rounded-full', indicatorVariantClasses]"
        style="width: var(--width); height: var(--height); left: var(--left); box-shadow: inset 0 -3px 0 0 rgba(0, 0, 0, 0.1), inset 0 2px 0 0 rgba(255, 255, 255, 0.2), 0 1px 3px 0 rgb(0 0 0 / 0.2), 0 1px 2px -1px rgb(0 0 0 / 0.4)"
        )

      //- Switch options.
      div(class="tw-inline-flex tw-flex-shrink-0 tw-space-x-0.5")
        div(
          v-for="option in optionsForRadioGroup"
          :key="option.id"
          v-bind="$attrs"
          )
          //- Note: using `disabled` instead of `internalDisabled` to avoid a visual flicker when the component is first loaded.
          label(
            v-bind="api.getItemProps({ value: option.id })"
            :class="optionClasses(option)"
            :data-testid="`${sentenceCaseToKebabCase(option.label)}-label`"
            )
            span(
              v-bind="api.getItemTextProps({ value: option.id })"
              :data-testid="`${sentenceCaseToKebabCase(option.label)}-span`"
              ) {{ option.label }}
            input(
              v-bind="api.getItemHiddenInputProps({ value: option.id })"
              :data-testid="`${sentenceCaseToKebabCase(option.label)}-input`"
              )

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}

//- Shows a single select dropdown on mobile view only if there are more than 3 options.
MInputSimpleSelectDropdown(
  :model-value="modelValue"
  :options="options"
  :variant="variant"
  :hint-message="hintMessage"
  :disabled="disabled"
  class="sm:tw-hidden"
  @update:model-value="(event) => api.setValue(event)"
  :data-testid="dataTestid"
  )
</template>

<script setup lang="ts" generic="T extends string">
import { isEqual } from 'lodash-es'
import { computed } from 'vue'

import { makeHash, makeIntoUniqueKey, sentenceCaseToKebabCase } from '@metaplay/meta-utilities'

import * as zagRadio from '@zag-js/radio-group'
import type { Context } from '@zag-js/radio-group'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputSimpleSelectDropdown from '../unstable/MInputSimpleSelectDropdown.vue'
import type { Variant } from '../utils/types'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * The current value of the switch.
     */
    modelValue: T
    /**
     * The options to display.
     */
    options: Array<{ label: string; value: T }>
    /**
     * Optional: Disable the switch. Defaults to false.
     */
    disabled?: boolean
    /**
     * Optional: The visual variant of the switch. Defaults to 'primary'.
     */
    variant?: Variant
    /**
     * Optional: The size of the switch. Defaults to 'default'.
     */
    size?: 'small' | 'default'
    /**
     * Optional: Label for the switch.
     */
    label?: string
    /**
     * Optional: Hint message to display under the switch.
     */
    hintMessage?: string
    /**
     * Optional: Add a `data-testid` attribute to the root element.
     */
    dataTestid?: string
  }>(),
  {
    disabled: false,
    variant: 'primary',
    size: 'default',
    label: undefined,
    hintMessage: undefined,
    dataTestid: undefined,
  }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const emit = defineEmits<{
  'update:modelValue': [value: T]
}>()

/**
 * Options with added ids for each option.
 */
const optionsForRadioGroup = computed(() => {
  // Generate IDs for all options.
  const options = props.options.map((option) => ({
    ...option,
    id: makeHash(option.value),
  }))

  // Check for duplicates IDs.
  const uniqueIds = new Set(options.map((option) => option.id))
  if (options.length !== uniqueIds.size) {
    console.error('Duplicate IDs found in options array of MInputSingleSelectSwitch:', options)
  }

  return options
})

/**
 * Helper function to get the value that corresponds to given id.
 * @param id The id of the option.
 */
function getValueFromId(id: string): T {
  const optionFromId = optionsForRadioGroup.value.find((option) => option.id === id)
  return optionFromId ? optionFromId.value : (id as unknown as T)
}

// Zag Switch ---------------------------------------------------------------------------------------------------------

/**
 * The initial value of the radio group.
 */
const initalValue = computed(() => {
  if (typeof props.modelValue === 'object') {
    return optionsForRadioGroup.value.find((option) => isEqual(option.value, props.modelValue))?.id
  } else {
    return optionsForRadioGroup.value.find((option) => option.value === props.modelValue)?.id
  }
})

/**
 * Context values to be passed to the state machine.
 */
const transientContext = computed(
  (): Partial<Context> => ({
    value: initalValue.value,
    disabled: internalDisabled.value,
    name: props.label,
  })
)

/**
 * The state machine for the segmented switch.
 */
const [state, send] = useMachine(
  zagRadio.machine({
    id: makeIntoUniqueKey('segmentedswitch'),
    onValueChange: ({ value }) => {
      emit('update:modelValue', getValueFromId(value))
    },
  }),
  {
    context: transientContext,
  }
)

/**
 * Api object that contains all the props, state, methods and event handlers to interact with the segmented switch.
 */
const api = computed(() => zagRadio.connect(state.value, send, normalizeProps))

// Dropdown -----------------------------------------------------------------------------------------------------------

/**
 * Computed property to determine the variant of the dropdown based on the switch variant.
 */
const dropdownVariants = computed(() => {
  if (props.variant === 'success') return 'success'
  if (props.variant === 'warning') return 'default'
  if (props.variant === 'danger') return 'danger'
  return 'default'
})

// UI visuals ---------------------------------------------------------------------------------------------------------

/**
 * Computed property to set the size of the switch based on the `size` prop.
 */
const bodySizeStyles = computed(() => {
  if (props.size === 'small') return 'padding-bottom: 2px;'
  else return 'padding-top: 2px; padding-bottom: 2px;'
})

/**
 * Helper function to determine the classes for the switch options.
 * @param option The option to determine the classes for.
 */
function optionClasses(option: { label: string; value: T; id: string }): string {
  const baseClasses = `tw-m-0 tw-inline-block tw-rounded-full tw-ring-0 tw-py-1 tw-font-medium tw-transition-colors ${optionSizeClasses.value}`
  const disabledClasses = `tw-text-neutral-500`

  // Determine if the current option is selected
  let isSelected = false
  if (typeof api.value.value === 'object') {
    isSelected = isEqual(api.value.value, option.value)
  } else {
    isSelected = api.value.value === option.id
  }

  // Handle disabled state
  if (internalDisabled.value && isSelected) {
    return `${baseClasses} ${disabledClasses} tw-pointer-events-none`
  }

  if (internalDisabled.value && !isSelected) {
    return `${baseClasses} ${disabledClasses} tw-cursor-not-allowed`
  }

  // Handle selected state
  if (isSelected) {
    return `${baseClasses} tw-cursor-pointer tw-text-white`
  }

  // Default state for non-disabled and non-selected options
  return `${baseClasses} tw-cursor-pointer hover:tw-bg-neutral-200`
}
/**
 * Computed property to set the size of the switch options based on the `size` prop.
 */
const optionSizeClasses = computed(() => {
  if (props.size === 'small') return 'tw-text-xs tw-px-2'
  else return 'tw-text-sm tw-px-3'
})

/**
 * Computed property to set the switch outline color based on the `variant` prop and `disabled` state.
 */
const bodyVariantClasses = computed(() => {
  if (props.variant === 'success') {
    if (props.disabled) return 'tw-border-green-200'
    else return 'tw-border-green-500'
  }

  if (props.variant === 'warning') {
    if (props.disabled) return 'tw-border-orange-200'
    else return 'tw-border-orange-500'
  }

  if (props.variant === 'danger') {
    if (props.disabled) return 'tw-border-red-200'
    else return 'tw-border-red-500'
  }

  if (props.disabled) return 'tw-border-neutral-200'
  else return 'tw-border-neutral-300'
})

/**
 * Computed property to set the indicator color based on the `variant` prop and disabled state.
 */
const indicatorVariantClasses = computed(() => {
  if (props.variant === 'success') {
    if (props.disabled) return 'tw-bg-green-200'
    else return 'tw-bg-green-500'
  }

  if (props.variant === 'warning') {
    if (props.disabled) return 'tw-bg-orange-200'
    else return 'tw-bg-orange-500'
  }

  if (props.variant === 'danger') {
    if (props.disabled) return 'tw-bg-red-200'
    else return 'tw-bg-red-500'
  }

  if (props.disabled) return 'tw-bg-neutral-200'
  else return 'tw-bg-blue-500'
})
</script>

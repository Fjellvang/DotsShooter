<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
span(
  v-bind="api.getRootProps()"
  class="tw-relative"
  )
  //- Label.
  div(
    v-if="label"
    v-bind="api.getLabelProps()"
    role="heading"
    :class="['tw-text-sm tw-leading-6', { 'tw-text-neutral-400': internalDisabled, 'tw-font-bold tw-mb-0.5': props.size === 'default', 'tw-font-semibold': props.size === 'small' }]"
    ) {{ label }}
  //- Input.
  MTooltip(
    :content="tooltipContent"
    no-underline
    :data-testid="'tooltip-' + label"
    )
    span(
      :class="['tw-flex tw-gap-0.5', { 'tw-flex-col': props.vertical, 'tw-gap-x-2 tw-flex-wrap': !props.vertical }]"
      )
      label(
        v-for="option in optionsForRadioGroup"
        :key="option.label"
        v-bind="api.getItemProps({ value: option.id, disabled: option.disabled })"
        :class="['tw-flex tw-items-center tw-gap-x-1 tw-mb-0', { 'tw-cursor-not-allowed': internalDisabled || option.disabled, 'tw-cursor-pointer': !internalDisabled && !option.disabled }]"
        :data-testid="`${sentenceCaseToKebabCase(option.label)}-radio-button`"
        )
        span(
          v-bind="api.getItemControlProps({ value: option.id, disabled: option.disabled })"
          :class="getRadioButtonClasses(option)"
          tabindex="0"
          @keydown.space.prevent="api.setValue(option.id)"
          @keydown.enter.prevent="api.setValue(option.id)"
          :data-testid="`${sentenceCaseToKebabCase(option.label)}-control`"
          )
          //- White circle inside the radio button.
          div(
            v-if="api.value === option.id"
            :class="['tw-bg-white tw-rounded-full', { 'tw-h-1.5 tw-w-1.5': props.size === 'default', 'tw-h-[0.315rem] tw-w-[0.315rem]': props.size === 'small' }]"
            )

        span(
          v-bind="api.getItemTextProps({ value: option.id, disabled: option.disabled })"
          :class="[getlabelVariantClasses(option), 'tw-overflow-hidden tw-overflow-ellipsis']"
          ) {{ option.label }}
        input(
          v-bind="api.getItemHiddenInputProps({ value: option.id, disabled: option.disabled })"
          tabindex="-1"
          :data-testid="`${sentenceCaseToKebabCase(option.label)}-input`"
          )
  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts" generic="T extends string | number">
import { isEqual } from 'lodash-es'
import { computed, watch } from 'vue'

import { makeHash, makeIntoUniqueKey, sentenceCaseToKebabCase } from '@metaplay/meta-utilities'

import * as radio from '@zag-js/radio-group'
import type { Context } from '@zag-js/radio-group'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MTooltip from '../primitives/MTooltip.vue'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

interface OptionForRadioGroup<T> {
  label: string
  value: T
  id: string
  disabled?: boolean
}

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input. Can be undefined.
     */
    modelValue?: T
    /**
     * The collection of items to show in the select.
     */
    options: Array<{ label: string; value: T; disabled?: boolean }>
    /**
     * Optional: Show a label for the input. Defaults to undefined.
     */
    label?: string
    /**
     * Optional: Disable the radio buttons and show a tooltip with the given text.
     */
    disabledTooltip?: string
    /**
     * Optional: Visual variant of the input. Defaults to 'primary'.
     */
    variant?: 'primary' | 'danger' | 'warning' | 'success'
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
    disabledTooltip: undefined,
    variant: 'primary',
    hintMessage: undefined,
    description: undefined,
    size: 'default',
  }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => !!props.disabledTooltip))

/**
 * The tooltip content to show when the radio group is disabled.
 */
const tooltipContent = computed(() => {
  if (internalDisabled.value) return props.disabledTooltip
  return undefined
})

const emit = defineEmits<{
  'update:modelValue': [value: T]
}>()

/**
 * Options for the radio group with unique IDs.
 */
const optionsForRadioGroup = computed(() => props.options.map((option) => ({ ...option, id: makeHash(option.value) })))

/**
 * Helper function to get the value from the id.
 */
function getValueFromId(id: string): T {
  const option = optionsForRadioGroup.value.find((option) => option.id === id)
  if (!option) return props.options[0].value
  else return option.value
}

// Zag ----------------------------------------------------------------------------------------------------------------

/**
 * The initial value of the radio group.
 */
const initalValue = computed(() => {
  if (!props.modelValue) return undefined
  else if (typeof props.modelValue === 'object') {
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
  })
)

/**
 * State machine to handle the radio input.
 */
const [state, send] = useMachine(
  radio.machine({
    id: makeIntoUniqueKey('radio'),
    onValueChange: (details) => {
      emit('update:modelValue', getValueFromId(details.value))
    },
  }),
  {
    context: transientContext,
  }
)

/**
 * Object containing all the props, state, methods and event handlers to interact with the radio inputs.
 */
const api = computed(() => radio.connect(state.value, send, normalizeProps))

/**
 * Watch for prop updates and check for duplicate labels.
 */
watch(
  () => props.options,
  (newValue) => {
    // If there are two options with the same label, throw an error.
    const labels = newValue.map((option) => option.label)
    if (labels.length !== new Set(labels).size) {
      console.warn(
        'Duplicate labels found in the options array of a ragio group. This is confusing for users. Options: ',
        labels
      )
    }
  }
)

// UI Visuals ----------------------------------------------------------------------------------------------------------

/**
 * Helper to get variant specific classes.
 */
function getRadioButtonClasses(option: OptionForRadioGroup<T>): string {
  const baseClasses = 'tw-rounded-full tw-border tw-flex tw-items-center tw-justify-center tw-flex-none'

  const sizeRadiusClasses: Record<string, string> = {
    default: 'tw-h-4 tw-w-4',
    small: 'tw-h-3.5 tw-w-3.5',
  }
  const selectedRadiusSize = sizeRadiusClasses[props.size]

  const variantClasses: Record<string, { enabled: string; disabled: string }> = {
    danger: {
      enabled: 'tw-border-red-500 tw-bg-red-500',
      disabled: 'tw-border-red-300 tw-bg-red-300',
    },
    warning: {
      enabled: 'tw-border-orange-500 tw-bg-orange-500',
      disabled: 'tw-border-orange-300 tw-bg-orange-300',
    },
    success: {
      enabled: 'tw-border-green-500 tw-bg-green-500',
      disabled: 'tw-border-green-300 tw-bg-green-300',
    },
    primary: {
      enabled: 'tw-border-blue-500 tw-bg-blue-500',
      disabled: 'tw-border-blue-300 tw-bg-blue-300',
    },
  }

  // Check if the current option is selected and apply the appropriate variant or neutral classes
  let isSelected = false
  if (typeof props.modelValue === 'object') {
    isSelected = isEqual(props.modelValue, option.value)
  } else isSelected = props.modelValue === option.value

  let selectedVariant = ''
  if (isSelected) {
    if (internalDisabled.value || option.disabled) {
      selectedVariant = variantClasses[props.variant].disabled
    } else {
      selectedVariant = variantClasses[props.variant].enabled
    }
  } else if (option.disabled) {
    selectedVariant = 'tw-border-neutral-300 tw-bg-neutral-200'
  } else {
    selectedVariant = 'tw-border-neutral-300 tw-bg-white hover:tw-bg-neutral-100 active:tw-bg-neutral-200'
  }

  // Return the concatenated class string
  return `${baseClasses} ${selectedRadiusSize} ${selectedVariant}`
}

/**
 * Helper to get variant specific classes for the label.
 */
function getlabelVariantClasses(option: OptionForRadioGroup<T>): string {
  const baseClasses = props.size === 'small' ? 'tw-text-sm' : 'tw-text-base'

  if (option.disabled ?? internalDisabled.value) return `${baseClasses} tw-text-neutral-300`
  if (props.variant === 'danger') return `${baseClasses} tw-text-red-400`
  else return `${baseClasses} tw-text-inherit`
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
label(
  v-bind="api.getRootProps()"
  class="tw-mb-0"
  )
  //- Label.
  div(
    v-if="label"
    v-bind="api.getLabelProps()"
    :class="['tw-block tw-text-sm tw-leading-6', { 'tw-text-neutral-400': internalDisabled, 'tw-font-bold tw-mb-0.5': props.size === 'default', 'tw-font-semibold': props.size === 'small' }]"
    ) {{ label }}

  //- Input.
  //- TODO: Consider keyboard navigation and focus states.
  div(
    :class="['tw-flex tw-items-center', { 'tw-cursor-not-allowed': internalDisabled, 'tw-cursor-pointer': !internalDisabled }]"
    )
    <!-- @vue-ignore - TS is confused about multiple event handlers -->
    div(
      v-bind="{ ...$attrs, ...api.controlProps }"
      :class="['tw-text-white tw-shrink-0 tw-border tw-shadow-inner tw-rounded tw-flex tw-items-center tw-justify-center', checkboxVariantClasses]"
      tabindex="0"
      @keydown.space.prevent="api.toggleChecked()"
      @keydown.enter.prevent="api.toggleChecked()"
      )
      //- Icon from https://heroicons.com/
      <svg v-if="api.checked" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" class="tw-w-4 tw-h-4">
        <path fill-rule="evenodd" d="M12.416 3.376a.75.75 0 0 1 .208 1.04l-5 7.5a.75.75 0 0 1-1.154.114l-3-3a.75.75 0 0 1 1.06-1.06l2.353 2.353 4.493-6.74a.75.75 0 0 1 1.04-.207Z" clip-rule="evenodd" />
      </svg>

    span(:class="['tw-ml-1', descriptionVariantClasses]")
      span(v-if="description") {{ description }}
      <!-- @slot Optional: Descriptive text to be shown next to checkbox.-->
      slot(v-else)

  input(
    v-bind="api.getHiddenInputProps()"
    tabindex="-1"
    )

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts">
import { computed } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as checkbox from '@zag-js/checkbox'
import type { Context } from '@zag-js/checkbox'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input. Can be undefined.
     */
    modelValue: boolean
    /**
     * Optional: Show a label for the input.
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
     * Optional: Text to show next to the checkbox.
     */
    description?: string
    /**
     * Optional: Set the size of the checkbox to fit with the surrounding content. Defaults to 'default'.
     */
    size?: 'small' | 'default'
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    hintMessage: undefined,
    description: undefined,
    size: 'default',
    disabled: false,
  }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

/**
 * Helper to get variant specific classes.
 */
const checkboxVariantClasses = computed(() => {
  const baseClasses = props.size === 'default' ? 'tw-w-4 tw-h-4' : 'tw-w-3.5 tw-h-3.5'

  if (internalDisabled.value) {
    if (api.value.checked) {
      switch (props.variant) {
        case 'danger':
          return `${baseClasses} tw-border-red-200 tw-bg-red-300`
        case 'success':
          return `${baseClasses} tw-border-green-200 tw-bg-green-300`
        default:
          return `${baseClasses} tw-border-neutral-200 tw-bg-neutral-300`
      }
    } else {
      switch (props.variant) {
        case 'danger':
          return `${baseClasses} tw-border-red-200 tw-bg-neutral-50`
        case 'success':
          return `${baseClasses} tw-border-green-200 tw-bg-neutral-50`
        default:
          return `${baseClasses} tw-border-neutral-200 tw-bg-neutral-50`
      }
    }
  }

  if (api.value.checked) {
    switch (props.variant) {
      case 'danger':
        return `${baseClasses} tw-border-red-400 tw-bg-red-500`
      case 'success':
        return `${baseClasses} tw-border-green-500 tw-bg-green-500`
      default:
        return `${baseClasses} tw-border-blue-300 tw-bg-blue-500`
    }
  } else {
    switch (props.variant) {
      case 'danger':
        return `${baseClasses} tw-border-red-400 tw-bg-neutral-50`
      case 'success':
        return `${baseClasses} tw-border-green-500 tw-bg-neutral-50`
      default:
        return `${baseClasses} tw-border-neutral-300 tw-bg-neutral-50`
    }
  }
})

/**
 * Helper to get variant specific classes.
 */
const descriptionVariantClasses = computed(() => {
  const baseClasses = props.size === 'small' ? 'tw-text-sm' : 'tw-text-base'

  if (internalDisabled.value) {
    if (props.variant === 'danger') return `${baseClasses} tw-text-red-300`
    else return `${baseClasses} tw-text-neutral-400`
  }
  if (props.variant === 'danger') return `${baseClasses} tw-text-red-400`
  else return `${baseClasses} tw-text-inherit`
})

// Zag Checkbox -------------------------------------------------------------------------------------------------------

const transientContext = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    checked: props.modelValue,
  })
)

const [state, send] = useMachine(
  checkbox.machine({
    id: makeIntoUniqueKey('checkbox'),
    onCheckedChange: (details) => {
      emit('update:modelValue', details.checked !== false)
    },
  }),
  {
    context: transientContext,
  }
)

const api = computed(() => checkbox.connect(state.value, send, normalizeProps))
</script>

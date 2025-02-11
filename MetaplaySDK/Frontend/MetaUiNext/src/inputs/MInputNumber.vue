<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
div(
  v-bind="api.getRootProps()"
  class="tw-w-full"
  )
  //- Label.
  label(
    v-if="label"
    v-bind="api.getLabelProps()"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  //- Input.
  div(class="tw-relative")
    input(
      v-bind="{ ...$attrs, ...api.getInputProps() }"
      :placeholder="placeholder"
      :class="['tw-w-full tw-rounded-md tw-border-0 tw-py-1.5 tw-text-neutral-900 tw-shadow-sm tw-ring-1 tw-ring-inset placeholder:tw-text-neutral-400 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 sm:tw-text-sm sm:tw-leading-6 disabled:tw-cursor-not-allowed disabled:tw-bg-neutral-50 disabled:tw-text-neutral-500 disabled:ring-neutral-200', variantClasses]"
      :aria-invalid="variant === 'danger'"
      :aria-describedby="hintId"
      )

    button(
      v-bind="api.getIncrementTriggerProps()"
      :class="['tw-absolute tw-top-0.5 tw-right-2 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || api.getIncrementTriggerProps().disabled }]"
      )
      //- Icon from https://heroicons.com/
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
        <path fill-rule="evenodd" d="M14.77 12.79a.75.75 0 01-1.06-.02L10 8.832 6.29 12.77a.75.75 0 11-1.08-1.04l4.25-4.5a.75.75 0 011.08 0l4.25 4.5a.75.75 0 01-.02 1.06z" clip-rule="evenodd" />
      </svg>

    //div(v-bind="api.scrubberProps")

    button(
      v-bind="api.getDecrementTriggerProps()"
      :class="['tw-absolute tw-bottom-0.5 tw-right-2 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || api.getDecrementTriggerProps().disabled }]"
      )
      //- Icon from https://heroicons.com/
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
        <path fill-rule="evenodd" d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z" clip-rule="evenodd" />
      </svg>

    //- Icon.
    div(class="tw-pointer-events-none tw-absolute tw-inset-y-0 tw-right-4 tw-flex tw-items-center tw-space-x-0.5 tw-pr-3")
      //- Icons from https://heroicons.com/
      <svg v-if="(modelValue !== undefined && modelValue !== null) && (allowUndefined || clearOnZero)" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" class="tw-size-4 tw-fill-neutral-50 tw-p-0.5 tw-rounded-full tw-bg-neutral-400 hover:tw-bg-neutral-500 active:tw-bg-neutral-600 tw-cursor-pointer" @click="api.clearValue()">
        <path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" />
      </svg>
      <svg v-if="variant === 'danger'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-red-500" fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 10a1 1 0 100-2 1 1 0 000 2z" clip-rule="evenodd" />
      </svg>

      <svg v-else-if="variant === 'warning'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" fill="currentColor" class="tw-size-4 tw-mr-0.5 tw-text-orange-500" aria-hidden="true">
      <!-- Font Awesome Free 6.7.1 - https://fontawesome.com License - https://fontawesome.com/license/free Copyright 2024 Fonticons, Inc. -->
        <path d="M256 32c14.2 0 27.3 7.5 34.5 19.8l216 368c7.3 12.4 7.3 27.7 .2 40.1S486.3 480 472 480L40 480c-14.3 0-27.6-7.7-34.7-20.1s-7-27.8 .2-40.1l216-368C228.7 39.5 241.8 32 256 32zm0 128c-13.3 0-24 10.7-24 24l0 112c0 13.3 10.7 24 24 24s24-10.7 24-24l0-112c0-13.3-10.7-24-24-24zm32 224a32 32 0 1 0 -64 0 32 32 0 1 0 64 0z"/>
      </svg>

      <svg v-else-if="variant === 'success'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-green-500" fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clip-rule="evenodd" />
      </svg>

  //- Hint message.
  MInputHintMessage(
    :id="hintId"
    :variant="variant"
    ) {{ hintMessage }}
</template>

<script setup lang="ts">
import { computed, watch } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as numberInput from '@zag-js/number-input'
import type { Context } from '@zag-js/number-input'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * Input value. Can be undefined.
     */
    modelValue?: number
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
    variant?: 'default' | 'danger' | 'success' | 'warning'
    /**
     * Optional: Minimum number allowed.
     */
    min?: number
    /**
     * Optional: Maximum number allowed.
     */
    max?: number
    /**
     * Optional: The amount to increment or decrement the value by.
     * Defaults to 1.
     */
    step?: number
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Placeholder text to show in the input. Defaults to 'Enter a number...'
     */
    placeholder?: string
    /**
     * Optional: Allow undefined input values. Defaults to false.
     */
    allowUndefined?: boolean
    /**
     * Optional: Instead of 0 (zero) input, clear the form and show the placeholder text. This implicitly also allows undefined input values. Defaults to false.
     */
    clearOnZero?: boolean
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    min: undefined,
    max: undefined,
    step: 1,
    hintMessage: undefined,
    placeholder: 'Enter a number...',
  }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const emit = defineEmits<{
  'update:modelValue': [value?: number]
}>()

const hintId = makeIntoUniqueKey('hint')

const variantClasses = computed(() => {
  switch (props.variant) {
    case 'danger':
      return 'tw-ring-red-400 tw-text-red-400'
    case 'success':
      return 'tw-ring-green-500'
    default:
      return 'tw-ring-neutral-300'
  }
})

// TODO: Add validation for the constraints (e.g. ensure that min < max)

watch(
  [(): number => props.step, (): number | undefined => props.min, (): number | undefined => props.max],
  ([newStep, newMin, newMax]) => {
    if (newStep === undefined || newMin === undefined || newMax === undefined) {
      return
    }
    const range = Math.max(Math.abs(newMin), Math.abs(newMax)) - Math.min(Math.abs(newMin), Math.abs(newMax))
    if (range <= newStep) {
      console.warn(
        `The step size of ${newStep} ${range < newStep ? 'exceeds' : 'equals'} the ${newMin}..${newMax} range of ${range}. Consider using a toggle instead.`
      )
    }
  },
  { immediate: true }
)

// Zag machine options ------------------------------------------------------------------------------------------------

const transientContext = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    value: props.modelValue ? String(props.modelValue) : undefined,
    min: props.min,
    max: props.max,
    step: props.step,
  })
)

const [state, send] = useMachine(
  numberInput.machine({
    // Unique situation where disabled prop needs to be initialized into machine in addition to the context above.
    disabled: internalDisabled.value,
    id: makeIntoUniqueKey('number'),
    allowMouseWheel: true,
    focusInputOnChange: true,
    onFocusChange: (newValue) => {
      // If the input is empty and the `clearOnZero` flag is not set and undefined values are not allowed, reset to 0.
      if (isNaN(newValue.valueAsNumber) && !props.clearOnZero && !props.allowUndefined) {
        api.value.setValue(0)
        emit('update:modelValue', 0)
        // If the input is 0 and the `clearOnZero` flag is set, clear the input.
      } else if (newValue.valueAsNumber === 0 && props.clearOnZero) {
        api.value.clearValue()
        // If the input is not a number and undefined values are allowed, emit `undefined`.
      } else if (isNaN(newValue.valueAsNumber) && (props.clearOnZero || props.allowUndefined)) {
        emit('update:modelValue', undefined)
        // If the input is smaller than the minimum, set it to the minimum.
      } else if (props.min && newValue.valueAsNumber < props.min) {
        api.value.setToMin()
        // If the input is larger than the maximum, set it to the maximum.
      } else if (props.max && newValue.valueAsNumber > props.max) {
        api.value.setToMax()
        // Otherwise, emit the new number.
      } else {
        emit('update:modelValue', newValue.valueAsNumber)
      }
    },
  }),
  {
    context: transientContext,
  }
)

const api = computed(() => numberInput.connect(state.value, send, normalizeProps))

// Watch the modelValue and update the machine context.
watch(
  () => props.modelValue,
  (newValue) => {
    if (newValue !== undefined) api.value.setValue(newValue)
    else api.value.clearValue()
  }
)
</script>

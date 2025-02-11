<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  label(
    v-if="label"
    :for="id"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  div(
    :class="['tw-flex tw-items-center tw-max-w-fit tw-rounded-md tw-text-neutral-900 tw-shadow-sm tw-ring-1 tw-ring-inset sm:tw-text-sm sm:tw-leading-6', { 'ring-neutral-200 tw-bg-neutral-50 tw-cursor-not-allowed': internalDisabled, 'tw-bg-white': !internalDisabled }, variantClasses]"
    )
    //- Hours.
    div(class="tw-relative")
      input(
        v-bind="apiHours.getInputProps()"
        class="tw-max-w-14 tw-rounded-md tw-border-0 tw-bg-transparent tw-py-1.5 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 disabled:tw-cursor-not-allowed disabled:tw-text-neutral-500"
        :aria-invalid="variant === 'danger' ? true : undefined"
        :aria-describedby="hintId"
        )

      //- Input arrow up.
      button(
        v-bind="apiHours.getIncrementTriggerProps()"
        :class="['tw-absolute tw-top-0.5 tw-right-1 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || apiHours.getIncrementTriggerProps().disabled }]"
        )
        //- Icon from https://heroicons.com/
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
          <path fill-rule="evenodd" d="M14.77 12.79a.75.75 0 01-1.06-.02L10 8.832 6.29 12.77a.75.75 0 11-1.08-1.04l4.25-4.5a.75.75 0 011.08 0l4.25 4.5a.75.75 0 01-.02 1.06z" clip-rule="evenodd" />
        </svg>

      button(
        v-bind="apiHours.getDecrementTriggerProps()"
        :class="['tw-absolute tw-bottom-0.5 tw-right-1 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || apiHours.getDecrementTriggerProps().disabled }]"
        )
        //- Icon from https://heroicons.com/
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
          <path fill-rule="evenodd" d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z" clip-rule="evenodd" />
        </svg>

    div(class="tw-relative tw-bottom-[1px] tw-text-lg tw-text-neutral-400") :

    //- Minutes.
    div(class="tw-relative")
      input(
        v-bind="apiMinutes.getInputProps()"
        class="tw-max-w-14 tw-rounded-md tw-border-0 tw-bg-transparent tw-py-1.5 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 disabled:tw-cursor-not-allowed disabled:tw-text-neutral-500"
        :aria-invalid="variant === 'danger' ? true : undefined"
        :aria-describedby="hintId"
        )

      //- Input arrow up.
      button(
        v-bind="apiMinutes.getIncrementTriggerProps()"
        :class="['tw-absolute tw-top-0.5 tw-right-1 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || apiMinutes.getIncrementTriggerProps().disabled }]"
        )
        //- Icon from https://heroicons.com/
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
          <path fill-rule="evenodd" d="M14.77 12.79a.75.75 0 01-1.06-.02L10 8.832 6.29 12.77a.75.75 0 11-1.08-1.04l4.25-4.5a.75.75 0 011.08 0l4.25 4.5a.75.75 0 01-.02 1.06z" clip-rule="evenodd" />
        </svg>

      button(
        v-bind="apiMinutes.getDecrementTriggerProps()"
        :class="['tw-absolute tw-bottom-0.5 tw-right-1 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 tw-pointer-events-none': internalDisabled || apiMinutes.getDecrementTriggerProps().disabled }]"
        )
        //- Icon from https://heroicons.com/
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
          <path fill-rule="evenodd" d="M5.23 7.21a.75.75 0 011.06.02L10 11.168l3.71-3.938a.75.75 0 111.08 1.04l-4.25 4.5a.75.75 0 01-1.08 0l-4.25-4.5a.75.75 0 01.02-1.06z" clip-rule="evenodd" />
        </svg>

  //- Hint message.
  MInputHintMessage(
    :id="hintId"
    :variant="variant"
    ) {{ hintMessage }}
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { computed, watch } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as numberInput from '@zag-js/number-input'
import type { Context } from '@zag-js/number-input'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputHintMessage from './MInputHintMessage.vue'

const props = defineProps<{
  /**
   * Current value of the input as a ISO 8601 compatible time string.
   * @example '18:56'
   */
  modelValue: string
  /**
   * Optional: Show a label for the input.
   */
  label?: string
  /**
   * Optional: Hint message to show below the input.
   */
  hintMessage?: string
  /**
   * Optional: Disables input to the picker.
   */
  disabled?: boolean
  /**
   * Optional: Minimum time allowed as an ISO 8601 compatible time string. 'utcNow' sets this value to the current time.
   * @example '18:56'
   */
  // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents -- `string` and `utcNow` are overlapping types.
  minIsoTime?: string | 'utcNow'
  /**
   * Optional: Maximum time allowed as an ISO 8601 compatible time string. 'utcNow' sets this value to the current time.
   * @example '18:56'
   */
  // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents -- `string` and `utcNow` are overlapping types.
  maxIsoTime?: string | 'utcNow'
  /**
   * Optional: Visual variant of the input. Defaults to 'neutral'.
   */
  variant?: 'neutral' | 'danger' | 'success'
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

function emitNewTime(newTime: DateTime): void {
  emit(
    'update:modelValue',
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    newTime.toISOTime({ suppressSeconds: true, includeOffset: false })!
  )
}

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))
const id = makeIntoUniqueKey('date')
const hintId = makeIntoUniqueKey('date-input-hint')

const selectedHour = computed(() => {
  return DateTime.fromISO(props.modelValue).hour
})

const selectedMinute = computed(() => {
  return DateTime.fromISO(props.modelValue).minute
})

const minTime = computed(() => {
  if (!props.minIsoTime) return undefined
  if (props.minIsoTime === 'utcNow') {
    return DateTime.utc().startOf('minute')
  } else {
    return DateTime.fromISO(props.minIsoTime)
  }
})

const maxTime = computed(() => {
  if (!props.maxIsoTime) return undefined
  if (props.maxIsoTime === 'utcNow') {
    return DateTime.utc().endOf('minute')
  } else {
    return DateTime.fromISO(props.maxIsoTime)
  }
})

// Visuals ----------------------------------------------------------------------------------------------
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

// Zag hours input ------------------------------------------------------------------------------------------------
const minHour = computed(() => {
  if (minTime.value) {
    return minTime.value.hour
  } else {
    return 0
  }
})

const maxHour = computed(() => {
  if (maxTime.value) {
    return maxTime.value.hour
  } else {
    return 23
  }
})

const transientContext = computed(() => ({
  disabled: internalDisabled.value,
  value: String(selectedHour.value),
  min: minHour.value,
  max: maxHour.value,
}))

const [stateHours, sendHours] = useMachine(
  numberInput.machine({
    disabled: internalDisabled.value,
    id: makeIntoUniqueKey('time-hours'),
    allowMouseWheel: true,
    inputMode: 'numeric',
    formatOptions: {
      maximumFractionDigits: 0,
      minimumIntegerDigits: 2,
    },
    focusInputOnChange: true,
    onFocusChange: (newValue) => {
      // If the input is empty, reset to the previous value.
      if (isNaN(newValue.valueAsNumber)) {
        apiHours.value.setValue(selectedHour.value)
        // Otherwise, emit the new number.
      } else {
        emitNewTime(
          DateTime.fromObject({
            hour: newValue.valueAsNumber,
            minute: selectedMinute.value,
          })
        )
      }
    },
  }),
  {
    context: transientContext,
  }
)

const apiHours = computed(() => numberInput.connect(stateHours.value, sendHours, normalizeProps))

// Zag minutes input ------------------------------------------------------------------------------------------------
const minMinute = computed(() => {
  if (minTime.value && selectedHour.value === minHour.value) {
    return minTime.value.minute
  } else {
    return 0
  }
})

const maxMinute = computed(() => {
  if (maxTime.value && selectedHour.value === maxHour.value) {
    return maxTime.value.minute
  } else {
    return 59
  }
})

const transientContextMinutes = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    value: String(selectedMinute.value),
    min: minMinute.value,
    max: maxMinute.value,
  })
)

const [stateMinutes, sendMinutes] = useMachine(
  numberInput.machine({
    disabled: internalDisabled.value,
    id: makeIntoUniqueKey('time-minutes'),
    allowMouseWheel: true,
    inputMode: 'numeric',
    formatOptions: {
      maximumFractionDigits: 0,
      minimumIntegerDigits: 2,
    },
    onFocusChange: (newValue) => {
      // If the input is empty, reset to the previous value.
      if (isNaN(newValue.valueAsNumber)) {
        apiMinutes.value.setValue(selectedMinute.value)
        // Otherwise, emit the new number.
      } else {
        emitNewTime(
          DateTime.fromObject({
            hour: selectedHour.value,
            minute: newValue.valueAsNumber,
          })
        )
      }
    },
  }),
  {
    context: transientContextMinutes,
  }
)

// Re-sync the minutes input when the hour changes as that might change the min/max values.
watch(selectedHour, (newValue) => {
  if (newValue === minHour.value && selectedMinute.value < minMinute.value) {
    emitNewTime(DateTime.fromObject({ hour: newValue, minute: minMinute.value }))
  }
  if (newValue === maxHour.value && selectedMinute.value > maxMinute.value) {
    emitNewTime(DateTime.fromObject({ hour: newValue, minute: maxMinute.value }))
  }
})
const apiMinutes = computed(() => numberInput.connect(stateMinutes.value, sendMinutes, normalizeProps))
</script>

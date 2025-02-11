<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- The component is in a visual container if empty value is allowed.
div(
  class="tw-@container"
  :class="{ 'tw-bg-neutral-100 tw-rounded-md tw-border tw-border-neutral-200 tw-p-3': allowEmpty }"
  )
  div(class="tw-flex tw-items-center tw-justify-between")
    label(
      v-if="label"
      :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': disabled, 'tw-text-neutral-900': !disabled }]"
      ) {{ label }}

    div(
      v-if="allowEmpty"
      class="tw-flex tw-items-center tw-space-x-2"
      )
      label(
        class="tw-mb-0 tw-text-xs"
        :class="{ 'tw-text-neutral-400': disabled, 'tw-text-neutral-500': !disabled }"
        ) Enable
      MInputSwitch(
        :model-value="internalValueSelectionEnabled"
        :disabled="disabled"
        size="small"
        @update:model-value="onInternalValueSelectionEnabledChanged"
        )

  div(v-if="!allowEmpty || internalValueSelectionEnabled")
    div(
      class="tw-flex tw-flex-col tw-space-y-2 @sm:tw-flex-row @sm:tw-space-x-4 @sm:tw-space-y-0"
      :class="variantClasses"
      )
      div(class="tw-flex tw-grow tw-items-baseline tw-space-x-2")
        label(
          :for="idDays"
          :class="['tw-text-sm', { 'tw-text-red-500': !valid, 'tw-text-neutral-400': disabled }]"
          ) Days:
        MInputNumber(
          class="tw-grow"
          name="days"
          :id="idDays"
          :model-value="selectedDays"
          :min="0"
          :max-fraction-digits="5"
          :variant="variant === 'default' ? (!valid ? 'danger' : 'default') : variant"
          :disabled="disabled"
          placeholder="0-365"
          @update:model-value="selectedDays = Number($event)"
          )

      div(class="tw-flex tw-grow tw-items-baseline tw-space-x-2")
        label(
          :for="idHours"
          :class="['tw-text-sm', { 'tw-text-red-500': !valid, 'tw-text-neutral-400': disabled }]"
          ) Hours:
        MInputNumber(
          class="tw-grow"
          name="hours"
          :id="idHours"
          :model-value="selectedHours"
          :min="0"
          :max-fraction-digits="5"
          :variant="variant === 'default' ? (!valid ? 'danger' : 'default') : variant"
          :disabled="disabled"
          placeholder="0-24"
          @update:model-value="selectedHours = Number($event)"
          )

      div(class="tw-flex tw-grow tw-items-baseline tw-space-x-2")
        label(
          :for="idMinutes"
          :class="['tw-text-sm', { 'tw-text-red-500': !valid, 'tw-text-neutral-400': disabled }]"
          ) Minutes:
        MInputNumber(
          class="tw-grow"
          name="hours"
          :id="idMinutes"
          :model-value="selectedMinutes"
          :min="0"
          :max-fraction-digits="5"
          :variant="variant === 'default' ? (!valid ? 'danger' : 'default') : variant"
          :disabled="disabled"
          placeholder="0-60"
          @update:model-value="selectedMinutes = Number($event)"
          )

    div(
      v-if="!valid"
      class="tw-mt-1 tw-text-xs tw-text-red-500"
      ) Enter a valid duration.
    div(
      v-else-if="selectedDuration && referenceDateTime"
      class="tw-mt-1 tw-text-xs tw-text-neutral-400"
      ) Selected duration lasts until {{ referenceDateTime.plus(selectedDuration).toUTC().toLocaleString(DateTime.DATETIME_FULL_WITH_SECONDS) }}

    //- If the game time has been skipped, show a hint message.
    MInputHintMessage(v-if="showGameTimeHint")
      MGameTimeOffsetHint

    //- Hint message.
    MInputHintMessage(:variant="variant") {{ hintMessage }}

  p(
    v-else
    class="tw-m-0 tw-text-xs tw-text-neutral-400"
    ) {{ hintMessage }}
</template>

<script setup lang="ts">
import { DateTime, Duration } from 'luxon'
import { computed, ref, watch } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import MGameTimeOffsetHint from '../composites/MGameTimeOffsetHint.vue'
import MInputHintMessage from './MInputHintMessage.vue'
import MInputNumber from './MInputNumber.vue'
import MInputSwitch from './MInputSwitch.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Duration in ISO format (PnDTnHnM) or as a Luxon Duration object. Can also be undefined if the `allowEmpty` prop is true.
     */
    modelValue: Duration | string | undefined
    /**
     * Optional: Luxon `DateTime` to calculate the end date/time preview.
     */
    referenceDateTime?: DateTime
    /**
     * Optional: Allow the input to be empty and show a button to disable the. Defaults to false.
     */
    allowEmpty?: boolean
    /**
     * Optional: Show a label for the input.
     */
    label?: string
    /**
     * Optional: Disable the input. Defaults to false.
     */
    disabled?: boolean
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Visual variant of the input. Defaults to 'default'.
     */
    variant?: 'default' | 'danger' | 'success'
    /**
     * Optional: If this is true and game time has been skipped into the future, a hint warning text is shown. Defaults to true.
     */
    showGameTimeHint?: boolean
  }>(),
  {
    referenceDateTime: undefined,
    label: undefined,
    hintMessage: undefined,
    variant: 'default',
    showGameTimeHint: true,
  }
)

const emit = defineEmits<{
  'update:modelValue': [value?: Duration]
  isValid: [valid: boolean]
}>()

const internalValueSelectionEnabled = ref(props.modelValue !== undefined)
function onInternalValueSelectionEnabledChanged(value: boolean): void {
  internalValueSelectionEnabled.value = value

  if (value) {
    emit('update:modelValue', lastSelectedValue.value)
    emit('isValid', lastSelectedValue.value !== undefined)
  } else {
    emit('update:modelValue', undefined)
    emit('isValid', true)
  }
}

const lastSelectedValue = ref<Duration | undefined>(
  typeof props.modelValue === 'string' ? Duration.fromISO(props.modelValue) : props.modelValue
)

// Data model ---------------------------------------------------------------------------------------------------------

const selectedDays = ref<number | undefined>(
  typeof props.modelValue === 'string' ? Duration.fromISO(props.modelValue).days : props.modelValue?.days
)
const selectedHours = ref<number | undefined>(
  typeof props.modelValue === 'string' ? Duration.fromISO(props.modelValue).hours : props.modelValue?.hours
)
const selectedMinutes = ref<number | undefined>(
  typeof props.modelValue === 'string' ? Duration.fromISO(props.modelValue).minutes : props.modelValue?.minutes
)

// Update internal model values when the external model value changes.
watch(
  () => props.modelValue,
  (newValue) => {
    if (typeof newValue === 'string') newValue = Duration.fromISO(newValue)

    selectedDays.value = newValue?.days
    selectedHours.value = newValue?.hours
    selectedMinutes.value = newValue?.minutes
  },
  { immediate: true }
)

/**
 * External model value (duration) based on internal values.
 */
const selectedDuration = computed(() => {
  const durationObject: { days?: number; hours?: number; minutes?: number } = {}

  if (selectedDays.value) {
    durationObject.days = selectedDays.value
  }
  if (selectedHours.value) {
    durationObject.hours = selectedHours.value
  }
  if (selectedMinutes.value) {
    durationObject.minutes = selectedMinutes.value
  }

  return Duration.fromObject(durationObject)
})

// Emit updates to the external model value.
watch(
  () => selectedDuration.value,
  (newValue) => {
    if (newValue.toMillis() <= 0) {
      emit('update:modelValue', undefined)
      emit('isValid', props.allowEmpty && !internalValueSelectionEnabled.value)
    } else {
      lastSelectedValue.value = newValue
      emit('update:modelValue', newValue)
      emit('isValid', true)
    }
  }
)

// UI stuff -----------------------------------------------------------------------------------------------------------

// Durations of zero or less are not valid.
const valid = computed(() => selectedDuration.value > Duration.fromObject({ minutes: 0 }))

const idDays = makeIntoUniqueKey('days')
const idHours = makeIntoUniqueKey('hours')
const idMinutes = makeIntoUniqueKey('minutes')

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
</script>

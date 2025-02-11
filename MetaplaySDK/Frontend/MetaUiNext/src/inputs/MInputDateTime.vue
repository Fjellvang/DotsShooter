<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- The component is in a visual container if empty value is allowed.
div(:class="{ 'tw-bg-neutral-100 tw-rounded-md tw-border tw-border-neutral-200 tw-p-3': allowEmpty }")
  div(class="tw-flex tw-items-center tw-justify-between")
    label(
      v-if="label"
      :for="id"
      :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
      ) {{ label }}

    div(
      v-if="allowEmpty"
      class="tw-flex tw-items-center tw-space-x-2"
      )
      label(
        class="tw-mb-0 tw-text-xs"
        :class="{ 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-500': !internalDisabled }"
        ) Enable
      MInputSwitch(
        :model-value="internalValueSelectionEnabled"
        :disabled="internalDisabled"
        size="small"
        @update:model-value="onInternalValueSelectionEnabledChanged"
        :data-testid="`${dataTestid}-enable-toggle`"
        )

  div(v-if="!allowEmpty || internalValueSelectionEnabled")
    div(
      class="tw-flex tw-space-x-1"
      :class="{ 'tw-mt-1': allowEmpty }"
      )
      MInputDate(
        :model-value="extractedDate"
        :disabled="internalDisabled"
        :min-iso-date="minDate"
        :max-iso-date="maxDate"
        :variant="variant"
        class="tw-flex-auto"
        @update:model-value="onDateChanged"
        )

      MInputTime(
        :model-value="extractedTime"
        :disabled="disabledTimePicker"
        :min-iso-time="minTime"
        :max-iso-time="maxTime"
        :variant="variant"
        @update:model-value="onTimeChanged"
        )

    //- If the game time has been skipped, show a hint message.
    MInputHintMessage(v-if="showGameTimeHint")
      MGameTimeOffsetHint

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { computed, ref, watch } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MGameTimeOffsetHint from '../composites/MGameTimeOffsetHint.vue'
import MInputDate from './MInputDate.vue'
import MInputHintMessage from './MInputHintMessage.vue'
import MInputSwitch from './MInputSwitch.vue'
import MInputTime from './MInputTime.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Current value of the input as Luxon DateTime object.
     * `undefined` is only allowed if `allowEmpty` is set to true.
     */
    modelValue: DateTime | undefined
    /**
     * Optional: Allow the input to be empty and show a button to disable the input. Defaults to false.
     */
    allowEmpty?: boolean
    /**
     * Optional: Show a label for the input.
     */
    label?: string
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Disable the component.
     */
    disabled?: boolean
    /**
     * Optional: Dates before the given date will be disabled. Passing 'utcNow' sets the date and time to current time.
     */
    minDateTime?: DateTime | 'utcNow'
    /**
     * Optional: Dates after the given date will be disabled. Passing 'utcNow' sets the date and time to current time.
     */
    maxDateTime?: DateTime | 'utcNow'
    /**
     * Optional: Visual variant of the input. Defaults to 'neutral'.
     */
    variant?: 'neutral' | 'danger' | 'success'
    /**
     * Optional: Add a `data-testid` element to the button.
     */
    dataTestid?: string
    /**
     * Optional: If this is true and game time has been skipped into the future, a hint warning text is shown. Defaults to true.
     */
    showGameTimeHint?: boolean
  }>(),
  {
    allowEmpty: false,
    label: undefined,
    hintMessage: undefined,
    disabled: false,
    minDateTime: undefined,
    maxDateTime: undefined,
    variant: 'neutral',
    dataTestid: undefined,
    showGameTimeHint: true,
  }
)

const internalValueSelectionEnabled = ref(props.modelValue !== undefined)
function onInternalValueSelectionEnabledChanged(value: boolean): void {
  internalValueSelectionEnabled.value = value

  if (value) {
    emit('update:modelValue', lastSelectedValue.value)
  } else {
    emit('update:modelValue', undefined)
  }
}

/**
 * Internal state for being able to toggle the component on and off without losing the last selected value.
 */
const lastSelectedValue = ref<DateTime | undefined>(props.modelValue ?? DateTime.utc())

// Watch changed to props.modelValue.
watch(
  () => props.modelValue,
  (newValue) => {
    // If allowEmpty is not set, throw an error if the new value is undefined.
    if (!props.allowEmpty && newValue === undefined) {
      throw new TypeError('modelValue cannot be undefined without setting allowEmpty to true.')
    }

    // If the new value is undefined and the internalValueSelectionEnabled is true, set it to false.
    if (newValue === undefined && internalValueSelectionEnabled.value) {
      internalValueSelectionEnabled.value = false
    }
  },
  { immediate: true }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const id = makeIntoUniqueKey('datetime')

/**
 * Extracts the date part from the DateTime object, which is used to set the modelValue for the MInputDate component.
 */
const extractedDate = computed(() => props.modelValue?.toISODate() ?? '')

const minDate = computed(() => {
  if (props.minDateTime instanceof DateTime) {
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    return props.minDateTime.toISODate()!
  } else if (props.minDateTime === 'utcNow') {
    return DateTime.utc().endOf('day').toISODate()
  } else {
    return props.minDateTime
  }
})

const maxDate = computed(() => {
  if (props.maxDateTime instanceof DateTime) {
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    return props.maxDateTime.toISODate()!
  } else if (props.maxDateTime === 'utcNow') {
    return DateTime.utc().startOf('day').toISODate()
  } else {
    return props.maxDateTime
  }
})

/**
 * Extracts the time part from the DateTime object, which is used to set the modelValue for the MInputTime component.
 */
const extractedTime = computed(() => props.modelValue?.toUTC().toISOTime({ includeOffset: false }) ?? '')

const minTime = computed(() => {
  // Time selection limit only matters when the same day is selected.
  if (!!props.minDateTime && props.modelValue?.toUTC().toISODate() === minDate.value) {
    if (props.minDateTime instanceof DateTime) {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      return props.minDateTime.toUTC().toISOTime({ suppressSeconds: true, includeOffset: false })!
    } else {
      return DateTime.utc().startOf('minute').toISOTime({ suppressSeconds: true, includeOffset: false })
    }
  } else {
    return undefined
  }
})

const maxTime = computed(() => {
  // Time selection limit only matters when the same day is selected.
  if (!!props.maxDateTime && props.modelValue?.toUTC().toISODate() === maxDate.value) {
    if (props.maxDateTime instanceof DateTime) {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      return props.maxDateTime.toUTC().toISOTime({ suppressSeconds: true, includeOffset: false })!
    } else {
      return DateTime.utc().startOf('minute').toISOTime({ suppressSeconds: true, includeOffset: false })
    }
  } else {
    return undefined
  }
})

const emit = defineEmits<{
  'update:modelValue': [value: DateTime | undefined]
}>()

function onDateChanged(value: string): void {
  const hour = props.modelValue?.toUTC().hour
  const minute = props.modelValue?.toUTC().minute

  let newDateTime = DateTime.fromISO(value, { zone: 'utc' }).set({
    hour,
    minute,
  })

  const resolvedMinDateTime =
    props.minDateTime instanceof DateTime ? props.minDateTime : DateTime.utc().startOf('minute')
  const resolvedMaxDateTime =
    props.maxDateTime instanceof DateTime ? props.maxDateTime : DateTime.utc().startOf('minute')

  // Check that the new date + time is not smaller or greater than the min/max date.
  if (props.minDateTime && newDateTime < resolvedMinDateTime) {
    newDateTime = resolvedMinDateTime
  } else if (maxDate.value && newDateTime > resolvedMaxDateTime) {
    newDateTime = resolvedMaxDateTime
  }

  lastSelectedValue.value = newDateTime

  emit('update:modelValue', newDateTime)
}

function onTimeChanged(value: string): void {
  const newDateTime = DateTime.fromISO(value, { zone: 'utc' }).set({
    year: props.modelValue?.year,
    month: props.modelValue?.month,
    day: props.modelValue?.day,
  })

  lastSelectedValue.value = newDateTime

  emit('update:modelValue', newDateTime)
}

/**
 * When you select the time before the date, the component will break. We disable the timepicker until the date is entered.
 * This prevents the component from breaking when allowEmpty is set to true.
 */
const disabledTimePicker = computed((): boolean => {
  if (internalDisabled.value) {
    return internalDisabled.value
  }

  return props.allowEmpty && !props.modelValue?.isValid
})
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  //- Label row.
  div(:class="['tw-flex tw-justify-between']")
    label(
      :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': disabled, 'tw-text-neutral-900': !disabled }]"
      )
      span(v-if="label") {{ label }}
      span(v-else) {{ internalInputMode === 'duration' ? durationTitle : `${dateTimeTitle} (UTC)` }}

    //- Exact date picker switch.
    div
      span(
        class="tw-mr-2 tw-text-xs"
        @click="internalInputMode = internalInputMode === 'duration' ? 'endDateTime' : 'duration'"
        ) Exact date picker
      MInputSwitch(
        :model-value="internalInputMode === 'endDateTime' ? true : false"
        size="extraSmall"
        class="-tw-mb-1"
        @update:model-value="internalInputMode = $event ? 'endDateTime' : 'duration'"
        )

  //- Duration picker output is emitted as-is.
  MInputDuration(
    v-show="internalInputMode === 'duration'"
    :model-value="modelValue"
    :disabled="disabled"
    @update:model-value="onDurationChanged"
    )
  //- DateTime picker results are transformed into a Duration before emitting.
  MInputDateTime(
    v-show="internalInputMode === 'endDateTime'"
    :model-value="internalDateTime"
    :min-date-time="referenceDateTime"
    :disabled="disabled"
    @update:model-value="onDateTimeChanged"
    )

  //- Hint message.
  MInputHintMessage(v-if="internalInputMode === 'duration'")
    <!-- @slot Optional: Define a custom hint message for the duration picker. Use the `dateTime` slot prop to access the currently selected duration's projected end date. -->
    slot(
      name="duration-hint-message"
      :dateTime="internalDateTime.toUTC().toLocaleString(DateTime.DATETIME_FULL_WITH_SECONDS)"
      ) Selected duration will last until {{ internalDateTime.toUTC().toLocaleString(DateTime.DATETIME_FULL_WITH_SECONDS) }}
  MInputHintMessage(v-else)
    <!-- @slot Optional: Define a custom hint message for the end date time picker. Use the `duration` slot prop to access the currently selected end time's projected duration. -->
    slot(
      name="end-time-hint-message"
      :duration="modelValue.toHuman({ listStyle: 'long', unitDisplay: 'short' })"
      ) Selected end time is {{ modelValue.toHuman({ listStyle: 'long', unitDisplay: 'short' }) }} after start time.
</template>

<script setup lang="ts">
import { DateTime, type Duration } from 'luxon'
import { onMounted, onUnmounted, ref, watch } from 'vue'

import MInputDateTime from './MInputDateTime.vue'
import MInputDuration from './MInputDuration.vue'
import MInputHintMessage from './MInputHintMessage.vue'
import MInputSwitch from './MInputSwitch.vue'

const props = withDefaults(
  defineProps<{
    /**
     * End date time as a Luxon `Duration` object.
     */
    modelValue: Duration
    /**
     * Optional: Start date time as a Luxon `DateTime` object to calculate the duration preview. Defaults to UTC now.
     */
    referenceDateTime?: DateTime | 'utcNow'
    /**
     * Optional: Input mode. Defaults to `duration`.
     */
    inputMode?: 'duration' | 'endDateTime'
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
     * Optional: Custom title to show when the `duration` input mode is selected. Defaults to 'Duration'.
     */
    durationTitle?: string
    /**
     * Optional: Custom title to show when the `endDateTime` input mode is selected. Defaults to 'End'.
     */
    dateTimeTitle?: string
  }>(),
  {
    inputMode: 'duration',
    hintMessage: undefined,
    label: undefined,
    referenceDateTime: 'utcNow',
    durationTitle: 'Duration',
    dateTimeTitle: 'End',
  }
)

const emit = defineEmits<{
  'update:modelValue': [value: Duration]
  'update:duration': [value: Duration]
  'update:endDateTime': [value: DateTime]
}>()

// Keep track of the internal input mode.
const internalInputMode = ref<'duration' | 'endDateTime'>(props.inputMode)
watch(
  () => props.inputMode,
  (newValue) => {
    internalInputMode.value = newValue
  },
  { immediate: true }
)

// Keep track of the reference date time.
const internalReferenceDateTime = ref<DateTime>(
  props.referenceDateTime === 'utcNow' ? DateTime.now() : props.referenceDateTime
)

// React to outside changes in the reference date time.
watch(
  () => props.referenceDateTime,
  (newValue) => {
    internalReferenceDateTime.value = newValue === 'utcNow' ? DateTime.now() : newValue
  }
)

// Use a timer to tick the reference date time every second.
let intervalHandle: ReturnType<typeof setInterval>
onMounted(() => {
  intervalHandle = setInterval(() => {
    if (props.referenceDateTime === 'utcNow' && internalInputMode.value === 'duration') {
      internalReferenceDateTime.value = DateTime.now()
      internalDateTime.value = getSelectedDurationAsDateTime(props.modelValue, internalReferenceDateTime.value)
    }
  }, 1000)
})

onUnmounted(() => {
  clearInterval(intervalHandle)
})

// Keep track of the selected duration as a date time...
const internalDateTime = ref<DateTime>(getSelectedDurationAsDateTime(props.modelValue, internalReferenceDateTime.value))

function getSelectedDurationAsDateTime(selectedDuration: Duration, referenceDateTime: DateTime): DateTime {
  return referenceDateTime.plus(selectedDuration)
}

// ...and update it when the model values change...
watch(
  () => props.modelValue,
  (newValue) => {
    internalDateTime.value = getSelectedDurationAsDateTime(newValue, internalReferenceDateTime.value)
  }
)
watch(
  () => props.referenceDateTime,
  (newValue) => {
    internalDateTime.value = getSelectedDurationAsDateTime(
      props.modelValue,
      newValue === 'utcNow' ? DateTime.now() : newValue
    )
  }
)

function onDurationChanged(newValue?: Duration): void {
  if (!newValue) return

  internalDateTime.value = getSelectedDurationAsDateTime(newValue, internalReferenceDateTime.value)
  emit('update:modelValue', newValue)
  emit('update:duration', newValue)
}

function onDateTimeChanged(newValue: DateTime | undefined): void {
  // TODO: Why the non-null assertion? Looks sus.
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  const newDateTime = newValue!

  internalDateTime.value = newDateTime
  emit('update:modelValue', newDateTime.diff(internalReferenceDateTime.value, ['days', 'hours', 'minutes']))
  emit('update:endDateTime', newDateTime)
}
</script>

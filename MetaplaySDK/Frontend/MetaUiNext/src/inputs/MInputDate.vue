<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div
  label(
    v-if="label"
    :for="id"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  div(class="tw-relative")
    <!-- @vue-ignore - TS is confused about multiple event handlers -->
    input(
      v-bind="{ ...api.getTriggerProps(), ...$attrs }"
      type="text"
      :value="dateDisplayValue"
      :disabled="disabled"
      :class="['tw-w-full tw-rounded-md tw-shadow-sm tw-border-0 tw-py-1.5 tw-text-neutral-900 tw-ring-1 tw-ring-inset placeholder:tw-text-neutral-400 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 sm:tw-text-sm sm:tw-leading-6 disabled:tw-cursor-not-allowed disabled:tw-bg-neutral-50 disabled:tw-text-neutral-500 disabled:ring-neutral-200', variantClasses]"
      :aria-invalid="variant === 'danger' ? true : undefined"
      :aria-describedby="hintId"
      :ref="setInputElementRef"
      readonly
      @wheel.stop="onInputScrollWheel"
      @keyup.enter="api.setOpen(true)"
      @keyup.space="api.setOpen(true)"
      @keyup.up="modifySelection('keyUp')"
      @keyup.down="modifySelection('keyDown')"
      @keyup.left="modifySelection('keyLeft')"
      @keyup.right="modifySelection('keyRight')"
      )

    //- Input arrow up.
    button(
      :class="['tw-absolute tw-top-0.5 tw-right-2 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 hover:tw-bg-transparent active:tw-bg-transparent': internalDisabled || !canModifySelection('buttonUp') }]"
      :disabled="internalDisabled || !canModifySelection('buttonUp')"
      @click="modifySelection('buttonUp')"
      )
      //- Icon from https://heroicons.com/
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-w-4 tw-h-4">
        <path fill-rule="evenodd" d="M14.77 12.79a.75.75 0 01-1.06-.02L10 8.832 6.29 12.77a.75.75 0 11-1.08-1.04l4.25-4.5a.75.75 0 011.08 0l4.25 4.5a.75.75 0 01-.02 1.06z" clip-rule="evenodd" />
      </svg>

    //- Input arrow down.
    button(
      :class="['tw-absolute tw-bottom-0.5 tw-right-2 hover:tw-bg-neutral-300 active:tw-bg-neutral-400 tw-rounded', { 'tw-text-red-500': variant === 'danger', 'tw-text-neutral-400 hover:tw-bg-transparent active:tw-bg-transparent': internalDisabled || !canModifySelection('buttonDown') }]"
      :disabled="internalDisabled || !canModifySelection('buttonDown')"
      @click="modifySelection('buttonDown')"
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

  Teleport(to="body")
    div(v-bind="api.getPositionerProps()")
      div(
        v-bind="api.getArrowProps()"
        v-show="api.open"
        )
        div(
          v-bind="api.getArrowTipProps()"
          class="tw-z-0 tw-border-l tw-border-t tw-border-neutral-200"
          )
      <!-- @vue-ignore -->
      div(
        v-bind="api.getContentProps()"
        class="tw-max-h-[calc(100vh_-_100px)] tw-max-w-sm tw-overflow-auto tw-rounded-lg tw-border tw-border-neutral-200 tw-bg-white tw-shadow-md"
        @wheel="onPopoverScrollWheel"
        @keydown.up="modifyKeyboardHighlight('popoverKeyUp')"
        @keydown.down="modifyKeyboardHighlight('popoverKeyDown')"
        @keydown.left="modifyKeyboardHighlight('popoverKeyLeft')"
        @keydown.right="modifyKeyboardHighlight('popoverKeyRight')"
        @keydown.enter="onDateSelected(keyboardHighlight)"
        )
        //- Header.
        div(class="tw-flex tw-h-10 tw-items-center")
          button(
            class="tw-flex tw-size-10 tw-flex-none tw-items-center tw-justify-center tw-rounded-md hover:tw-bg-neutral-200 hover:tw-text-neutral-700 focus:tw-border-2 focus:tw-border-blue-600 active:tw-bg-neutral-300"
            @click="openMonth = openMonth?.minus({ months: 1 })"
            )
            //- Icon from Heroicons.
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-size-5">
              <path fill-rule="evenodd" d="M11.78 5.22a.75.75 0 0 1 0 1.06L8.06 10l3.72 3.72a.75.75 0 1 1-1.06 1.06l-4.25-4.25a.75.75 0 0 1 0-1.06l4.25-4.25a.75.75 0 0 1 1.06 0Z" clip-rule="evenodd" />
            </svg>

          div(
            v-if="openMonth"
            class="tw-flex tw-h-10 tw-flex-grow tw-items-center tw-justify-center tw-text-sm tw-font-medium tw-text-neutral-700"
            ) {{ openMonth.monthLong }} {{ openMonth.year }}
          button(
            class="tw-flex tw-size-10 tw-flex-none tw-items-center tw-justify-center tw-rounded-md hover:tw-bg-neutral-200 hover:tw-text-neutral-700 focus:tw-border-2 focus:tw-border-blue-600 active:tw-bg-neutral-300"
            @click="openMonth = openMonth?.plus({ months: 1 })"
            )
            //- Icon from Heroicons.
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-size-5">
              <path fill-rule="evenodd" d="M8.22 5.22a.75.75 0 0 1 1.06 0l4.25 4.25a.75.75 0 0 1 0 1.06l-4.25 4.25a.75.75 0 0 1-1.06-1.06L11.94 10 8.22 6.28a.75.75 0 0 1 0-1.06Z" clip-rule="evenodd" />
            </svg>

        div(class="tw-grid tw-grid-cols-7 tw-gap-px")
          //- Weekdays.
          div(
            v-for="day in ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']"
            :key="day"
            class="tw-relative tw-z-0 tw-flex tw-size-10 tw-items-center tw-justify-center tw-text-xs tw-font-medium tw-text-neutral-500"
            ) {{ day }}
          //- Days.
          div(
            v-for="day in dayOptionsToShow"
            :key="day.date.toISODate() || ''"
            class="tw-relative tw-z-0 tw-flex tw-size-10 tw-items-center tw-justify-center tw-text-sm tw-font-medium"
            )
            span(
              v-if="day.isToday"
              class="tw-pointer-events-none tw-absolute tw-inset-0.5 -tw-z-10 tw-rounded-full tw-bg-neutral-200"
              aria-hidden="true"
              )
            button(
              v-if="!day.isDisabled"
              :ref="`day-${day.date.day}`"
              class="tw-size-10 tw-cursor-pointer tw-rounded-md tw-leading-5 focus:tw-border-2 focus:tw-border-blue-600 focus:tw-outline-none"
              :class="{ 'tw-bg-blue-500 tw-text-white': day.isSelected, 'hover:tw-text-neutral-700 hover:tw-bg-neutral-200 active:tw-bg-neutral-300': !day.isSelected, 'tw-text-neutral-700': !day.isDifferentMonth && !day.isSelected, 'tw-text-neutral-400': day.isDifferentMonth && !day.isSelected, 'tw-bg-neutral-200': day.isKeyboardHighlight && !day.isSelected }"
              :aria-label="day.date.toLocaleString(DateTime.DATE_HUGE)"
              :aria-current="day.isToday ? 'date' : undefined"
              :aria-selected="day.isSelected"
              @click="onDateSelected(day.date)"
              ) {{ day.date.day }}
            span(
              v-else
              class="tw-flex tw-size-10 tw-cursor-not-allowed tw-items-center tw-justify-center tw-rounded-md tw-bg-neutral-50 tw-leading-5 tw-text-neutral-300 tw-ring-neutral-200"
              ) {{ day.date.day }}
</template>

<script setup lang="ts">
import { DateTime, type DurationLikeObject } from 'luxon'
import { computed, onMounted, onBeforeUnmount, ref, Teleport } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import * as popover from '@zag-js/popover'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputHintMessage from './MInputHintMessage.vue'

const props = defineProps<{
  /**
   * Current value of the input as a ISO 8601 date string.
   * @example '2021-01-01'
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
   * Optional: Dates before the given ISO 8601 date will be disabled. Passing in the string value `utcNow` sets this value to the
   * current date.
   * @example '2021-01-01'
   */
  // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents -- `string` and `utcNow` are overlapping types.
  minIsoDate?: string | 'utcNow'
  /**
   * Optional: Dates after the given ISO 8601 date will be disabled. Passing in the string value `utcNow` sets this value to the
   * current date.
   * @example '2021-12-31'
   */
  // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents -- `string` and `utcNow` are overlapping types.
  maxIsoDate?: string | 'utcNow'
  /**
   * Optional: Visual variant of the input. Defaults to 'neutral'.
   */
  variant?: 'neutral' | 'danger' | 'success'
}>()

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))
const id = makeIntoUniqueKey('date')
const hintId = makeIntoUniqueKey('date-input-hint')

/**
 * The selected date as a Luxon DateTime object.
 */
const selectedDateTime = computed(() => {
  if (!props.modelValue) return undefined
  return DateTime.fromISO(props.modelValue)
})

const minDateTime = computed(() => {
  if (!props.minIsoDate) return undefined
  if (props.minIsoDate === 'utcNow') {
    return DateTime.now().toUTC().startOf('day')
  } else {
    return DateTime.fromISO(props.minIsoDate)
  }
})

const maxDateTime = computed(() => {
  if (!props.maxIsoDate) return undefined
  if (props.maxIsoDate === 'utcNow') {
    return DateTime.now().toUTC().endOf('day')
  } else {
    return DateTime.fromISO(props.maxIsoDate)
  }
})

// Calculate width ----------------------------------------------------------------------------------------------

// Reference to the input element.
const inputElement = ref<Element>()
function setInputElementRef(el: any): void {
  inputElement.value = el as Element
}

// Observe changes in the size of the input element and remember the width.
const inputElementObserver = new ResizeObserver(() => {
  inputElementWidth.value = inputElement.value?.clientWidth ?? 0
})

onMounted(() => {
  if (inputElement.value) {
    inputElementObserver.observe(inputElement.value)
  }
})

onBeforeUnmount(() => {
  inputElementObserver.disconnect()
})

// Width of the input element.
const inputElementWidth = ref(0)

// Actions ----------------------------------------------------------------------------------------------
const actionConfig: Record<string, DurationLikeObject> = {
  keyUp: { days: +1 },
  keyDown: { days: -1 },
  keyRight: { days: +1 },
  keyLeft: { days: -1 },
  buttonUp: { days: +1 },
  buttonDown: { days: -1 },
  scrollwheelUp: { days: +1 },
  scrollwheelDown: { days: -1 },
  popoverKeyUp: { weeks: -1 },
  popoverKeyDown: { weeks: +1 },
  popoverKeyRight: { days: +1 },
  popoverKeyLeft: { days: -1 },
}

function timePlusAction(time: DateTime, action: string, clamp: boolean): DateTime {
  const duration = actionConfig[action]
  console.assert(action in actionConfig, `Invalid action: ${action}`)
  return clamp ? clampDateTime(time.plus(duration)) : time.plus(duration)
}

// Value modification ----------------------------------------------------------------------------------------------

function canModifySelection(action: string): boolean {
  if (!selectedDateTime.value) return false
  return !selectedDateTime.value.hasSame(timePlusAction(selectedDateTime.value, action, true), 'day')
}

function modifySelection(action: string): void {
  if (!selectedDateTime.value) return
  onDateSelected(timePlusAction(selectedDateTime.value, action, true))
}

function clampDateTime(dateTime: DateTime): DateTime {
  if (minDateTime.value && dateTime < minDateTime.value) {
    return minDateTime.value
  } else if (maxDateTime.value && dateTime > maxDateTime.value) {
    return maxDateTime.value
  } else {
    return dateTime
  }
}

// Mouse scroll ----------------------------------------------------------------------------------------------
function onInputScrollWheel(event: WheelEvent): void {
  event.preventDefault()
  if (internalDisabled.value) return

  if (event.deltaY < 0) {
    modifySelection('scrollwheelUp')
  } else {
    modifySelection('scrollwheelDown')
  }
}

function onPopoverScrollWheel(event: WheelEvent): void {
  if (!openMonth.value) return
  if (event.deltaY < 0) {
    openMonth.value = openMonth.value.plus({ months: 1 })
  } else {
    openMonth.value = openMonth.value.minus({ months: 1 })
  }

  event.stopPropagation()
}

// Open picker ----------------------------------------------------------------------------------------------

const openMonth = ref<DateTime | undefined>(selectedDateTime.value)
const keyboardHighlight = ref<DateTime | undefined>()
interface DayOption {
  date: DateTime
  isDifferentMonth: boolean
  isDisabled: boolean
  isSelected: boolean
  isToday: boolean
  isKeyboardHighlight: boolean
}

const dayOptionsToShow = computed((): DayOption[] => {
  // Return a list of days to show in the picker. This includes days from the previous month and next month to fill out the grid.
  if (!openMonth.value) return []
  const startDate = openMonth.value.startOf('month').startOf('week')
  const endDate = startDate.plus({ weeks: 6 }).minus({ days: 1 })
  const today = DateTime.now().toISODate()
  const days: DayOption[] = []
  for (let day = startDate; day <= endDate; day = day.plus({ days: 1 })) {
    const isDifferentMonth = day.month !== openMonth.value.month
    const isDisabled = Boolean(
      (!!minDateTime.value && day < minDateTime.value) || (!!maxDateTime.value && day > maxDateTime.value)
    )
    const isSelected = Boolean(!!props.modelValue && day.toISODate() === props.modelValue)
    const isToday = day.toISODate() === today
    const isKeyboardHighlight = keyboardHighlight.value?.toISODate() === day.toISODate()
    days.push({
      date: day,
      isDifferentMonth,
      isDisabled,
      isSelected,
      isToday,
      isKeyboardHighlight,
    })
  }
  return days
})

function modifyKeyboardHighlight(action: string): void {
  if (!keyboardHighlight.value) keyboardHighlight.value = DateTime.now()
  const newValue = timePlusAction(keyboardHighlight.value, action, false)
  const newValueClamped = timePlusAction(keyboardHighlight.value, action, true)
  if (newValue.hasSame(newValueClamped, 'day')) {
    keyboardHighlight.value = newValue
    if (keyboardHighlight.value.month !== openMonth.value?.month) {
      openMonth.value = keyboardHighlight.value
    }
  }
}

// Visuals ----------------------------------------------------------------------------------------------

const dateDisplayValue = computed(() => {
  if (props.modelValue) {
    let format: Intl.DateTimeFormatOptions
    if (inputElementWidth.value < 130) format = DateTime.DATE_SHORT
    else if (inputElementWidth.value < 220) format = DateTime.DATE_MED
    else format = DateTime.DATE_HUGE
    return DateTime.fromISO(props.modelValue).toLocaleString(format)
  } else {
    return undefined
  }
})

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

// Emits ----------------------------------------------------------------------------------------------

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

/**
 * Emits the value as a ISO 8601 string.
 * The `datepicker` component gives us the updated value as a `DateTime` object,
 * so we need to convert it before emitting it.
 * @param value New datetime value
 */
function onDateSelected(value?: DateTime): void {
  if (value) {
    api.value.setOpen(false)
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    emit('update:modelValue', value.toISODate()!)
  }
}

// Zag ----------------------------------------------------------------------------------------------

const initialFocusElement = ref<HTMLElement>()
const [state, send] = useMachine(
  popover.machine({
    id: makeIntoUniqueKey('popover'),
    modal: true,
    autoFocus: false,
    onOpenChange: (details) => {
      if (details.open) {
        openMonth.value = selectedDateTime.value ?? DateTime.now()
        keyboardHighlight.value = selectedDateTime.value
      }
    },
  })
)

const api = computed(() => popover.connect(state.value, send, normalizeProps))
</script>

<style scoped>
[data-part='arrow'] {
  --arrow-background: white;
  --arrow-size: 16px;
}
</style>

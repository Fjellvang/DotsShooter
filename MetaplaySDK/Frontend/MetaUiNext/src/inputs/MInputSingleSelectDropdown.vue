<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
div
  //- Label.
  label(
    v-if="label"
    v-bind="api.getLabelProps()"
    :for="id"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  //- Input.
  div(ref="trigger")
    button(
      v-bind="api.getTriggerProps()"
      :class="['tw-w-full tw-flex tw-justify-between tw-items-center tw-px-3 tw-rounded-md tw-shadow-sm tw-border-0 tw-bg-white tw-py-1.5 tw-overflow-x-hidden tw-text-neutral-900 tw-ring-1 tw-ring-inset placeholder:tw-text-neutral-400 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 sm:tw-text-sm sm:tw-leading-6 disabled:tw-cursor-not-allowed disabled:tw-bg-neutral-50 disabled:tw-text-neutral-500 disabled:ring-neutral-200', variantClasses]"
      :data-testid="`${dataTestid}-dropdown`"
      )
      <!-- @slot Optional: Slot for customizing the selected option. -->
      slot(
        v-if="api.value[0]"
        name="selection"
        :value="getSelectedOptionFromId(api.value[0])"
        )
        span(:class="{ 'tw-text-neutral-400': !getSelectedOptionFromId(api.value[0]) }") {{ getSelectedOptionFromId(api.value[0])?.label }}
      span(
        v-else
        class="tw-text-sm tw-text-neutral-400"
        ) {{ placeholder }}
      span(class="tw-text-neutral-400") ▼

  //- Options popover.
  component(
    :is="props.teleportToBody ? 'teleport' : 'div'"
    :to="props.teleportToBody ? 'body' : undefined"
    )
    div(
      :key="key"
      v-bind="api.getPositionerProps()"
      style="z-index: 9999"
      )
      ul(
        v-bind="api.getContentProps()"
        ref="listbox"
        class="tw-max-h-80 tw-overflow-y-auto tw-overflow-x-hidden tw-overflow-ellipsis tw-rounded-md tw-border tw-border-neutral-300 tw-bg-white tw-text-sm tw-shadow-lg"
        :style="{ width: listboxWidth }"
        )
        li(
          v-for="option in optionsForSelectDropdown"
          :key="option.id"
          v-bind="api.getItemProps({ item: option })"
          :disabled="option.disabled"
          :class="['tw-px-3 tw-py-1.5 first:tw-rounded-t-md last:tw-rounded-b-md tw-cursor-pointer', { '!tw-bg-blue-500 hover:!tw-bg-blue-600 !tw-text-white': api.selectedItems.some((selectedOption) => selectedOption?.id === option.id), 'tw-text-neutral-400 tw-bg-neutral-50 tw-cursor-not-allowed tw-italic': option.disabled }]"
          :data-testid="`select-option-${sentenceCaseToKebabCase(option.label)}`"
          )
          <!-- @slot Optional: Slot for customizing dropdown options. -->
          slot(
            name="option"
            :option="getOptionInfo(option)"
            )
            div(class="tw-flex tw-justify-between")
              span {{ option.label }}
              span(
                v-bind="api.getItemIndicatorProps({ item: option })"
                class="tw-ml-2"
                ) ✓

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts" generic="T">
import { isEqual } from 'lodash-es'
import { computed, ref } from 'vue'

import { makeHash, makeIntoUniqueKey, sentenceCaseToKebabCase } from '@metaplay/meta-utilities'

import { useResizeObserver } from '@vueuse/core'
import * as select from '@zag-js/select'
import type { Context } from '@zag-js/select'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import { useNotifications } from '../composables/useNotifications'
import MInputHintMessage from './MInputHintMessage.vue'

export interface MInputSingleSelectDropdownOption<T> {
  label: string
  value: T
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
    options: Array<MInputSingleSelectDropdownOption<T>>
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
     * Optional: Placeholder text to show in the input.
     */
    placeholder?: string
    /**
     * Optional: Whether to teleport the options popover to the HTML body. Defaults to false.
     * This is an advanced option that can help with z-index issues.
     */
    teleportToBody?: boolean
    /**
     * Optional: Add a `data-testid` attribute to the dropdown element.
     */
    dataTestid?: string
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    hintMessage: undefined,
    placeholder: 'Select option',
    dataTestid: undefined,
  }
)

/**
 * Set the internal disabled state.
 */
const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const { showErrorNotification } = useNotifications()

const emit = defineEmits<{
  'update:modelValue': [value: T]
}>()

const trigger = ref<HTMLElement | null>(null)
const listbox = ref<HTMLElement | null>(null)
const listboxWidth = ref<string | undefined>(undefined)

/**
 * Unique key to force a re-render when the window width changes.
 */
const key = ref(1)

/**
 * Force the menu to resize when the trigger resizes.
 */
useResizeObserver(trigger, (entries) => {
  const { width } = entries[0].contentRect
  // Set the width of the listbox to the width of the trigger.
  if (listbox.value) {
    listboxWidth.value = `${width}px`
  }
  key.value++
})

// Options ------------------------------------------------------------------------------------------------------------

/**
 * Options with additional IDs for the select dropdown.
 */
const optionsForSelectDropdown = computed(() => {
  // Generate IDs for all options.
  // Zag select only accepts strings as values, we introduce IDs in order
  // to support generic values.
  const options = props.options.map((option) => ({
    ...option,
    id: makeHash(option.value),
  }))

  // Check for duplicates IDs in the options array.
  const uniqueIds = new Set(options.map((option) => option.id))
  if (options.length !== uniqueIds.size) {
    console.error('Duplicate IDs found in options array of MInputSingleSelectDropdown:', options)
  }

  return options
})

/**
 * Helper to get the selected option based on it's ID.
 * @param id The ID of the option.
 */
const getSelectedOptionFromId = (id: string): MInputSingleSelectDropdownOption<T> | undefined => {
  const optionFromId = optionsForSelectDropdown.value.find((option) => option.id === id)
  return optionFromId ?? undefined
}

// Zag Select ---------------------------------------------------------------------------------------------------------

const id = makeIntoUniqueKey('select')

/**
 * The initial value of the select.
 */
const initalValue = computed(() => {
  if (typeof props.modelValue === 'object') {
    return optionsForSelectDropdown.value.find((option) => isEqual(option.value, props.modelValue))?.id
  } else {
    return optionsForSelectDropdown.value.find((option) => option.value === props.modelValue)?.id
  }
})

/**
 * Values to be passed to the state machine context.
 */
const transientContext = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    value: initalValue.value ? [initalValue.value] : undefined,
  })
)

/**
 * Zag state machine for the select.
 */
const [state, send] = useMachine(
  select.machine({
    id,
    collection: select.collection({
      items: optionsForSelectDropdown.value,
      isItemDisabled: (item) => !!item.disabled,
      itemToValue: (item) => item.id,
    }),
    loopFocus: true,
    onValueChange: ({ value }) => {
      const option = getSelectedOptionFromId(value[0])
      if (option) {
        emit('update:modelValue', option.value)
      } else {
        showErrorNotification(`Option with ID ${value[0]} not found in options array.`)
      }
    },
  }),
  {
    // Store the state and transition data.
    context: transientContext,
  }
)

/**
 * Api object that contains all the props, state, methods and event handlers to interact with the select.
 */
const api = computed(() => select.connect(state.value, send, normalizeProps))

/**
 * Helper to get the option and its related info.
 * @param option The option to get info for.
 */
function getOptionInfo(option: any): {
  label: string
  value: T
  highlighted: boolean
  selected: boolean
} {
  return {
    ...(option as { label: string; value: T }),

    highlighted: api.value.highlightedItem?.value === option.value,

    selected: api.value.selectedItems.some((selectedOption) => selectedOption === option.value),
  }
}

// Custom Styles ---------------------------------------------------------------------------------------------------

/**
 * Helper to get variant specific classes.
 */
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

<style scoped>
[data-part='item'][data-highlighted] {
  @apply tw-bg-neutral-200;
}
</style>

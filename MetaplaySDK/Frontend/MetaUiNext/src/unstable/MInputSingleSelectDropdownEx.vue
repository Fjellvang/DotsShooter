<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
div(v-bind="api.getRootProps()")
  //- Label.
  label(
    v-if="label"
    v-bind="api.getLabelProps()"
    :for="inputComponentId"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}
  //- Input.
  div(
    v-bind="api.getControlProps()"
    ref="trigger"
    class="tw-relative tw-flex tw-min-h-8 tw-w-full tw-items-center tw-justify-between tw-shadow-sm tw-ring-1 tw-ring-inset"
    :class="[{ 'tw-text-neutral-400 tw-bg-neutral-50 tw-cursor-not-allowed': internalDisabled }, variantClasses]"
    )
    input(
      v-bind="api.getInputProps()"
      v-model="searchInput"
      :id="inputComponentId"
      :placeholder="api.value[0] ? undefined : placeholder"
      class="focus:tw-ring-blue-60 tw-absolute tw-inset-0 tw-left-0 tw-z-0 tw-overflow-x-hidden tw-rounded-md tw-border-none tw-bg-transparent placeholder:tw-text-neutral-400 focus:tw-ring-2 focus:tw-ring-inset disabled:tw-cursor-not-allowed"
      :class="{ 'tw-rounded-b-none': api.open }"
      @keyup:enter="api.selectValue"
      :data-testid="`${dataTestid}-input`"
      )
    <!-- @slot Optional: Slot for customizing the selected option. The slot gets on `option` prop with the currently selected option. Selection can be `undefined`. -->
    div(
      :class="['tw-flex-grow tw-pl-3 tw-py-1 tw-text-ellipsis tw-overflow-hidden']"
      :data-testid="`${dataTestid}-selected-option`"
      )
      slot(
        v-if="api.value[0] && !api.inputValue"
        name="selection"
        :option="getSelectedOptionFromId(api.value[0])"
        ) {{ getSelectedOptionFromId(api.value[0])?.label }}

    //- Icons.
    div(class="tw-z-10 tw-flex tw-items-center tw-space-x-1")
      //- Success icon.
      <svg v-if="variant === 'success'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-pointer-events-none tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-green-500" fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clip-rule="evenodd" />
      </svg>

      //- Warning icon.
      <svg v-if="variant === 'warning'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-pointer-events-none tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-orange-500" fill-rule="evenodd" d="M10 2L2 18h16L10 2z" clip-rule="evenodd" />
        <path class="tw-text-orange-50" fill-rule="evenodd" d="M9 6h2v6H9V6zm0 8h2v2H9v-2z" clip-rule="evenodd" />
      </svg>

      //- Danger icon.
      <svg v-else-if="variant === 'danger'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-pointer-events-none tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-red-500" fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 10a1 1 0 100-2 1 1 0 000 2z" clip-rule="evenodd" />
      </svg>

      //- Clear button.
      button(
        v-if="api.value[0] && showClearButton"
        v-bind="api.getClearTriggerProps()"
        :data-testid="`${dataTestid}-clear-button`"
        )
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" class="tw-size-4 tw-fill-neutral-50 tw-p-0.5 tw-rounded-full tw-bg-neutral-400 hover:tw-bg-neutral-500 active:tw-bg-neutral-600 tw-cursor-pointer" :class="[iconVariantClasses]">
          <path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" />
        </svg>

      //- Dropdown open button.
      button(
        v-bind="api.getTriggerProps()"
        class="tw-pr-3 tw-text-neutral-400 hover:tw-text-neutral-500 active:tw-text-neutral-600 disabled:tw-cursor-not-allowed"
        :data-testid="`${dataTestid}-trigger`"
        ) ▼

  //- Options popover.
  div(class="tw-absolute")
    //- Positioner.
    div(
      :key="forceRefreshKey"
      v-bind="api.getPositionerProps()"
      :id="inputComponentId"
      :style="{ zIndex: 9999, width: listboxWidth }"
      )
      //- List root.
      ul(
        v-if="optionsForSelectDropdown.length > 0"
        v-bind="api.getContentProps()"
        ref="listbox"
        class="tw-max-h-80 tw-overflow-y-auto tw-overflow-x-hidden tw-overflow-ellipsis tw-rounded-b-md tw-border tw-border-t-0 tw-border-neutral-300 tw-bg-white tw-text-sm tw-shadow-lg"
        :style="{ width: listboxWidth }"
        :data-testid="`${dataTestid}-dropdown`"
        )
        //- List items.
        li(
          v-for="option in optionsForSelectDropdown"
          :key="option.id"
          v-bind="api.getItemProps({ item: option })"
          :disabled="option.disabled"
          :class="['tw-px-3 tw-py-1.5 first:tw-rounded-t-md last:tw-rounded-b-md tw-cursor-pointer', { '!tw-bg-blue-500 hover:!tw-bg-blue-600 !tw-text-white': api.value[0] === option.id, 'tw-text-neutral-400 tw-bg-neutral-100 tw-cursor-not-allowed tw-italic': option.disabled }]"
          @click:model-value="api = $event"
          :data-testid="`${dataTestid}-${option.label}`"
          )
          <!-- @slot Optional: Slot for customizing dropdown options. The slot gets an `optionInfo` prop that has the option itself and its highligh and selected status. -->
          slot(
            name="option"
            :optionInfo="getOptionInfo(option)"
            )
            div(class="tw-flex tw-justify-between")
              div(class="tw-grow tw-overflow-hidden tw-text-ellipsis") {{ option.label }}
              span(
                v-bind="api.getItemIndicatorProps({ item: option })"
                class="tw-ml-2"
                ) ✓
      div(
        v-else
        class="tw-select-none tw-overflow-y-auto tw-overflow-x-hidden tw-overflow-ellipsis tw-rounded-b-md tw-border tw-border-t-0 tw-border-neutral-300 tw-bg-neutral-200 tw-px-3 tw-py-1 tw-text-sm tw-italic tw-text-neutral-500 tw-shadow-lg"
        ) {{ emptyMessage }}

  //- Hint message.
  MInputHintMessage(:variant="variant") {{ hintMessage }}
</template>

<script setup lang="ts" generic="T">
import { isEqual } from 'lodash-es'
import { computed, ref, onMounted, watch } from 'vue'

import { makeHash, makeIntoUniqueKey } from '@metaplay/meta-utilities'

import { useResizeObserver } from '@vueuse/core'
import * as combobox from '@zag-js/combobox'
import type { Context } from '@zag-js/combobox'
import { normalizeProps, useMachine } from '@zag-js/vue'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import { useNotifications } from '../composables/useNotifications'
import MInputHintMessage from '../inputs/MInputHintMessage.vue'
import type { Variant } from '../utils/types'

export interface MInputSingleSelectDropdownExOption<T = string> {
  /**
   * Human readable label to show in the dropdown.
   */
  label: string
  /**
   * The value of the option. Can be a complex object.
   */
  value: T
  /**
   * Optional: Whether the option is disabled. Defaults to `false`.
   */
  disabled?: boolean
}

export type MInputSelectSearchFunction<T> = (
  options: Array<MInputSingleSelectDropdownExOption<T>>,
  query: string
) => Array<MInputSingleSelectDropdownExOption<T>>

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input.
     */
    modelValue: T
    /**
     * The collection of items to show in the select.
     */
    options: Array<MInputSingleSelectDropdownExOption<T>>
    /**
     * Optional: A custom search function that returns a subset of the options based on the query. Default implementation searches the `label` field.
     */
    searchFunction?: MInputSelectSearchFunction<T>
    /**
     * Optional: Show a label for the input.
     */
    label?: string
    /**
     * Optional: Disable the input. Defaults to `false`.
     */
    disabled?: boolean
    /**
     * Optional: Visual variant of the input. Defaults to `neutral`.
     */
    variant?: Variant
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Placeholder text to show in the input. Defaults to "Select option".
     */
    placeholder?: string
    /**
     * Optional: Message to show when there are no options. Defaults to "No results found".
     */
    emptyMessage?: string
    /**
     * Optional: Add a button to clear the selection to `undefined`.
     */
    showClearButton?: boolean
    /**
     * Optional: Add a `data-testid` attribute to the dropdown element.
     */
    dataTestid?: string
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'neutral',
    hintMessage: undefined,
    placeholder: 'Select an option',
    emptyMessage: 'No results found',
    dataTestid: undefined,
    searchFunction: undefined,
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

const trigger = ref<HTMLElement>()
const listbox = ref<HTMLElement>()
const listboxWidth = ref<string>()

/**
 * A unique ID for the input component. Used to tie the `label` to the `input`.
 */
const inputComponentId = makeIntoUniqueKey('select')

/**
 * Fake dependency so that we can force the component to re-render when the window width changes.
 */
const forceRefreshKey = ref(1)

/**
 * Watch the trigger element for changes in width. We need to manually force the component to re-render when
 * this happens.
 */
useResizeObserver(trigger, (entries) => {
  const { width } = entries[0].contentRect

  // Set the width of the listbox to the width of the trigger.
  if (listbox.value) {
    listboxWidth.value = `${width}px`
  }

  // Force the component to resize by updating `forceRefreshKey`.
  forceRefreshKey.value++
})

/**
 * Fetch the options when the component is first mounted.
 */
onMounted(async () => {
  searchOptions()
})

// Options ------------------------------------------------------------------------------------------------------------

/**
 * The options are created by the `options` function. We cache them here.
 */
const cachedOptions = ref<Array<MInputSingleSelectDropdownExOption<T>>>([])

/**
 * The empty message to show when there are no options.
 */
const emptyMessage = ref(props.emptyMessage)

/**
 * Default search function that searches the `label` field of the options. Can be overridden by `props.searchFunction`.
 */
const defaultSearchFunction = (
  options: Array<MInputSingleSelectDropdownExOption<T>>,
  query: string
): Array<MInputSingleSelectDropdownExOption<T>> => {
  // Filter the options based on the query.
  return options.filter((option) => {
    const label = option.label.toLocaleLowerCase()
    return label.includes(query.toLocaleLowerCase())
  })
}

/**
 * Fetch the available options.
 * @param query Optional search query to use to filter the options.
 */
function searchOptions(query?: string): void {
  if (!query) {
    cachedOptions.value = props.options
  } else {
    try {
      if (props.searchFunction === undefined) {
        cachedOptions.value = defaultSearchFunction(props.options, query)
      } else {
        cachedOptions.value = props.searchFunction(props.options, query)
      }
    } catch (error: unknown) {
      showErrorNotification(String(error), 'Error while searching in dropdown')
    }
  }
}

/* Zag select only accepts strings as values, we introduce IDs in order to support generic values. */
interface MInputSingleSelectDropdownExOptionWithId<T> extends MInputSingleSelectDropdownExOption<T> {
  id: string
}

/**
 * Options with additional IDs for the select dropdown.
 */
const optionsForSelectDropdown = computed((): Array<MInputSingleSelectDropdownExOptionWithId<T>> => {
  // Generate IDs for all options.
  const options = cachedOptions.value.map((option) => {
    return {
      ...option,
      id: makeHash(option.value),
    }
  }) as Array<MInputSingleSelectDropdownExOptionWithId<T>>

  // Check for duplicates IDs in the options array.
  const uniqueIds = new Set(options.map((option) => option.id))
  if (options.length !== uniqueIds.size) {
    showErrorNotification('See console for more details', 'Duplicate options detected')
    console.error('Duplicate IDs found in options array of MInputSingleSelectDropdown:', options)
  }
  return options
})

/**
 * Maps the current model value to our internal "value with ID".
 */
const modelValueWithId = computed(() => {
  return optionsForSelectDropdown.value.find((option) => isEqual(option.value, props.modelValue))?.id
})

/**
 * Helper to get the selected option based on it's ID.
 * @param id The ID of the option.
 */
function getSelectedOptionFromId(id: string): MInputSingleSelectDropdownExOption<T> | undefined {
  return optionsForSelectDropdown.value.find((option) => option.id === id)
}

// Zag Combobox -------------------------------------------------------------------------------------------------------

/**
 * Zag collection.
 */
const collectionRef = computed(() =>
  combobox.collection({
    items: optionsForSelectDropdown.value,
    isItemDisabled: (item) => !!item.disabled,
    itemToValue: (item) => item.id,
    itemToString: (item) => item.id,
  })
)

const searchInput = ref('')

watch(searchInput, (newValue) => {
  searchOptions(newValue)
})

/**
 * Values to be passed to the state machine context.
 */
const transientContext = computed(
  (): Partial<Context> => ({
    disabled: internalDisabled.value,
    value: modelValueWithId.value ? [modelValueWithId.value] : undefined,
    collection: collectionRef.value,
    inputValue: searchInput.value,
  })
)

/**
 * Zag state machine for the select.
 */
const [state, send] = useMachine(
  combobox.machine({
    id: makeIntoUniqueKey('combobox'),
    collection: collectionRef.value,
    selectionBehavior: 'preserve',
    positioning: {
      placement: 'bottom-start',
      gutter: 0,
    },
    inputBehavior: 'autohighlight',
    openOnKeyPress: true,
    openOnClick: true,
    loopFocus: true,
    onValueChange(details: { items: Array<MInputSingleSelectDropdownExOptionWithId<T>> }) {
      const selectedItem = details.items[0]
      emit('update:modelValue', selectedItem?.value)
      searchInput.value = ''
    },
  }),
  {
    context: transientContext,
  }
)

/**
 * API object that contains all the props, state, methods and event handlers to interact with the select.
 */
const api = computed(() => combobox.connect(state.value, send, normalizeProps))

// Custom Styles ------------------------------------------------------------------------------------------------------

/**
 * Helper to get variant specific classes.
 */
const variantClasses = computed(() => {
  switch (props.variant) {
    case 'danger':
      return 'tw-ring-red-400 tw-text-red-400'
    case 'success':
      return 'tw-ring-green-400 tw-text-green-700'
    case 'warning':
      return 'tw-ring-orange-400 tw-text-orange-500'
    default:
      return 'tw-ring-neutral-300'
  }
})

/**
 * Helper to get variant specific classes for icons.
 */
const iconVariantClasses = computed(() => {
  switch (props.variant) {
    case 'danger':
      return 'tw-bg-red-500'
    case 'success':
      return 'tw-bg-green-500'
    case 'warning':
      return 'tw-bg-orange-500'
    default:
      return 'tw-bg-neutral-400'
  }
})

/**
 * Helper to get the option and its related info.
 * @param option The option to get info for.
 */
function getOptionInfo(option: MInputSingleSelectDropdownExOptionWithId<T>): {
  value: T
  label: string
  highlighted: boolean
  selected: boolean
} {
  return {
    value: option.value,
    label: option.label,
    highlighted:
      (api.value.highlightedItem as MInputSingleSelectDropdownExOptionWithId<T> | undefined)?.id === option.id,
    selected: api.value.selectedItems.some(
      (selectedOption) => (selectedOption as MInputSingleSelectDropdownExOptionWithId<T> | undefined)?.id,
      option.id
    ),
  }
}
</script>

<style scoped>
[data-part='item'][data-highlighted] {
  @apply tw-bg-neutral-200 first:tw-rounded-none;
}

[data-part='item'][data-state='checked'] {
  @apply first:tw-rounded-none;
}

[data-part='item'][data-state='open'] {
  @apply first:tw-border-t-0;
}

[data-part='control'][data-state='open'] {
  @apply tw-rounded-t-md tw-border-b-0;
}

[data-part='control'][data-state='closed'] {
  @apply tw-rounded-md;
}

[data-part='positioner'][data-state='open'] {
  @apply tw-rounded-b-md tw-border-t-0;
}
</style>

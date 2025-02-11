<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Container.
div
  //- Label.
  label(
    v-if="label"
    :for="id"
    :class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1', { 'tw-text-neutral-400': internalDisabled, 'tw-text-neutral-900': !internalDisabled }]"
    ) {{ label }}

  //- Input.
  div(class="tw-relative")
    <!-- @vue-ignore - TS is confused about multiple event handlers -->
    //- Note: added 1Password ignore to prevent their autofill from appearing the input.
    input(
      v-bind="$attrs"
      :type="type"
      :id="id"
      :value="modelValue"
      :disabled="disabled"
      :placeholder="placeholder"
      :class="['tw-w-full tw-rounded-md tw-shadow-sm tw-border-0 tw-py-1.5 tw-text-neutral-900 tw-ring-1 tw-ring-inset placeholder:tw-text-neutral-400 focus:tw-ring-2 focus:tw-ring-inset focus:tw-ring-blue-600 sm:tw-text-sm sm:tw-leading-6 disabled:tw-cursor-not-allowed disabled:tw-bg-neutral-50 disabled:tw-text-neutral-500 disabled:tw-ring-neutral-200 tw-z-0', variantClasses]"
      :aria-invalid="ariaInvalidValue"
      :aria-describedby="hintId"
      @input="(event) => onInput(event.target.value)"
      @blur="$emit('blur:modelValue', internalLatestInputValue)"
      @keyup.enter="$emit('blur:modelValue', internalLatestInputValue)"
      @keyup.esc="$emit('blur:modelValue', internalLatestInputValue)"
      data-1p-ignore
      )

    //- Icon.
    div(class="tw-absolute tw-inset-y-0 tw-right-0 tw-z-10 tw-flex tw-items-center tw-space-x-0.5 tw-pr-3")
      //- Icons from https://heroicons.com/
      <svg v-if="modelValue !== '' && showClearButton" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" class="tw-size-4 tw-fill-neutral-50 tw-p-0.5 tw-rounded-full tw-bg-neutral-400 hover:tw-bg-neutral-500 active:tw-bg-neutral-600 tw-cursor-pointer" @click="onInput(''); $emit('blur:modelValue', '')">
        <path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" />
      </svg>
      <svg v-if="variant === 'danger'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-pointer-events-none tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-red-500" fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 10a1 1 0 100-2 1 1 0 000 2z" clip-rule="evenodd" />
      </svg>
      <svg v-else-if="variant === 'success'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="tw-pointer-events-none tw-w-5 tw-h-5" aria-hidden="true">
        <path class="tw-text-neutral-50" d="M3 10 a7 7 0 1 1 14 0 a7 7 0 1 1 -14 0 Z" />
        <path class="tw-text-green-500" fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clip-rule="evenodd" />
      </svg>
      <svg v-else-if="variant === 'loading'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" class="tw-pointer-events-none tw-w-5 tw-h-5 tw-fill-blue-500 tw-animate-spin" aria-hidden="true">
        <!--! Font Awesome Free 6.4.2 by @fontawesome - https://fontawesome.com License - https://fontawesome.com/license (Commercial License) Copyright 2023 Fonticons, Inc. -->
        <path d="M304 48a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zm0 416a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zM48 304a48 48 0 1 0 0-96 48 48 0 1 0 0 96zm464-48a48 48 0 1 0 -96 0 48 48 0 1 0 96 0zM142.9 437A48 48 0 1 0 75 369.1 48 48 0 1 0 142.9 437zm0-294.2A48 48 0 1 0 75 75a48 48 0 1 0 67.9 67.9zM369.1 437A48 48 0 1 0 437 369.1 48 48 0 1 0 369.1 437z"/>
      </svg>

  //- Hint message.
  MInputHintMessage(
    :id="hintId"
    :variant="variant"
    ) {{ hintMessage }}
</template>

<script setup lang="ts">
import { debounce as lodashDebounce } from 'lodash-es'
import { computed, ref } from 'vue'

import { makeIntoUniqueKey } from '@metaplay/meta-utilities'

import { useEnableAfterSsr } from '../composables/useEnableAfterSsr'
import MInputHintMessage from './MInputHintMessage.vue'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * The value of the input.
     */
    modelValue: string | undefined
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
    variant?: 'default' | 'danger' | 'success' | 'loading'
    /**
     * Optional: Hint message to show below the input.
     */
    hintMessage?: string
    /**
     * Optional: Placeholder text to show in the input.
     */
    placeholder?: string
    /**
     * Optional: Type of the input. Defaults to 'text'.
     */
    type?: 'text' | 'password' | 'email'
    /**
     * Optional: Debounce the input by delaying the update of the model value by the given amount of milliseconds. Defaults to 0.
     */
    debounce?: number
    /**
     * Optional: Show a clear button to set the input to an empty string. Defaults to false.
     */
    showClearButton?: boolean
  }>(),
  {
    modelValue: undefined,
    label: undefined,
    variant: 'default',
    hintMessage: undefined,
    placeholder: undefined,
    type: 'text',
    debounce: 0,
  }
)

const { internalDisabled } = useEnableAfterSsr(computed(() => props.disabled))

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'blur:modelValue': [value: string]
}>()

const id = makeIntoUniqueKey('text-input')
const hintId = makeIntoUniqueKey('text-input-hint')

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

const internalLatestInputValue = ref(props.modelValue)

function onInput(newValue: string): void {
  internalLatestInputValue.value = newValue
  debouncedInput(newValue)
}

const debouncedInput = lodashDebounce((newValue: string) => {
  emit('update:modelValue', newValue)
}, props.debounce)

const ariaInvalidValue = computed((): 'true' | 'false' | undefined => {
  if (props.variant === 'danger') return 'true'
  else if (props.variant === 'success') return 'false'
  else return undefined
})
</script>

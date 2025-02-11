<template lang="pug">
div
  //- Header
  div(
    :class="['tw-flex tw-cursor-pointer', { 'tw-pl-5 !tw-cursor-auto': hideCollapse }, variantClasses]"
    @click="isOpen = !isOpen"
    )
    //- Icon from https://heroicons.com/
    svg(
      v-if="!hideCollapse"
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 20 20"
      fill="currentColor"
      :class="['tw-block tw-w-5 tw-h-5 tw-ml-5 tw-transition-transform tw-relative tw-shrink-0 -tw-left-1', { 'tw-rotate-90': isOpen, 'tw-mt-3': extraMListItemMargin }, iconClasses]"
      style="top: 1px"
      )
      path(
        fill-rule="evenodd"
        d="M7.21 14.77a.75.75 0 01.02-1.06L11.168 10 7.23 6.29a.75.75 0 111.04-1.08l4.5 4.25a.75.75 0 010 1.08l-4.5 4.25a.75.75 0 01-1.06-.02z"
        clip-rule="evenodd"
        )

    div(:class="['tw-grow']")
      <!-- @slot Default: Header content to be displayed. This content is always visible. -->
      slot(name="header")
        span Header content TBD

  //- Body. This is the part that collapses.
  MTransitionCollapse
    div(
      v-if="!hideCollapse && isOpen"
      class="tw-mx-5 tw-my-3"
      )
      <!-- @slot Default: Collapsible content. This content is hidden by default. -->
      slot
        span Body content TBD
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

import MTransitionCollapse from '../primitives/MTransitionCollapse.vue'
import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: Set the color of the hover and active states. Defaults to `neutral`.
     */
    variant?: Variant
    /**
     * Optional: Adds a top margin to accommodate for [MListItem](figure out correct filepath here if possible) being wrapped inside this component.
     */
    extraMListItemMargin?: boolean
    /**
     * Optional: Set to `true` to hide the collapsible section. Defaults to `false`.
     */
    hideCollapse?: boolean
    /**
     * Optional: Set the initial state of the collapse to be open. Defaults to `false`.
     */
    isOpenByDefault?: boolean
    /**
     * Optional: Additional classes for the icon. Useful for aligning the icon with the text.
     */
    iconClasses?: string
  }>(),
  {
    variant: 'neutral',
    extraMListItemMargin: false,
    hideCollapse: false,
    isOpenByDefault: false,
    iconClasses: undefined,
  }
)
/**
 * Whether the collapse is open or closed.
 */
const isOpen = ref(props.isOpenByDefault)

/**
 * The classes for the variant.
 */
const variantClasses = computed(() => {
  if (props.hideCollapse) {
    return 'hover:tw-bg-neutral-100'
  }
  const classes: Record<string, string> = {
    primary: 'hover:tw-bg-blue-200 active:tw-bg-blue-300',
    warning: 'hover:tw-bg-orange-200 active:tw-bg-orange-300',
    success: 'hover:tw-bg-green-200 active:tw-bg-green-300',
    danger: 'hover:tw-bg-red-200 active:tw-bg-red-300',
    neutral: 'hover:tw-bg-neutral-200 active:tw-bg-neutral-300',
  }
  return classes[props.variant] ?? (undefined as never)
})
</script>

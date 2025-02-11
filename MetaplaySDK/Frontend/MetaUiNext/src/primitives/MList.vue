<template lang="pug">
//- Container
div(:class="['tw-overflow-hidden', variantClassesBorder]")
  //- List root
  ul(
    role="list"
    :class="['tw-divide-y', variantClassesDivider, 'tw-m-0', { '[&>*:nth-child(even)]:tw-bg-neutral-100': striped, 'tw-flex tw-flex-wrap tw-justify-start tw-gap-4 tw-px-5 tw-pb-5 tw-divide-y-0': horizontal }]"
    )
    <!-- @slot Default: List content to be displayed (HTML/components supported). -->
    slot
</template>
<script lang="ts" setup>
import { computed } from 'vue'

import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: If true, a border will be applied to the list.
     */
    showBorder?: boolean
    /**
     * Optional: The variant color to be applied to the list.
     */
    variant?: Variant
    /**
     * Optional: If true, the list will be striped.
     */
    striped?: boolean
    /**
     * Optional: If true, the list will be displayed horizontally.
     */
    horizontal?: boolean
  }>(),
  {
    variant: 'neutral',
  }
)

/**
 * Variant color to be applied to the divider when the variant prop is set.
 */
const variantClassesDivider = computed(() => {
  const variantToDividerClasses: Record<string, string> = {
    primary: 'tw-divide-blue-300',
    success: 'tw-divide-green-300',
    danger: 'tw-divide-red-300',
    warning: 'tw-divide-orange-300',
    default: 'tw-divide-neutral-300',
  }
  return variantToDividerClasses[props.variant] || variantToDividerClasses.default
})

/**
 * Border to be applied to the list when the showBorder prop is set.
 * Additionally a border color will be set according to the variant prop.
 */
const variantClassesBorder = computed(() => {
  if (props.showBorder) {
    const variantBorderColorClasses: Record<string, string> = {
      primary: 'tw-border-blue-300',
      success: 'tw-border-green-300',
      danger: 'tw-border-red-300',
      warning: 'tw-border-orange-300',
      default: 'tw-border-neutral-300',
    }
    const colorClass = variantBorderColorClasses[props.variant] || variantBorderColorClasses.default
    return `tw-border tw-rounded-md ${colorClass}`
  } else {
    return undefined
  }
})
</script>

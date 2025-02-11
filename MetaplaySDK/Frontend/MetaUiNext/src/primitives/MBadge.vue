<template lang="pug">
MTooltip(
  :content="tooltip"
  noUnderline
  )
  //- Dynamic scaling of font-size and line-height below causes problems when scaling of browser isn't exactly 100%. Looks bad at 90% or 110% where the bottom has more padding than top in some places.
  span(
    :class="[props.class, 'tw-font-semibold tw-py-0.5 tw-transition-colors tw-inline-flex tw-text-center tw-items-center', { 'tw-px-1': shape === 'default', 'tw-px-1.5': shape === 'pill' }, variantClasses, shapeClasses]"
    style="font-size: 77%; line-height: 130%; vertical-align: baseline"
    )
    div(
      v-if="$slots.icon"
      class="mbadge-icon-container tw-ml-0.5 tw-mr-1"
      )
      <!-- @slot Optional: Slot to add an icon (HTML/components supported). -->
      slot(name="icon")

    span(:class="`tw-drop-shadow-sm tw-break-all ${tooltip && shape === 'default' ? 'border-bottom-dashed' : ''}`")
      <!-- @slot Default: The main content to display. -->
      slot Content TBD
</template>

<script setup lang="ts">
import { computed } from 'vue'

import type { Variant } from '../utils/types'
import MTooltip from './MTooltip.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: Setting this to 'pill' will round the badge's corners.
     */
    shape?: 'default' | 'pill'
    /**
     * Optional: The visual style of the badge.
     */
    variant?: Variant
    /**
     * Optional: Add a tooltip to the badge element.
     */
    tooltip?: string
    /**
     * Optional: Classes to apply to the badge's trigger element. This prop is fixing a Vue bug where classes are not inherited correctly.
     */
    class?: string
  }>(),
  {
    shape: 'default',
    variant: 'neutral',
    tooltip: undefined,
    class: undefined,
  }
)

const shapeClasses = computed(() => {
  if (props.shape === 'pill') return 'tw-rounded-full'
  else return 'tw-rounded'
})

const variantClasses = computed(() => {
  const defaultClasses: Record<string, string> = {
    primary: 'tw-bg-blue-500 tw-text-white',
    success: 'tw-bg-green-500 tw-text-white',
    warning: 'tw-bg-orange-500 tw-text-white',
    danger: 'tw-bg-red-500 tw-text-white',
    neutral: 'tw-bg-neutral-500 tw-text-white',
  }

  return defaultClasses[props.variant] || defaultClasses.neutral
})
</script>

<style>
/* Set the height of all child svg elements */
.mbadge-icon-container > svg {
  height: 0.7rem;
}
</style>

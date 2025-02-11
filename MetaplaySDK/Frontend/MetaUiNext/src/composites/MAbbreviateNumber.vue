<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- This component prints potentially large numbers in a space efficient and humanized format. -->

<template lang="pug">
MTooltip(:content="tooltipContent") {{ numberDisplayString }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { abbreviateNumber } from '@metaplay/meta-utilities'

import MTooltip from '../primitives/MTooltip.vue'

const props = defineProps<{
  /**
   * The number to abbreviate.
   */
  number: number
  /**
   * Optional: Text label to show after the number.
   * @example 'byte'
   */
  unit?: string
  /**
   * Optional: Round the number down to the nearest integer.
   */
  roundDown?: boolean
  /**
   * Optional: Do not show the exact number in a tooltip.
   */
  disableTooltip?: boolean
}>()

/**
 * Number to be abbreviated after rounding down if roundDown is true.
 */
const roundedNumber = computed(() => {
  return props.roundDown ? Math.floor(props.number) : props.number
})

/**
 * String representing the shortened form of the number value.
 */
const abbreviatedNumberString = computed(() => {
  return abbreviateNumber(roundedNumber.value)
})

/**
 * Hides the default tooltip showing the exact number value when the abbreviated and final values are equal
 * or disableTooltip is passed other wise shows string representing the full number value.
 */
const tooltipContent = computed(() => {
  if (abbreviatedNumberString.value === roundedNumber.value.toString() || props.disableTooltip) {
    return undefined
  } else {
    // Format number to locale string to show thousand separators.
    return props.number.toLocaleString()
  }
})

/**
 * The final string to display.
 */
const numberDisplayString = computed(() => {
  if (props.unit) {
    return `${abbreviatedNumberString.value} ${props.unit}${roundedNumber.value === 1 ? '' : 's'}`
  } else {
    return abbreviatedNumberString.value
  }
})
</script>

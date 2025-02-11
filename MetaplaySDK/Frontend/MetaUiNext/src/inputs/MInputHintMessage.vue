<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  v-if="$slots.default"
  :id="id"
  :class="['tw-text-xs tw-text-neutral-400 tw-mt-1', variantClasses]"
  )
  slot
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ComputedRef } from 'vue'

import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * Optional: ID to associate with the hint message.
     */
    id?: string
    /**
     * Optional: The visual style of the hint message.
     */
    // eslint-disable-next-line @typescript-eslint/no-redundant-type-constituents -- In some components, the variant passed here is a string.
    variant?: string | Variant
  }>(),
  {
    id: undefined,
    variant: undefined,
  }
)

const variantClasses: ComputedRef<string> = computed(() => {
  const classes: Record<string, string> = {
    danger: 'tw-text-red-400',
    success: 'tw-text-green-400',
    warning: 'tw-text-orange-400',
  }
  return props.variant ? classes[props.variant] : ''
})
</script>

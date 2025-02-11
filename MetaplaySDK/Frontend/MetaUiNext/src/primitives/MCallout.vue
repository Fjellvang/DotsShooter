<template lang="pug">
div(:class="['tw-border tw-rounded-md tw-py-2 tw-px-3', variantClasses]")
  div(class="tw-mb-1 tw-flex tw-flex-wrap")
    //- Title.
    span(
      role="heading"
      :class="['tw-font-semibold tw-mr-1 tw-text-ellipsis tw-overflow-hidden']"
      ) {{ title }}
    //- Badge
    span
      <!-- @slot Optional: Slot to add a custom badge component -->
      slot(name="badge")

  //- Body.
  div(class="tw-overflow-x-auto")
    <!-- @slot Default: Main callout content to be displayed (HTML/components supported). -->
    slot

  //- Buttons.
  //- TODO: Spend some effort to review and make this nice.
  MButtonGroupLayout(
    v-if="$slots.buttons"
    class="tw-mt-2"
    )
    <!-- @slot Optional: Slot to add custom buttons. -->
    slot(name="buttons")
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MButtonGroupLayout from '../layouts/MButtonGroupLayout.vue'
import type { Variant } from '../utils/types'

const props = withDefaults(
  defineProps<{
    /**
     * The title of the callout.
     */
    title: string
    /**
     * Contextual colors for the callout. Defaults to 'warning'.
     */
    variant?: Variant
  }>(),
  {
    variant: 'warning',
  }
)

const variantClasses = computed(() => {
  const variantToClasses: Record<string, string> = {
    warning: 'tw-border-orange-200 tw-bg-orange-100 tw-text-orange-900',
    danger: 'tw-border-red-200 tw-bg-red-100 tw-text-red-900',
    success: 'tw-border-green-200 tw-bg-green-100 tw-text-green-900',
    default: 'tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600',
  }
  return variantToClasses[props.variant] || variantToClasses.default
})
</script>

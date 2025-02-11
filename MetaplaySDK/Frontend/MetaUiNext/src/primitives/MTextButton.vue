<template lang="pug">
MTooltip(
  :content="attributes.tooltipContent"
  no-underline
  )
  Component(
    v-bind="{ ...$attrs, ...attributes }"
    :is="attributes.is"
    class="tw-inline-flex tw-items-center tw-space-x-2 tw-text-center"
    @click.stop="emit('click')"
    )
    //- Icon slot
    <!-- @slot Optional: Slot to add an icon (HTML/components supported). -->
    slot(
      v-if="$slots.icon"
      name="icon"
      )

    <!-- @slot Default: Link text to be displayed. -->
    slot Link Text TBD
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MTooltip from '../primitives/MTooltip.vue'
import type { Variant } from '../utils/types'
import { useMTextButton } from './useMTextButton'

defineOptions({
  inheritAttrs: false,
})

const props = withDefaults(
  defineProps<{
    /**
     * Optional: The route to navigate to when the button is clicked.
     */
    to?: string
    /**
     * Optional: Disable the button and show a tooltip with the given text.
     */
    disabledTooltip?: string
    /**
     * Optional: Set the visual variant of the text button. Defaults to 'primary'.
     */
    variant?: Variant
    /**
     * Optional: The permission required to use this button. If the user does not have this permission the button will be disabled with a tooltip.
     */
    permission?: string
  }>(),
  {
    to: undefined,
    disabledTooltip: undefined,
    variant: 'primary',
    permission: undefined,
  }
)

const emit = defineEmits(['click'])

const attributes = useMTextButton(computed(() => props))
</script>

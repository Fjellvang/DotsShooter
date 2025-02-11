<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->
<!-- A minimal component to display a standard disclaimer. -->

<template lang="pug">
MCallout(
  :variant="variant ? variant : 'warning'"
  title="⚠️ No seatbelts!"
  )
  p(class="tw-m-0")
    span(v-if="message") {{ message }}
    span(v-else) This action can't be undone.
  span(
    v-if="name"
    class="tw-space-x-1"
    ) Are you sure #[MBadge(variant="warning") {{ name }}] is ok with this?

  div(
    v-if="$slots.default"
    class="tw-mt-1"
    )
    slot
</template>

<script lang="ts" setup>
import { MBadge, MCallout, type Variant } from '@metaplay/meta-ui-next'

defineProps<{
  /**
   * Optional: Alternative variant color for the alert box.
   */
  variant?: Variant
  /**
   * Optional:  The name of the entity that an action is being performed upon. E.g a player.
   */
  name?: string
  /**
   * Optional: Content to be displayed in the alert box.
   */
  message?: string
}>()
</script>

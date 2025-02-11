<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  class="m-two-column-parent tw-flex tw-flex-wrap tw-justify-center @2xl:tw-gap-x-3"
  :class="[{ 'tw-items-start': !stretchItemsVertically }]"
  )
  slot
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  stretchItemsVertically?: boolean
}>()
</script>

<style>
/* Direct children of the layout container. */
.m-two-column-parent > * {
  @apply tw-mb-3 tw-w-full;
}

/* Force to two columns on 2XL container size. */
@container (min-width: 42rem) {
  .m-two-column-parent > * {
    max-width: calc(
      50% - 0.375rem
    ); /* Single column width is 50% of the available size minus half of the margin between columns. */
  }
}
</style>

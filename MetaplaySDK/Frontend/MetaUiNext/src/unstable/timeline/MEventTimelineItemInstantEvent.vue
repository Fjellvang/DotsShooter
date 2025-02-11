<template lang="pug">
div(
  v-bind="$attrs"
  class="tw-absolute tw-flex tw-cursor-pointer tw-rounded-full tw-border tw-border-neutral-50 tw-shadow tw-filter hover:tw-brightness-90 active:tw-brightness-75"
  :class="{ 'tw-ring tw-ring-blue-500 tw-ring-offset-1': selected }"
  :style="renderInfo.styles"
  @click="$emit('click')"
  )
</template>

<script setup lang="ts">
import type { TimelineItemRenderInfoInstantEvent } from './MEventTimelineItemInstantEventTypes'

defineProps<{
  renderInfo: TimelineItemRenderInfoInstantEvent
  selected?: boolean
}>()

defineOptions({
  inheritAttrs: false,
})
</script>

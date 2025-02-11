<template lang="pug">
MEventTimelineInspectorSingle(
  :id="id"
  :version="version"
  title="Instant Event"
  :description="details.description"
  @close="emit('close')"
  @invoke-command="emit('invokeCommand', $event)"
  )
  template(#title-badge)
    MBadge instant
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MBadge from '../../primitives/MBadge.vue'
import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import type { TimelineItemDetailsInstantEvent } from './MEventTimelineItemInstantEventTypes'
import type { TimelineItemDetails } from './MEventTimelineTypes'
import type { ToServerCommand } from './timelineCommands'

const emit = defineEmits({
  close: () => ({ type: 'close' as const }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'command', command }),
})

const props = defineProps<{
  id: string
  version: number
  rawDetails: TimelineItemDetails
}>()

const details = computed(() => {
  return props.rawDetails as TimelineItemDetailsInstantEvent
})
</script>

<template lang="pug">
MEventTimelineInspectorSingle(
  :id="id"
  :version="version"
  :title="details.displayName"
  description="Sections are used to organize groups in the timeline."
  @close="emit('close')"
  @invoke-command="emit('invokeCommand', $event)"
  )
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import type { TimelineItemDetails, TimelineItemDetailsSection } from './MEventTimelineTypes'
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
  return props.rawDetails as TimelineItemDetailsSection
})
</script>

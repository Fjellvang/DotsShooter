<template lang="pug">
MEventTimelineInspectorSingle(
  :id="id"
  :version="version"
  :title="details.displayName"
  description="Groups are used to organize rows in the timeline."
  show-title-editor
  show-color-picker
  :current-color="details.details.color"
  @close="emit('close')"
  @invoke-command="emit('invokeCommand', $event)"
  )
  template(#buttons)
    MButton(
      size="small"
      :disabled-tooltip="moveDisabledTooltip('down')"
      permission="api.liveops_events.edit"
      @click="onMove('down')"
      ) Move Down
    MButton(
      size="small"
      :disabled-tooltip="moveDisabledTooltip('up')"
      permission="api.liveops_events.edit"
      @click="onMove('up')"
      ) Move Up
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MButton from '../../primitives/MButton.vue'
import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import {
  timelineItemSectionHelper,
  type TimelineData,
  type TimelineItemDetails,
  type TimelineItemDetailsGroup,
} from './MEventTimelineTypes'
import { isParentImmutable } from './MEventTimelineUtils'
import type { ToServerCommand, ToServerCommandMoveItems } from './timelineCommands'

const emit = defineEmits({
  close: () => ({ type: 'close' as const }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'command', command }),
})

const props = defineProps<{
  id: string
  version: number
  visibleTimelineData?: TimelineData
  rawDetails: TimelineItemDetails
}>()

const details = computed(() => {
  return props.rawDetails as TimelineItemDetailsGroup
})

function moveDisabledTooltip(direction: 'up' | 'down'): string | undefined {
  if (isParentImmutable(props.id, props.visibleTimelineData?.items)) {
    return 'Cannot move. Parent is immutable.'
  } else {
    const { parentItem, childIndex } = timelineItemSectionHelper.getParentAs(props.id, props.visibleTimelineData?.items)
    const newIndex = childIndex + (direction === 'up' ? -1 : 2)
    if (newIndex < 0) {
      return 'Cannot move. Already at the top.'
    } else if (newIndex > parentItem.hierarchy.childIds.length) {
      return 'Cannot move. Already at the bottom.'
    } else {
      return undefined
    }
  }
}

function onMove(direction: 'up' | 'down'): void {
  const { parentId, parentItem, childIndex } = timelineItemSectionHelper.getParentAs(
    props.id,
    props.visibleTimelineData?.items
  )
  const newIndex = childIndex + (direction === 'up' ? -1 : 2)
  const command: ToServerCommandMoveItems = {
    commandType: 'moveItems',
    items: [
      {
        targetId: props.id,
        currentVersion: props.version,
        parentVersion: parentItem.version,
      },
    ],
    newParent: {
      targetId: parentId,
      currentVersion: parentItem.version,
      insertIndex: newIndex,
    },
  }
  emit('invokeCommand', command)
}
</script>

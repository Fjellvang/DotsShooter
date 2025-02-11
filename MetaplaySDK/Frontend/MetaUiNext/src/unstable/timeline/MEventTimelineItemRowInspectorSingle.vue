<template lang="pug">
MEventTimelineInspectorSingle(
  :id="id"
  :version="version"
  :title="details.displayName"
  description="Rows are used to organize items in the timeline."
  :show-title-editor="!isImmutable"
  @close="emit('close')"
  @invoke-command="emit('invokeCommand', $event)"
  )
  template(#buttons)
    MButton(
      size="small"
      :disabled-tooltip="moveDisabledTooltip('down')"
      permission="api.liveops_events.edit"
      @click="() => { if (moveDownCommand) emit('invokeCommand', moveDownCommand) }"
      ) Move Down
    MButton(
      size="small"
      :disabled-tooltip="moveDisabledTooltip('up')"
      permission="api.liveops_events.edit"
      @click="() => { if (moveUpCommand) emit('invokeCommand', moveUpCommand) }"
      ) Move Up
</template>

<script setup lang="ts">
import { computed } from 'vue'

import MButton from '../../primitives/MButton.vue'
import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import {
  timelineItemGroupHelper,
  timelineItemSectionHelper,
  type TimelineData,
  type TimelineItemDetails,
  type TimelineItemDetailsRow,
} from './MEventTimelineTypes'
import type { ToServerCommand, ToServerCommandMoveItems } from './timelineCommands'

const emit = defineEmits({
  close: () => ({ type: 'close' as const }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'command', command }),
})

const props = defineProps<{
  id: string
  version: number
  visibleTimelineData?: TimelineData
  isImmutable: boolean // This is a bit cheeky as it comes from the `data`. The `details` should know if it's immutable.
  rawDetails: TimelineItemDetails
}>()

const details = computed(() => {
  return props.rawDetails as TimelineItemDetailsRow
})

function moveDisabledTooltip(direction: 'up' | 'down'): string | undefined {
  if (direction === 'up' && !moveUpCommand.value) {
    return 'Cannot move. Already at the top.'
  } else if (direction === 'down' && !moveDownCommand.value) {
    return 'Cannot move. Already at the bottom.'
  } else {
    return undefined
  }
}

/**
 * Calculate the command to move the item up. Result is `undefined` if the item cannot move up.
 */
const moveUpCommand = computed((): ToServerCommand | undefined => {
  // Try to move down inside current group.
  const {
    parentId: groupId,
    parentItem: groupItem,
    childIndex: rowIndex,
  } = timelineItemGroupHelper.getParentAs(props.id, props.visibleTimelineData?.items)
  if (rowIndex > 0) {
    const newIndex = rowIndex - 1
    const command: ToServerCommandMoveItems = {
      commandType: 'moveItems',
      items: [
        {
          targetId: props.id,
          currentVersion: props.version,
          parentVersion: groupItem.version,
        },
      ],
      newParent: {
        targetId: groupId,
        currentVersion: groupItem.version,
        insertIndex: newIndex,
      },
    }

    // Success.
    return command
  }

  // Next, try to move up to the next group.
  const { parentItem: sectionItem, childIndex: groupIndex } = timelineItemSectionHelper.getParentAs(
    groupId,
    props.visibleTimelineData?.items
  )
  if (groupIndex > 0) {
    const nextGroupId = sectionItem.hierarchy.childIds[groupIndex - 1]
    const nextGroupItem = timelineItemGroupHelper.getAs(nextGroupId, props.visibleTimelineData?.items)
    const newIndex = nextGroupItem.hierarchy.childIds.length
    const command: ToServerCommandMoveItems = {
      commandType: 'moveItems',
      items: [
        {
          targetId: props.id,
          currentVersion: props.version,
          parentVersion: sectionItem.version,
        },
      ],
      newParent: {
        targetId: nextGroupId,
        currentVersion: nextGroupItem.version,
        insertIndex: newIndex,
      },
    }

    // Success.
    return command
  }

  // Cannot move up.
  return undefined
})

/**
 * Calculate the command to move the item down. Result is `undefined` if the item cannot move down.
 */
const moveDownCommand = computed((): ToServerCommand | undefined => {
  // Try to move down inside current group.
  const {
    parentId: groupId,
    parentItem: groupItem,
    childIndex: rowIndex,
  } = timelineItemGroupHelper.getParentAs(props.id, props.visibleTimelineData?.items)
  if (rowIndex < groupItem.hierarchy.childIds.length - 1) {
    const newIndex = rowIndex + 2
    const command: ToServerCommandMoveItems = {
      commandType: 'moveItems',
      items: [
        {
          targetId: props.id,
          currentVersion: props.version,
          parentVersion: groupItem.version,
        },
      ],
      newParent: {
        targetId: groupId,
        currentVersion: groupItem.version,
        insertIndex: newIndex,
      },
    }

    // Success.
    return command
  }

  // Next, try to move down to the next group.
  const { parentItem: sectionItem, childIndex: groupIndex } = timelineItemSectionHelper.getParentAs(
    groupId,
    props.visibleTimelineData?.items
  )
  if (groupIndex < sectionItem.hierarchy.childIds.length - 1) {
    const nextGroupId = sectionItem.hierarchy.childIds[groupIndex + 1]
    const nextGroupItem = timelineItemGroupHelper.getAs(nextGroupId, props.visibleTimelineData?.items)
    const newIndex = 0
    const command: ToServerCommandMoveItems = {
      commandType: 'moveItems',
      items: [
        {
          targetId: props.id,
          currentVersion: props.version,
          parentVersion: sectionItem.version,
        },
      ],
      newParent: {
        targetId: nextGroupId,
        currentVersion: nextGroupItem.version,
        insertIndex: newIndex,
      },
    }

    // Success.
    return command
  }

  // Cannot move down.
  return undefined
})
</script>

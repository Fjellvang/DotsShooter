<template lang="pug">
MEventTimelineInspectorSingle(
  :id="id"
  :version="version"
  :title="details.displayName"
  :description="details.description"
  :current-color="details.details.eventParams.color"
  @close="emit('close')"
  @invoke-command="emit('invokeCommand', $event)"
  )
  template(#title-badge)
    MBadge(:variant="liveOpsEventPhaseInfos[details.details.currentPhase].badgeVariant") {{ liveOpsEventPhaseInfos[details.details.currentPhase].displayString }}

  template(#default)
    //- Event details.
    //- div(class="tw-space-y-3 tw-px-4 tw-pb-4")
      //- div(class="tw-flex tw-flex-row-reverse tw-items-center tw-justify-between")
        //- Lock toggle.
        span(class="tw-inline-flex tw-items-center tw-space-x-1")
          span(class="tw-mr-1 tw-text-sm tw-italic tw-text-neutral-600") {{ singleSelectedItem.data.isImmutable ? 'Immutable' : singleSelectedItem.data.isLocked ? 'Locked' : 'Unlocked' }}
          //- TODO: This modify server state instead.
          MInputSwitch(
            v-if="!singleSelectedItem.data.isImmutable"
            v-model="singleSelectedItem.data.isLocked"
            class="tw-relative tw-top-[2px]"
            size="small"
            )

    //- Targeting.
    div(class="tw-my-2 tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-sm")
      div(class="tw-mb-1 tw-mr-1 tw-font-bold") Targeting
      ul(v-if="details.details.eventParams.targetPlayers.length || details.details.eventParams.targetCondition")
        li(class="tw-flex tw-justify-between")
          div Segments
          div(v-if="targetConditionsSegmentCount === 0") None
          div(v-else-if="targetConditionsSegmentCount === 1") 1
          div(v-else-if="details.details.eventParams?.targetCondition?.requireAnySegment") Any of {{ targetConditionsSegmentCount }}
          div(v-else-if="details.details.eventParams?.targetCondition?.requireAllSegment") All of {{ targetConditionsSegmentCount }}
        li(class="tw-flex tw-justify-between")
          div Selected players
          MAbbreviateNumber(
            v-if="details.details.eventParams.targetPlayers.length"
            :number="details.details.eventParams.targetPlayers.length"
            )
          div(v-else) None
      div(
        v-else
        class="tw-text-xs tw-text-neutral-600"
        ) No targeting.

    //- Schedule.
    div(class="tw-my-2 tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-sm")
      div(class="tw-mb-1 tw-mr-1 tw-font-bold") Schedule
        span(v-if="!details.details.eventParams.useSchedule")
        span(
          v-else
          class="tw-ml-1 tw-text-xs tw-font-normal tw-text-neutral-600"
          ) {{ details.details.eventParams.schedule.isPlayerLocalTime ? 'Player Local' : 'UTC' }}
      ul(v-if="details.details.eventParams.useSchedule")
        li(class="tw-flex tw-justify-between")
          div Preview
          div(v-if="details.details.eventParams.schedule.previewDuration === 'P0Y0M0DT0H0M0S'") None
          meta-duration(
            v-else
            :duration="details.details.eventParams.schedule.previewDuration"
            show-as="exactDuration"
            hideMilliseconds
            )
        li(class="tw-flex tw-justify-between")
          div Event start
          MDateTime(
            :instant="DateTime.fromISO(details.details.eventParams.schedule.enabledStartTime)"
            :disable-tooltip="details.details.eventParams.schedule?.isPlayerLocalTime"
            )
        li(class="tw-flex tw-justify-between")
          div Ending soon
          div(v-if="details.details.eventParams.schedule.endingSoonDuration === 'P0Y0M0DT0H0M0S'") None
          meta-duration(
            v-else
            :duration="details.details.eventParams.schedule.endingSoonDuration"
            show-as="exactDuration"
            hideMilliseconds
            )
        li(class="tw-flex tw-justify-between")
          div Event end
          MDateTime(
            :instant="DateTime.fromISO(details.details.eventParams.schedule.enabledEndTime)"
            :disable-tooltip="details.details.eventParams.schedule?.isPlayerLocalTime"
            )
        li(class="tw-flex tw-justify-between")
          div Review
          div(v-if="details.details.eventParams.schedule.reviewDuration === 'P0Y0M0DT0H0M0S'") None
          meta-duration(
            v-else
            :duration="details.details.eventParams.schedule.reviewDuration"
            show-as="exactDuration"
            hideMilliseconds
            )
        div(class="tw-mt-2 tw-text-xs tw-text-neutral-600")
          div Active for #[meta-duration(:duration="getTotalDurationForEvent(false)" show-as="exactDuration" hideMilliseconds)].
          div Visible for #[meta-duration(:duration="getTotalDurationForEvent(true)" show-as="exactDuration" hideMilliseconds)] total.
      div(
        v-else
        class="tw-text-xs tw-text-neutral-600"
        ) No schedule.

    //- Parameters.
    div(class="tw-my-2 tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-sm")
      div(class="tw-mb-1 tw-mr-1 tw-font-bold") Parameters
      pre(
        v-if="details.details.eventParams.content"
        class="tw-text-xs"
        ) {{ humanizedParameters }}
      div(
        v-else
        class="tw-text-xs tw-text-neutral-600"
        ) No parameters.

    //- Stats.
    div(class="tw-my-2 tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-2 tw-text-sm")
      div(class="tw-mb-1 tw-mr-1 tw-font-bold") Stats
      ul
        li(class="tw-flex tw-justify-between")
          div Event Type
          MBadge {{ details.details.eventTypeName }}
        li(class="tw-flex tw-justify-between")
          div Total Participants
          div {{ details.details.participantCount }}

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
    MButton(
      size="small"
      :to="`/liveOpsEvents/${id.split(':')[1]}`"
      permission="api.liveops_events.view"
      ) View Event
</template>

<script setup lang="ts">
import { DateTime, Duration } from 'luxon'
import { computed } from 'vue'

import MAbbreviateNumber from '../../composites/MAbbreviateNumber.vue'
import MBadge from '../../primitives/MBadge.vue'
import MButton from '../../primitives/MButton.vue'
import type { Variant } from '../../utils/types'
import MDateTime from '../MDateTime.vue'
import MEventTimelineInspectorSingle from './MEventTimelineInspectorSingle.vue'
import type { TimelineItemDetailsLiveopsEvent } from './MEventTimelineItemLiveopsEventTypes'
import type { TimelineData, TimelineItem, TimelineItemDetails } from './MEventTimelineTypes'
import { timelineItemHelper, timelineItemRowHelper } from './MEventTimelineTypes'
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

// ----

const details = computed(() => {
  return props.rawDetails as TimelineItemDetailsLiveopsEvent
})

const targetConditionsSegmentCount = computed(() => {
  const anyCount: number = details.value?.details.eventParams.targetCondition?.requireAnySegment.length ?? 0
  if (anyCount) return anyCount

  const allCount: number = details.value?.details.eventParams.targetCondition?.requireAllSegment.length ?? 0
  if (allCount) return allCount

  return 0
})

function getTotalDurationForEvent(includePreviewAndReview: boolean): Duration {
  const schedule = details.value?.details.eventParams.schedule
  if (!schedule) return Duration.fromObject({})

  const previewDuration = includePreviewAndReview
    ? Duration.fromISO(schedule.previewDuration as string)
    : Duration.fromObject({})
  const reviewDuration = includePreviewAndReview
    ? Duration.fromISO(schedule.reviewDuration as string)
    : Duration.fromObject({})

  const startTime = DateTime.fromISO(schedule.enabledStartTime as string)
  const endTime = DateTime.fromISO(schedule.enabledEndTime as string)

  return endTime.diff(startTime).plus(previewDuration).plus(reviewDuration)
}

const humanizedParameters = computed((): string => {
  const parameters: string = JSON.stringify(details.value?.details.eventParams.content as string, null, 2)
    .split('\n')
    .filter((x) => !x.includes('"$type":')) // Hide type information.
    .filter((x) => x !== '{' && x !== '}') // Remove outermost braces.
    .map((x) => x.replace(/( *)"(.*)":/, '$1$2:')) // Remove quotes from around keys.
    .map((x) => x.replace(/^ {2}/, '')) // Reduce indent.
    .map((x) => x.replace(/,$/, '')) // Remove trailing commas.
    .join('\n')

  return parameters
})

// NB: Copy/pasted from `liveOpsEventUtils` in Core.
const liveOpsEventPhaseInfos: Record<string, { displayString: string; badgeVariant: Variant }> = {
  NotYetStarted: {
    displayString: 'Scheduled',
    badgeVariant: 'neutral',
  },
  InPreview: {
    displayString: 'In Preview',
    badgeVariant: 'primary',
  },
  Active: {
    displayString: 'Active',
    badgeVariant: 'success',
  },
  EndingSoon: {
    displayString: 'Ending Soon',
    badgeVariant: 'success',
  },
  InReview: {
    displayString: 'In Review',
    badgeVariant: 'primary',
  },
  Ended: {
    displayString: 'Concluded',
    badgeVariant: 'neutral',
  },
}

// ----

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
  if (!props.visibleTimelineData) {
    return undefined
  }
  const items = props.visibleTimelineData.items
  const itemId = props.id
  const item = timelineItemHelper.getAs(itemId, items)

  // Get the current row (this item's parent) and find the next row above that.
  // That next row (if any) is the new parent.

  const { parentId: rowId, parentItem: rowItem } = timelineItemRowHelper.getParentAs(itemId, items)

  const aboveRowId = tryGetAboveItemId(rowId, items)
  if (!aboveRowId) {
    return undefined
  }
  const aboveRow = timelineItemRowHelper.getAs(aboveRowId, items)

  const command: ToServerCommandMoveItems = {
    commandType: 'moveItems',
    items: [
      {
        targetId: itemId,
        currentVersion: item.version,
        parentVersion: rowItem.version,
      },
    ],
    newParent: {
      targetId: aboveRowId,
      currentVersion: aboveRow.version,
      insertIndex: 0, // Index doesn't matter for rows' children.
    },
  }

  return command
})

/**
 * Calculate the command to move the item down. Result is `undefined` if the item cannot move down.
 */
const moveDownCommand = computed((): ToServerCommand | undefined => {
  if (!props.visibleTimelineData) {
    return undefined
  }
  const items = props.visibleTimelineData.items
  const itemId = props.id
  const item = timelineItemHelper.getAs(itemId, items)

  // Get the current row (this item's parent) and find the next row below that.
  // That next row (if any) is the new parent.

  const { parentId: rowId, parentItem: rowItem } = timelineItemRowHelper.getParentAs(itemId, items)

  const belowRowId = tryGetBelowItemId(rowId, items)
  if (!belowRowId) {
    return undefined
  }
  const belowRow = timelineItemRowHelper.getAs(belowRowId, items)

  const command: ToServerCommandMoveItems = {
    commandType: 'moveItems',
    items: [
      {
        targetId: itemId,
        currentVersion: item.version,
        parentVersion: rowItem.version,
      },
    ],
    newParent: {
      targetId: belowRowId,
      currentVersion: belowRow.version,
      insertIndex: 0, // Index doesn't matter for rows' children.
    },
  }

  return command
})

/**
 * Helper for @see moveUpCommand: find the next item above the specified item, of the same rank
 * (i.e. at the same depth in the item tree).
 * For example, if the specified item is a row, this finds the row above it, possibly belonging to a different parent.
 * @param itemId The ID of the item to find the one above.
 * @param items The items to search in.
 * @returns The ID of the item above, or `undefined` if this is the topmost item of its rank.
 */
function tryGetAboveItemId(itemId: string, items: Record<string, TimelineItem>): string | undefined {
  // Look up item.
  const item = items[itemId]

  // We're at root of the hierarchy if the item has no parent.
  if (!item.hierarchy.parentId) {
    return undefined
  }

  // Get parent and find item's index in parent.
  const possibleParentId = item.hierarchy.parentId as string | undefined
  console.assert(!!possibleParentId, 'Must have parent.')
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- We just proved the parent exists.
  const parentId = possibleParentId!
  const parentItem = items[parentId]
  const parentChildIds = parentItem.hierarchy.childIds as string[]
  const itemIndexInParent = parentChildIds.indexOf(itemId)

  if (itemIndexInParent - 1 >= 0) {
    // This isn't the topmost (first) child of its parent,
    // so the result is found in the same parent.
    return parentChildIds[itemIndexInParent - 1]
  } else {
    // This is the topmost (first) child of its parent.
    // The result is found by finding the first parent above the current parent
    // that has a non-empty child list and taking its bottommost (last) child.
    for (
      let nextParentId = tryGetAboveItemId(parentId, items);
      nextParentId;
      nextParentId = tryGetAboveItemId(nextParentId, items)
    ) {
      const nextParentItem = items[nextParentId]
      const nextParentChildIds = nextParentItem.hierarchy.childIds as string[]
      if (nextParentChildIds.length > 0) {
        return nextParentChildIds[nextParentChildIds.length - 1]
      }
    }

    // No next item was found - we were already at the top.
    return undefined
  }
}

/**
 * Helper for @see moveDownCommand: find the next item below the specified item, of the same rank
 * (i.e. at the same depth in the item tree).
 * For example, if the specified item is a row, this finds the row below it, possibly belonging to a different parent.
 * @param itemId The ID of the item to find the one below.
 * @param items The items to search in.
 * @returns The ID of the item below, or `undefined` if this is the topmost item of its rank.
 */
function tryGetBelowItemId(itemId: string, items: Record<string, TimelineItem>): string | undefined {
  // Look up item.
  const item = items[itemId]

  // We're at the root of the hierarchy if the item has no parent.
  if (!item.hierarchy.parentId) {
    return undefined
  }

  // Get parent and find item's index in parent.
  const possibleParentId = item.hierarchy.parentId as string | undefined
  console.assert(!!possibleParentId, 'Must have parent.')
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- We just proved the parent exists.
  const parentId = possibleParentId!
  const parentItem = items[parentId]
  const parentChildIds = parentItem.hierarchy.childIds as string[]
  const itemIndexInParent = parentChildIds.indexOf(itemId)

  if (itemIndexInParent + 1 < parentChildIds.length) {
    // This isn't the bottommost (last) child of its parent,
    // so the result is found in the same parent.
    return parentChildIds[itemIndexInParent + 1]
  } else {
    // This is the bottommost (last) child of its parent.
    // The result is found by finding the first parent below the current parent
    // that has a non-empty child list and taking its topmost (first) child.
    for (
      let nextParentId = tryGetBelowItemId(parentId, items);
      nextParentId;
      nextParentId = tryGetBelowItemId(nextParentId, items)
    ) {
      const nextParentItem = items[nextParentId]

      const nextParentChildIds = nextParentItem.hierarchy.childIds as string[]
      if (nextParentChildIds.length > 0) {
        return nextParentChildIds[0]
      }
    }

    // No next item was found - we were already at the bottom.
    return undefined
  }
}
</script>

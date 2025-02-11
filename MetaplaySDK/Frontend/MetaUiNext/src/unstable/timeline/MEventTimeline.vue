<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Event Timeline"
  noBodyPadding
  :variant="timelineData && Object.keys(timelineData.items).length === 0 ? 'neutral' : 'primary'"
  :error="loadingError"
  data-testid="timeline"
  )
  template(#header-right)
    //- Utilities menu?

  //- Loading state.
  div(
    v-if="!timelineData"
    class="tw-min-h-64 tw-content-center tw-border-t tw-border-neutral-300 tw-text-center tw-@container"
    data-testid="loading-message"
    )
    //- Note: ideally we would use a skeleton loader but this was faster to make now.
    div(class="tw-animate-bounce tw-italic tw-text-neutral-500") Loading...

  //- Empty state.
  div(
    v-else-if="Object.keys(timelineData.items).length === 0"
    class="tw-min-h-64 tw-content-center tw-border-t tw-border-neutral-300 tw-text-center tw-@container"
    data-testid="empty-message"
    )
    div(class="tw-p-4 tw-text-center tw-text-neutral-500") No events to display.

  //- Layout container.
  div(
    v-else
    class="tw-flex tw-min-h-64 tw-border-t tw-border-neutral-300 tw-@container"
    data-testid="timeline-container"
    )
    //- Left side: Groups & names -------------------------------------------------------------------------------------
    MEventTimelineNavigator(
      :visibleTimelineData="visibleTimelineData"
      :expandedGroups="expandedGroups"
      :timelineFirstVisibleInstant="timelineFirstVisibleInstant"
      :selectedItemIds="selectedItemIds"
      @todayButtonClicked="timelineContentRef?.scrollTimelineToToday()"
      @groupClicked="toggleGroupExpansion"
      @invokeCommand="(command) => invokeCommand(command)"
      @itemInspected="(eventId) => (selectedItemIds = [eventId])"
      @close="clearCurrentlySelectedItem"
      )

    //- Center: Timeline ----------------------------------------------------------------------------------------------

    MEventTimelineContent(
      ref="timelineContentRef"
      :visibleTimelineData="visibleTimelineData"
      :timelineStartInstant="timelineStartInstant"
      :timelineEndInstant="timelineEndInstant"
      :timelineLengthInDays="timelineLengthInDays"
      :timelineFirstVisibleInstant="timelineFirstVisibleInstant"
      :numberOfTimelineBufferDays="numberOfTimelineBufferDays"
      :timelineDayWidthInRem="timelineDayWidthInRem"
      :expandedGroups="expandedGroups"
      :selectedItemIds="selectedItemIds"
      @update:timelineFirstVisibleInstant="(event) => (timelineFirstVisibleInstant = event)"
      @update:timelineLastVisibleInstant="(event) => (timelineLastVisibleInstant = event)"
      @update:timelineDayWidthInRem="(event) => (timelineDayWidthInRem = event)"
      @eventClicked="(eventId) => (selectedItemIds = [eventId])"
      )

    //- Right side: Event inspector -----------------------------------------------------------------------------------

    MEventTimelineInspector(
      :selectedItemAndDetails="selectedItemAndDetails"
      :visibleTimelineData="visibleTimelineData"
      @close="clearCurrentlySelectedItem"
      @invokeCommand="(command) => invokeCommand(command)"
      )

  //- Debug info.
  pre(
    v-if="props.debug"
    class="tw-space-y-2 tw-overflow-x-auto tw-bg-neutral-100 tw-p-2 tw-text-xs"
    )
    div(class="tw-font-bold") MEventTimeline Debug Info
    div
      div(class="tw-font-bold") TIMELINE
      div Day width: {{ timelineDayWidthInRem }}rem
      div First visible instant: {{ timelineFirstVisibleInstant }}
      div Last visible instant: {{ timelineLastVisibleInstant }}
    div
      div(class="tw-font-bold") TIMELINE DISPLAY OPTIONS
      div Timeline start: {{ timelineStartInstant }} ({{ numberOfTimelineBufferDays }} not fetched)
      div Total days: {{ timelineLengthInDays }}
      div Timeline end: {{ timelineEndInstant }} ({{ numberOfTimelineBufferDays }} not fetched)
    div
      div(class="tw-font-bold") VISIBLE RANGE (for data fetching)
      div Data range to fetch start: {{ visibleRange.startInstant }}
      div Total days: {{ visibleRange.endInstant.diff(visibleRange.startInstant, 'days').days }} days
      div Data range to fetch end: {{ visibleRange.endInstant }}
    div(v-if="timelineData")
      div(class="tw-font-bold") FETCHED DATA
      div Data start: {{ timelineData.startInstantIsoString }}
      div Total days: {{ DateTime.fromISO(timelineData.endInstantIsoString).diff(DateTime.fromISO(timelineData.startInstantIsoString), 'days').days }} days
      div Data end: {{ timelineData.endInstantIsoString }}
    div(v-else)
      div(class="tw-font-bold") FETCHED DATA
      div Empty
    div
      div(class="tw-font-bold") SELECTED ITEM
      div Selected items: {{ selectedItemIds.join(', ') || 'none' }}
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, onMounted, ref, watch } from 'vue'

import MCard from '../../primitives/MCard.vue'
import type { DisplayError } from '../../utils/DisplayErrorHandler'
import MEventTimelineContent from './MEventTimelineContent.vue'
import MEventTimelineInspector from './MEventTimelineInspector.vue'
import MEventTimelineNavigator from './MEventTimelineNavigator.vue'
import type { TimelineData, TimelineItem, TimelineItemDetails } from './MEventTimelineTypes'
import type { ToServerCommand } from './timelineCommands'

// Props & Emits ------------------------------------------------------------------------------------------------------

const props = defineProps<{
  /**
   * The data to display. Undefined means no data has been fetched yet.
   */
  timelineData: TimelineData | undefined
  /**
   * Optional: An error to display when loading the timeline data fails.
   */
  loadingError?: DisplayError
  /**
   * Optional: Whether all timeline groups should start expanded. Defaults to false.
   */
  startExpanded?: boolean
  /**
   * Optional: Where to focus the left edge of the visible timeline on initialization. Defaults to 2 days before UTC now.
   */
  initialVisibleTimelineStartInstant?: DateTime
  /**
   * Optional: Some prop that sets the resolution of the timeline. Needs thinking.
   */
  initialZoomLevel?: 'hours' | 'days' | 'weeks' | 'months' | 'years'
  /**
   * Optional: The ID of an event that should be preselected on initialization.
   */
  preselectedItemId?: string

  selectedItemDetails?: Record<string, TimelineItemDetails | undefined>
  /**
   * Optional: Whether to show debug info. Defaults to false.
   */
  debug?: boolean
}>()

const emits = defineEmits<{
  // TODO: Figure out what inputs we have and how they should be emitted. All in one go? Separately? At least the data range and filters and various edit actions.
  'update:visibleRange': [value: { startInstant: DateTime; endInstant: DateTime; bufferInDays: number }]
  'update:selectedItemIds': [value: string[]]
  invokeCommand: [command: ToServerCommand]
}>()

// Timeline UI state --------------------------------------------------------------------------------------------------

const timelineContentRef = ref<typeof MEventTimelineContent>()

const timelineFirstVisibleInstant = ref<DateTime>(
  props.initialVisibleTimelineStartInstant ?? DateTime.utc().minus({ days: 2 })
)
const timelineLastVisibleInstant = ref<DateTime>()
const timelineDayWidthInRem = ref(3)

watch(
  () => props.initialZoomLevel,
  (newZoomLevel) => {
    if (newZoomLevel === 'hours') {
      timelineDayWidthInRem.value = 30
    } else if (newZoomLevel === 'days') {
      timelineDayWidthInRem.value = 12
    } else if (newZoomLevel === 'weeks') {
      timelineDayWidthInRem.value = 3
    } else if (newZoomLevel === 'months') {
      timelineDayWidthInRem.value = 1
    } else if (newZoomLevel === 'years') {
      timelineDayWidthInRem.value = 0.1
    }
  },
  { immediate: true }
)

const timelineDataStartInstant = computed(() =>
  props.timelineData ? DateTime.fromISO(props.timelineData.startInstantIsoString, { zone: 'utc' }) : DateTime.utc()
)
const timelineDataEndInstant = computed(() =>
  props.timelineData ? DateTime.fromISO(props.timelineData.endInstantIsoString, { zone: 'utc' }) : DateTime.utc()
)

const numberOfTimelineBufferDays = ref(7)
const timelineStartInstant = computed(() =>
  timelineDataStartInstant.value.minus({
    days: numberOfTimelineBufferDays.value,
  })
)
const timelineEndInstant = computed(() => timelineDataEndInstant.value.plus({ days: numberOfTimelineBufferDays.value }))

const timelineLengthInDays = computed(() =>
  Math.ceil(timelineEndInstant.value.diff(timelineStartInstant.value, 'days').days)
)

// User input -> emits ------------------------------------------------------------------------------------------------

/**
 * The range of data that is visible.
 */
const visibleRange = computed(() => {
  return {
    startInstant: timelineFirstVisibleInstant.value.startOf('day'),
    endInstant: (timelineLastVisibleInstant.value ?? timelineFirstVisibleInstant.value.plus({ days: 7 })).endOf('day'),

    // TODO: Make this dynamic based on zoom level.
    bufferInDays: 7,
  }
})

watch(
  visibleRange,
  (newRange) => {
    emits('update:visibleRange', newRange)
  },
  { immediate: true }
)

// Data ---------------------------------------------------------------------------------------------------------------

const visibleTimelineData = ref<TimelineData>()

// Row grouping -------------------------------------------------------------------------------------------------------

const expandedGroups = ref<string[]>([])

// Open all groups if `startExpanded` is set and this is the first data fetch.
watch(
  () => props.timelineData,
  (newData, oldData) => {
    if (props.startExpanded && newData && !oldData) {
      openAllEventGroups()
    }
  },
  { immediate: true }
)

/**
 * Utility function that checks if a group is currently expanded.
 */
function isGroupExpanded(groupId: string): boolean {
  return expandedGroups.value.includes(groupId)
}

/**
 * Toggle a group's expansion state to open/closed.
 */
function toggleGroupExpansion(groupId: string): void {
  if (isGroupExpanded(groupId)) {
    expandedGroups.value = expandedGroups.value.filter((id) => id !== groupId)
  } else {
    expandedGroups.value = [...expandedGroups.value, groupId]
  }
}

/**
 * Utility function that adds all event groups to the expanded list.
 */
function openAllEventGroups(): void {
  expandedGroups.value = Object.entries(props.timelineData?.items ?? {})
    .filter(([_, item]) => item?.itemType === 'group')
    .map(([id, _]) => id)
}

/**
 * Utility function that removes all event groups from the expanded list.
 */
function closeAllEventGroups(): void {
  expandedGroups.value = []
}

// Data filtering -----------------------------------------------------------------------------------------------------

// Update visible data when the timeline data or visible range changes.
watch(
  [(): TimelineData | undefined => props.timelineData, timelineLastVisibleInstant],
  ([newTimelineData, newLastVisibleInstant]) => {
    if (!newLastVisibleInstant || !newTimelineData) return

    // TODO: Adding `numberOfTimelineBufferDays` prevents events that weird "one frame catch up" behavior
    // when scrolling. It also means more renderinfos are generated, and makes events extend into the
    // "unloaded" area. Not 100% sure this is the right approach.
    // visibleTimelineData.value = filterTimelineDataForVisibleRange(
    //   newTimelineData,
    //   timelineFirstVisibleInstant.value.minus({ days: numberOfTimelineBufferDays.value }),
    //   newLastVisibleInstant.plus({ days: numberOfTimelineBufferDays.value })
    // )

    visibleTimelineData.value = newTimelineData
  }
)

// Inspector ----------------------------------------------------------------------------------------------------------

const selectedItemIds = ref<string[]>([])

// Emit selected event IDs as they change.
watch(
  selectedItemIds,
  (newSelectedItemIds) => {
    emits('update:selectedItemIds', newSelectedItemIds)
  },
  { immediate: true }
)

// Pre-select an event if one is provided after the first data load.
watch(
  () => props.timelineData,
  (newData, oldData) => {
    if (props.preselectedItemId && newData && !oldData) {
      if (newData.items[props.preselectedItemId]) {
        selectedItemIds.value = [props.preselectedItemId]
      }
    }
  },
  { immediate: true }
)

function clearCurrentlySelectedItem(): void {
  // Clear the selected event.
  selectedItemIds.value = []
}

const selectedItemAndDetails = computed(
  (): Record<string, { data: TimelineItem; details: TimelineItemDetails | undefined }> => {
    const eventsIds = Object.keys(props.selectedItemDetails ?? {})
    const itemAndDetails: Record<string, { data: TimelineItem; details: TimelineItemDetails | undefined }> = {}
    eventsIds.forEach((eventId) => {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      const data = props.timelineData!.items[eventId]
      const details = props.selectedItemDetails?.[eventId]
      itemAndDetails[eventId] = {
        data,
        details,
      }
    })
    return itemAndDetails
  }
)

function invokeCommand(command: ToServerCommand): void {
  emits('invokeCommand', command)
}

// Hotkey listeners ---------------------------------------------------------------------------------------------------

// Register a global event listener for keys.
onMounted(() => {
  document.addEventListener('keydown', (event) => {
    if (!visibleTimelineData.value) {
      return
    }

    // CTRL + O to open all groups.
    if (event.key === 'o' && event.ctrlKey) {
      event.preventDefault()
      openAllEventGroups()
    }

    // CTRL + SHIFT + O to close all groups.
    if ((event.key === 'O' || event.key === 'o') && event.shiftKey && event.ctrlKey) {
      event.preventDefault()
      closeAllEventGroups()
    }

    // CTRL + SPACE to toggle group expansion.
    // todo - restore this
    // if (event.key === ' ' && event.ctrlKey && selectedItems.value.length > 0) {
    //   for (const selectedItem of selectedItems.value) {
    //     const groupOfSelectedItem = (props.timelineData?.sections ?? [])
    //       .flatMap((section) => section.groups)
    //       .find((group) => group.rows.flatMap((row) => row.items).includes(selectedItem))
    //     if (groupOfSelectedItem) {
    //       toggleGroupExpansion(groupOfSelectedItem.id)
    //     }
    //   }
    // }
  })
})
</script>

<style>
/*
 * Animating the active event overlay.
 */
@keyframes active-event-overlay {
  from {
    transform: translate(0);
  }

  to {
    transform: translate(-15px);
  }
}

.active-event-overlay {
  width: calc(100% + 15px);
  height: 100%;
  animation: active-event-overlay 1s linear infinite;
  background-image: repeating-linear-gradient(
    45deg,
    transparent,
    transparent 5px,
    rgba(255, 255, 255, 0.2) 5px,
    rgba(255, 255, 255, 0.2) 10px
  );
}
</style>

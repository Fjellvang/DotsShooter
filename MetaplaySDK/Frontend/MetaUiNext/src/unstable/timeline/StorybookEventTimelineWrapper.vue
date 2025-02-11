<template lang="pug">
MEventTimeline(
  :timelineData="fetchedTimelineData"
  :initialVisibleTimelineStartInstant="initialVisibleTimelineStartInstant"
  :initialZoomLevel="initialZoomLevel"
  :preselectedItemId="preselectedItemId"
  :selectedItemIds="selectedItemIds"
  :selectedItemDetails="fetchedTimelineItemDetails"
  :startExpanded="startExpanded"
  :debug="debug"
  @update:visibleRange="onVisibleRangeUpdated"
  @update:selectedItemIds="onSelectedItemIdsUpdated"
  )

//- Debug info.
pre(
  v-if="debug"
  class="tw-space-y-2 tw-overflow-x-auto tw-bg-neutral-100 tw-p-2 tw-text-xs"
  )
  div(class="tw-font-bold") StorybookEventTimelineWrapper Debug Info
  div
    div(class="tw-font-bold") DATA FETCH COUNT
    pre Data fetches completed: {{ timelineDataFetchCount }}
    pre Item detail fetches completed: {{ timelineItemDetailsFetchCount }}
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { ref } from 'vue'

import MEventTimeline from './MEventTimeline.vue'
import type { TimelineData, TimelineItemDetails } from './MEventTimelineTypes'
import { TimelineDataFetcher, TimelineDataFetchHandler } from './timelineDataFetcher'

/**
 * Props we need to repro interesting scenarios in Storybook.
 */
interface Props {
  /**
   * The full dataset to be displayed in the timeline.
   */
  timelineDataFetchHandler: () => TimelineDataFetchHandler
  /**
   * Where to position the left edge of the timeline when it first renders.
   */
  preselectedItemId?: string
  initialVisibleTimelineStartInstant?: DateTime
  initialZoomLevel?: 'hours' | 'days' | 'weeks' | 'months' | 'years'
  /**
   * Optional: Whether to start with all event groups expanded. Defaults to false.
   */
  startExpanded?: boolean
  debug?: boolean
}

const props = defineProps<Props>()

const selectedItemIds = ref<string[]>([])

/**
 *
 */
const fetchedTimelineItemDetails = ref<Record<string, TimelineItemDetails | undefined>>({})

/**
 * Data to be displayed in the timeline.
 */
const fetchedTimelineData = ref<TimelineData>()

/**
 * Debugging count of how many times we've fetched data.
 * This should be here, not in `MEventTimeline`, because that doesn't know when the data fetching actually happens.
 */
const timelineDataFetchCount = ref(0)
const timelineItemDetailsFetchCount = ref(0)

/**
 * Fake data backend.
 */
function onVisibleRangeUpdated(newVisibleRange: {
  startInstant: DateTime
  endInstant: DateTime
  bufferInDays: number
}): void {
  visibleRange.value = newVisibleRange

  timelineDataFetcher.requestTimelineData(
    newVisibleRange.startInstant,
    newVisibleRange.endInstant,
    newVisibleRange.bufferInDays
  )
}

function onSelectedItemIdsUpdated(newSelectedItemIds: string[]): void {
  selectedItemIds.value = newSelectedItemIds

  timelineDataFetcher.requestItemDetails(newSelectedItemIds)
}

/**
 * The range of data that is being visualized by the timeline. This gets set by the timeline component's emit after it has been mounted and we know the visible range.
 */
const visibleRange = ref<{ startInstant: DateTime; endInstant: DateTime }>()

/**
 * Make the data fetcher.
 */
const timelineDataFetcher = new TimelineDataFetcher(
  props.timelineDataFetchHandler(),
  (newTimelineData) => {
    // Store the data.
    fetchedTimelineData.value = newTimelineData
    timelineDataFetchCount.value++
  },
  (newTimelineItemDetails) => {
    // Store the data.
    // todo: this implies that update always return the entire set - it does not
    // need to somehow add/remove.. send entire known set?
    fetchedTimelineItemDetails.value = Object.fromEntries(
      Object.entries(newTimelineItemDetails).map(([id, details]): [string, TimelineItemDetails | undefined] => [
        id,
        details,
      ])
    )
    // timelineItemDetailsFetchCount.value += Object.keys(newTimelineItemDetails).length
  },
  {
    largestAllowedTotalWindowInDays: 100,
    debounceTimeInMs: 1_000,
  }
)
</script>

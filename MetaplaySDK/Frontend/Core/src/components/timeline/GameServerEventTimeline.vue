<template lang="pug">
MEventTimeline(
  :timelineData="fetchedTimelineData"
  :initialVisibleTimelineStartInstant="initialVisibleTimelineStartInstant"
  :preselectedItemId="preselectedItemId"
  :selectedItemIds="selectedItemIds"
  :selectedItemDetails="fetchedTimelineItemDetails"
  :startExpanded="startExpanded"
  @update:visibleRange="onVisibleRangeUpdated"
  @update:selectedItemIds="onSelectedItemIdsUpdated"
  @invokeCommand="onInvokeCommand"
  )
</template>

<script setup lang="ts">
import type { CancelTokenSource } from 'axios'
import axios from 'axios'
import { DateTime } from 'luxon'
import type { TimelineItem } from 'node_modules/@metaplay/meta-ui-next/dist/unstable/timeline/MEventTimelineTypes'
import { onUnmounted, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MEventTimeline,
  TimelineDataFetcher,
  TimelineDataFetchHandler,
  type TimelineData,
  type TimelineItemDetails,
} from '@metaplay/meta-ui-next'

defineProps<{
  /**
   * Optional: The ID of an event that should be selected after the timeline data first loads.
   */
  preselectedItemId?: string
  /**
   * Optional: Where to position the left edge of the timeline when it first renders.
   */
  initialVisibleTimelineStartInstant?: DateTime
  /**
   * Optional: Whether to start with all event groups expanded. Defaults to false.
   */
  startExpanded?: boolean
}>()

const selectedItemIds = ref<string[]>([])

/**
 * Data to be displayed in the timeline.
 */
const fetchedTimelineData = ref<TimelineData>()

const fetchedTimelineItemDetails = ref<Record<string, TimelineItemDetails | undefined>>({})

// Data fetching ------------------------------------------------------------------------------------------------------

const gameServerApi = useGameServerApi()

/**
 * A timeline data fetcher that talks to the server. Includes polling for updates after a successful fetch.
 */
class TimelineDataFetchHandlerFromServer extends TimelineDataFetchHandler {
  /**
   * Implementation of base class `requestTimelineData`.
   * @param firstInstant The first instant in the window to fetch.
   * @param lastInstant The last instant in the window to fetch.
   */
  requestTimelineData(firstInstant: DateTime, lastInstant: DateTime): void {
    // Stop any pending fetches or polls.
    if (this.initialFetchCancelTokenSource) {
      this.initialFetchCancelTokenSource.cancel()
    }
    this.cancelPoll()

    // Remember the request.
    this.firstInstant = firstInstant
    this.lastInstant = lastInstant

    // Cancel token allows us to cancel this request if necessary.
    this.initialFetchCancelTokenSource = axios.CancelToken.source()

    // Make the request and don't wait for the response.
    void gameServerApi
      .request<TimelineDataResponse>({
        method: 'GET',
        url: 'liveOpsTimelineData',
        params: {
          firstInstant: firstInstant.toISO() ?? '??',
          lastInstant: lastInstant.toISO() ?? '??',
        },
        cancelToken: this.initialFetchCancelTokenSource.token,
      })
      .then((response) => {
        // Cancel token is no longer needed.
        this.initialFetchCancelTokenSource = undefined

        // Store the data.
        this.setTimelineData(response.data.timelineData)

        // Start polling for updates...
        this.continuationCursor = response.data.updateCursor
        this.pollForUpdates()
      })
      .catch((e) => {
        // Error handling. If the request was cancelled, ignore the error.
        if (axios.isCancel(e)) {
          // Ignore.
        } else if (e.response) {
          throw e as Error
        }
      })
  }

  /**
   * Implementation of base class `requestItemDetails`.
   * @param itemIds The IDs of the items to fetch details for.
   */
  requestItemDetails(itemIds: string[]): void {
    const body: { itemIds: string[] } = { itemIds }
    void gameServerApi
      .post<{ items: Record<string, TimelineItemDetails> }>('liveOpsTimelineItemDetails', body)
      .then((response) => {
        this.updateItemDetails(response.data.items)
      })
  }

  /**
   * Trigger a full refresh now.
   */
  triggerFullRefreshNow(): void {
    if (this.firstInstant && this.lastInstant) {
      this.requestTimelineData(this.firstInstant, this.lastInstant)
    }
  }

  /**
   * Trigger a poll for updates now, rather than waiting for the next update.
   */
  triggerUpdatePollNow(): void {
    this.pollForUpdates()
  }

  /**
   * Polls the server for an update now. On success, schedules the next poll. On failure, triggers a full refresh.
   */
  private pollForUpdates(): void {
    // Cancel any existing poll.
    this.cancelPoll()

    if (stopPolling.value) return

    // Cancel token allows us to cancel this request if necessary.
    this.updateCancelTokenSource = axios.CancelToken.source()

    // Make the request and don't wait for the response.
    void gameServerApi
      .post<TimelineDataUpdatesResponse>(
        // Post request details.
        'getLiveOpsTimelineDataUpdates',
        { cursor: this.continuationCursor },
        { cancelToken: this.updateCancelTokenSource.token }
      )
      .then((response) => {
        // Cancel token is no longer needed.
        this.updateCancelTokenSource = undefined

        // Look at the response.
        if (response.data.isSuccess) {
          // If there are any item updates them apply them.
          if (Object.keys(response.data.itemUpdates as object).length) {
            this.updateItemData(response.data.itemUpdates)
          }

          // Remember the (possibly new) continuation cursor.
          this.continuationCursor = response.data.continuationCursor

          // Set up a new poll in the future.
          this.updatePollId = setTimeout(() => {
            this.pollForUpdates()
          }, 2_000)
        } else {
          // Update fetch failed. We'll need to fully re-fetch the whole data.
          this.triggerFullRefreshNow()
        }
      })
      .catch((e) => {
        // Error handling. If the request was cancelled, ignore the error.
        if (axios.isCancel(e)) {
          // Ignore.
        } else if (e.response) {
          throw e as Error
        }
      })
  }

  /**
   * Cancel the current poll, if any.
   */
  cancelPoll(): void {
    if (this.updatePollId) {
      clearTimeout(this.updatePollId)
      this.updatePollId = undefined
    }

    if (this.updateCancelTokenSource) {
      this.updateCancelTokenSource.cancel()
      this.updateCancelTokenSource = undefined
    }
  }

  private firstInstant?: DateTime
  private lastInstant?: DateTime
  private continuationCursor?: unknown
  private initialFetchCancelTokenSource?: CancelTokenSource
  private updateCancelTokenSource?: CancelTokenSource
  private updatePollId?: ReturnType<typeof setTimeout>
}

interface TimelineDataResponse {
  timelineData: TimelineData
  updateCursor: unknown
}

interface TimelineDataUpdatesResponse {
  isSuccess: boolean
  error: string
  itemUpdates: Record<string, TimelineItem | undefined>
  continuationCursor: unknown
}

/**
 * Make the data fetcher.
 */
const timelineDataFetchHandlerFromServer = new TimelineDataFetchHandlerFromServer()

const timelineDataFetcher = new TimelineDataFetcher(
  timelineDataFetchHandlerFromServer,
  (newTimelineData) => {
    // Store the data.
    fetchedTimelineData.value = newTimelineData
  },
  (newTimelineItemDetails) => {
    // Store the data.
    // todo: this implies that update always return the entire set - it does not
    // need to somehow add/remove.. send entire known set?
    fetchedTimelineItemDetails.value = newTimelineItemDetails
  },
  {
    largestAllowedTotalWindowInDays: 100,
    debounceTimeInMs: 1_000,
  }
)

/**
 * Cancel any update polling when the component is unmounted.
 */
const stopPolling = ref(false)
onUnmounted(() => {
  timelineDataFetchHandlerFromServer.cancelPoll()
  stopPolling.value = true
})

// Event handlers -----------------------------------------------------------------------------------------------------

function onVisibleRangeUpdated(newVisibleRange: {
  startInstant: DateTime
  endInstant: DateTime
  bufferInDays: number
}): void {
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

async function onInvokeCommand(command: any): Promise<void> {
  const params = { command }
  await gameServerApi.post('invokeLiveOpsTimelineCommand', params)
  timelineDataFetchHandlerFromServer.triggerUpdatePollNow()
}
</script>

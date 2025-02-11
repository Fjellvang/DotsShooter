<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- Display event logs for a given entity. -->

<template lang="pug">
meta-event-stream-card(
  :title="`Latest ${entityKind} Events`"
  icon="clipboard-list"
  :eventStream="eventStream"
  :eventStreamLoading="backwardsEventsPollerActive"
  :utilitiesMode="utilitiesMode"
  :searchPreHighlight="searchPreHighlight"
  :keywordPreFilters="keywordPreFilters"
  :eventTypePreFilters="eventTypePreFilters"
  :maxHeight="maxHeight"
  :permission="requiredPermissionToGetEvents"
  :showTimeDeltas="folding !== 'day'"
  :showViewMoreLink="showViewMoreLink"
  allow-pausing
  data-testid="entity-event-log-card"
  )
</template>

<script lang="ts" setup>
import { keyBy } from 'lodash-es'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

import {
  EventStreamItemBase,
  EventStreamItemEvent,
  generateStats,
  MetaEventStreamCard,
  wrapDays,
  wrapRepeatingEvents,
  wrapSessions,
} from '@metaplay/event-stream'
import { ApiPoller, useGameServerApi } from '@metaplay/game-server-api'
import { usePermissions } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllAnalyticsEventsSubscriptionOptions } from '../../subscription_options/analyticsEvents'

const permissions = usePermissions()
const gameServerApi = useGameServerApi()

// Props --------------------------------------------------------------------------------------------------------------

const props = withDefaults(
  defineProps<{
    /**
     * Kind of entity that we are interested in.
     */
    entityKind: string
    /**
     * Id of the entity that we are interested in or a function that retrieves the needed Id.
     */
    entityId: string | (() => string)
    /**
     * Optional: A timestamp of the last full entity reset (caused by entity reset or overwrite). The purpose of this is
     * to invalidate the ongoing event log scan when an entity gets reset and the existing cached log segments are known
     * to no longer be valid.
     */
    entityResetTimestamp?: string | null
    /**
     * Optional: Setting this to 'highlight' will show events that match the search & filters with a different background color.
     * Setting this to 'filter' will hide events that do not match the search & filters.
     * Defaults to 'filter'.
     */
    utilitiesMode?: 'highlight' | 'filter'
    /**
     * Optional: Pre-fill the search with this string.
     */
    searchPreHighlight?: string
    /**
     * Optional: Prevent the user from changing the search string.
     */
    freezeSearch?: boolean
    /**
     * Optional: Pre-filtering based on keywords
     */
    keywordPreFilters?: string[]
    /**
     * Optional: Pre-filtering based on event types
     */
    eventTypePreFilters?: string[]
    /**
     * Optional: Limit height of the card.
     */
    maxHeight?: string
    /**
     * Optional: Interval in milliseconds to poll for new data. Defaults to 5000ms.
     */
    fetchPollingInterval?: number
    /**
     * Optional: Number of events to fetch at a time. Defaults to 500.
     */
    fetchPageSize?: number
    /**
     * Optional: Fold events into 'session's or 'day's.
     */
    folding?: string
    /**
     * Optional: Show a link to view the event timeline in a separate page.
     */
    showViewMoreLink?: boolean
  }>(),
  {
    entityResetTimestamp: null,
    searchPreHighlight: '',
    eventTypePreFilters: () => [],
    keywordPreFilters: () => [],
    maxHeight: '30rem',
    fetchPollingInterval: 5_000,
    fetchPageSize: 1_000,
    folding: 'session',
    showViewMoreLink: true,
    utilitiesMode: 'filter',
  }
)

// Misc ---------------------------------------------------------------------------------------------------------------

const emit = defineEmits(['stats'])

/**
 * Subscribe to data needed to render this component.
 */
const { data: analyticsEvents } = useSubscription(getAllAnalyticsEventsSubscriptionOptions())
const analyticsEventsByTypeName = computed(() => keyBy(analyticsEvents.value, (ev) => ev.type.split(',')[0]))

// UI & Event fetching ------------------------------------------------------------------------------------------------

// Forward poller for new events.
let forwardEventsPoller: ApiPoller | undefined

// Scan cursor for forward poller.
let forwardEventScanCursor: string

// Backward poller for old events.
let backwardEventsPoller: ApiPoller | undefined

// Scan cursor for backward poller.
let backwardEventScanCursor: string

// True if the backward poller is still active. Becomes false when we have fetched all past events.
const backwardsEventsPollerActive = ref(false)

/**
 * Data structure for the event log entries fetched from the API.
 */
type PlayerEventLogQueryResult = PlayerEventLogQuerySuccess | PlayerEventLogQueryDesyncFailure

interface PlayerEventLogQuerySuccess {
  failedWithDesync: false

  entries: any[]
  continuationCursor: string
  startCursor: string
}

interface PlayerEventLogQueryDesyncFailure {
  failedWithDesync: true

  desyncDescription: string
}

/**
 * Start fetching data when the page loads.
 */
onMounted(async () => {
  if (hasPermissionToGetEvents.value) {
    await startFetchingData()
  } else {
    backwardsEventsPollerActive.value = false
  }
})

/**
 * We need to manually cancel the pollers when the page unloads.
 */
onUnmounted(() => {
  forwardEventsPoller?.stop()
  backwardEventsPoller?.stop()
})

/**
 * Id of the entity that is to be displayed.
 * Note: Either the Id is passed in as a string or as a function that retrieves the entity Id.
 */
const entityId = computed(() => {
  let computedEntityId = ''
  if (typeof props.entityId === 'string') {
    computedEntityId = props.entityId
  } else {
    computedEntityId = props.entityId()
  }
  if (!computedEntityId) {
    throw new Error('Entity Id cannot be empty or undefined.')
  }
  return computedEntityId
})

/**
 * Re-initialize if the entity changes.
 */
watch([(): string => entityId.value, (): string | null => props.entityResetTimestamp], async () => {
  await startFetchingData()
})

/**
 * Calculates the endpoint that we will fetch the data from.
 */
const apiEndpoint = computed(() => {
  if (props.entityKind === 'Player') {
    return `/players/${entityId.value}/eventLog`
  } else if (props.entityKind === 'Guild') {
    return `/guilds/${entityId.value}/eventLog`
  } else {
    throw new Error('Invalid entityKind for EntityEventLogCard: ' + props.entityKind)
  }
})

/**
 * Raw events fetched from the API. This gets filled from two ends by the forwards and backwards pollers.
 */
const rawEvents = ref<any[]>([])

/**
 * Start fetching data from the API. Removes and resets any existing data. Note that we simultaneously scan backwards
 * and forwards. Fetching backwards means that we load the most relevant data first and then progressively fetch older
 * data over time. Fetching forwards means that we load the newest data first and keep updating the data as new events
 * occur.
 */
async function startFetchingData(): Promise<void> {
  // Reset data.
  rawEvents.value = []
  backwardsEventsPollerActive.value = true
  if (forwardEventsPoller) {
    forwardEventsPoller.stop()
    forwardEventsPoller = undefined
  }
  if (backwardEventsPoller) {
    backwardEventsPoller.stop()
    backwardEventsPoller = undefined
  }

  // Make an initial, small request to get the start cursor.
  const initialRequestData = (
    await gameServerApi.request<PlayerEventLogQueryResult>({
      method: 'GET',
      url: apiEndpoint.value,
      params: {
        startCursor: '$newest',
        numEntries: 1,
        scanDirection: 'towardsNewer',
      },
    })
  ).data
  if (initialRequestData.failedWithDesync) {
    throw new Error('Initial request from cursor $newest should never result in a desync.')
  }
  const startCursor = initialRequestData.startCursor
  forwardEventScanCursor = startCursor
  backwardEventScanCursor = startCursor

  // Start reading from newest and keep polling.
  forwardEventsPoller = new ApiPoller(
    () => props.fetchPollingInterval, // This is a function so that it is reactive to prop changes.
    'GET',
    apiEndpoint.value,
    () => {
      return {
        startCursor: forwardEventScanCursor,
        numEntries: props.fetchPageSize,
        scanDirection: 'towardsNewer',
      }
    },
    (data: PlayerEventLogQueryResult) => {
      if (!data.failedWithDesync) {
        if (data.entries.length > 0) {
          // Add events to list.
          rawEvents.value = rawEvents.value.concat(data.entries)
          validateEventSequence()
        }

        // Update the scan cursor.
        forwardEventScanCursor = data.continuationCursor
      } else {
        // NOTE: startFetchingData is async, so if e.g. the entityResetTimestamp watch fires,
        // there's no guarantee that it won't overlap with this one. We could probably tolerate
        // overlaps in startFetchingData.
        void startFetchingData()
      }
    }
  )

  // Start fetching backwards from newest to oldest. Stop when we reach the end of the stream.
  backwardEventsPoller = new ApiPoller(
    100, // Polling interval is 100ms so that we load the old events quickly.
    'GET',
    apiEndpoint.value,
    () => {
      return {
        startCursor: backwardEventScanCursor,
        numEntries: props.fetchPageSize,
        scanDirection: 'towardsOlder',
      }
    },
    (data: PlayerEventLogQueryResult) => {
      if (!data.failedWithDesync) {
        if (data.entries.length > 0) {
          // Add events to list.
          rawEvents.value = data.entries.concat(rawEvents.value)
          validateEventSequence()

          // Update the scan cursor.
          backwardEventScanCursor = data.continuationCursor
        }

        // First incomplete page fetched means that we have reached the end of the stream. We can stop
        // polling for more data as we have fetched all past events.
        if (data.entries.length < props.fetchPageSize) {
          backwardEventsPoller?.stop()
          backwardsEventsPollerActive.value = false
        }
      } else {
        // NOTE: startFetchingData is async, so if e.g. the entityResetTimestamp watch fires,
        // there's no guarantee that it won't overlap with this one. We could probably tolerate
        // overlaps in startFetchingData.
        void startFetchingData()
      }
    }
  )
}

/**
 * Event stream data, generated from the fetched data.
 */
const eventStream = computed(() => {
  // Create an event stream.
  if (rawEvents.value.length > 0) {
    let eventStream: EventStreamItemBase[] = rawEvents.value.map((event) => {
      return new EventStreamItemEvent(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        event.collectedAt,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        analyticsEventsByTypeName.value[event.payload.$type].displayName || event.payload.$type,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        event.payload.eventDescription,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        event.uniqueId,
        event,
        '',
        'timeline',
        `/entityEventLog/${props.entityKind}/${entityId.value}?search=${event.uniqueId}`
      )
    })

    // Fold what we can.
    if (props.folding === 'session') {
      eventStream = wrapSessions(eventStream)
    } else if (props.folding === 'day') {
      eventStream = wrapDays(eventStream)
    }
    eventStream = wrapRepeatingEvents(eventStream)

    // Blast out some stats data.
    emit('stats', generateStats(eventStream))

    return eventStream
  } else {
    return null
  }
})

/**
 * Check that the sequence of events in `rawEvents` is correct. This will detect gaps in the event log caused by either
 * server errors or data fetching errors. This can also happen in rare cases when a segment gets deleted before we have
 * managed to read it.
 */
function validateEventSequence(): void {
  if (rawEvents.value.length > 0) {
    for (let i = 1; i < rawEvents.value.length; i++) {
      if (rawEvents.value[i].sequentialId !== rawEvents.value[i - 1].sequentialId + 1) {
        console.warn(
          `Gap in event log: entry ${rawEvents.value[i].sequentialId} follows ${rawEvents.value[i - 1].sequentialId}`
        )
      }
    }
  }
}

// Permissions --------------------------------------------------------------------------------------------------------

const hasPermissionToGetEvents = computed(() => {
  return permissions.doesHavePermission(requiredPermissionToGetEvents.value)
})

const requiredPermissionToGetEvents = computed((): string => {
  if (props.entityKind === 'Player') return 'api.players.view'
  else if (props.entityKind === 'Guild') return 'api.guilds.view'
  else {
    throw new Error('Invalid entityKind for EntityEventLogCard: ' + props.entityKind)
  }
})
</script>

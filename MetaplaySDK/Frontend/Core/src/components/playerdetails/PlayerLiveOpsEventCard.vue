<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="LiveOps Events"
  icon="calendar-alt"
  :itemList="playerEvents"
  :searchFields="['displayName', 'description']"
  :filterSets="eventFilterSets"
  :sortOptions="sortOptions"
  :emptyMessage="`${playerData.model.playerName || 'This player'} is not eligible for any events.`"
  :description="playerLiveOpsEventDescription"
  permission="api.liveops_events.view"
  data-testid="player-liveops-events-card"
  )
  template(#item-card="{ item: event }")
    MListItem {{ event.displayName }}
      template(#top-right)
        MBadge(:variant="liveOpsEventPhaseInfos[event.currentPhase].badgeVariant") {{ liveOpsEventPhaseInfos[event.currentPhase].displayString }}

      template(#bottom-left) {{ event.description }}

      template(#bottom-right)
        div(
          v-if="event.nextPhase && event.nextPhaseTime && DateTime.fromISO(event.nextPhaseTime).diffNow().milliseconds > 0"
          )
          | {{ liveOpsEventPhaseInfos[event.nextPhase].displayString }} in #[meta-duration(:duration="DateTime.fromISO(event.nextPhaseTime).diffNow()" :showAs="getDurationFormat(event.nextPhaseTime)" hideMilliseconds)].
        div(
          v-else-if="event.nextPhase && event.nextPhaseTime && DateTime.fromISO(event.nextPhaseTime).diffNow().milliseconds <= 0"
          )
          | {{ liveOpsEventPhaseInfos[event.nextPhase].displayString }} has started #[meta-duration(:duration="DateTime.now().diff(DateTime.fromISO(event.nextPhaseTime))" :showAs="getDurationFormat(event.nextPhaseTime)" hideMilliseconds)] ago.
        MTextButton(:to="`/liveOpsEvents/${event.eventId}`") View event
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListCard, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type { LiveOpsEventPerPlayerInfo } from '../../liveOpsEventServerTypes'
import { liveOpsEventPhaseInfos, makeListViewFilterSets, getDurationFormat } from '../../liveOpsEventUtils'
import {
  getLiveOpsEventsForPlayerSubscriptionOptions,
  getLiveOpsEventTypesSubscriptionOptions,
} from '../../subscription_options/liveOpsEvents'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to get events for.
   */
  playerId: string
}>()

const { data: playerLiveOpsEventsData } = useSubscription(getLiveOpsEventsForPlayerSubscriptionOptions(props.playerId))

const { data: liveOpsEventTypesData } = useSubscription(getLiveOpsEventTypesSubscriptionOptions())

const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * List of events that the player is eligible for.
 */
const playerEvents = computed(() => {
  return playerLiveOpsEventsData.value?.events ?? []
})

/**
 * All available event types.
 */
const eventTypeNames = computed(() => {
  return liveOpsEventTypesData.value?.map((type) => type.eventTypeName) ?? []
})

/**
 * Description of when player was last refreshed and when there are eligible events to participate in.
 */
const playerLiveOpsEventDescription = computed(() => {
  if (playerEvents.value.length > 0) {
    return ` 
    ${playerData.value?.model.playerName || 'This player'} is participating in or eligible for the LiveOps Events below.
    The current and next phases displayed were last refreshed ${DateTime.fromISO(playerLiveOpsEventsData.value?.lastRefreshedAt ?? '').toRelative()} and may be out of date.`
  } else {
    return ''
  }
})

/**
 * Filters.
 */
const eventFilterSets = computed(() => {
  return makeListViewFilterSets(
    eventTypeNames.value,
    ['NotYetStarted', 'InPreview', 'Active', 'EndingSoon', 'InReview'],
    false
  )
})

/**
 * Sort function to sort events by current phase.
 * @param event Event to return sort index for.
 */
function currentPhaseEventSortFunction(event: LiveOpsEventPerPlayerInfo): number {
  // List of all phases in sort order. Later items have higher priority and will be sorted to the top of the list.
  const phaseSortOrder = ['Ended', 'NotYetStarted', 'InReview', 'InPreview', 'EndingSoon', 'Active']
  return phaseSortOrder.indexOf(event.currentPhase)
}

/**
 * Sorting options.
 */
const sortOptions = [
  new MetaListSortOption('Current phase', currentPhaseEventSortFunction, MetaListSortDirection.Descending),
  new MetaListSortOption('Current phase', currentPhaseEventSortFunction, MetaListSortDirection.Ascending),
]
</script>

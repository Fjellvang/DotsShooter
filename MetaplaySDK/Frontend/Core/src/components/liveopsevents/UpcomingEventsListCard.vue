<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Upcoming Events"
  icon="calendar-days"
  :itemList="liveOpsEventsData?.upcomingEvents"
  :searchFields="['displayName', 'description']"
  :filterSets="upcomingLiveOpsEventFilterSets"
  :sortOptions="sortOptions"
  emptyMessage="No upcoming events right now, please create new ones."
  data-testid="upcoming-live-ops-events-list-card"
  )
  template(#item-card="{ item: event }")
    MListItem
      | {{ event.displayName }}

      template(#top-right)
        MBadge(
          v-if="event.schedule?.isPlayerLocalTime === true"
          variant="primary"
          ) Player Local
        MBadge(
          v-else
          :variant="liveOpsEventPhaseInfos[event.currentPhase].badgeVariant"
          ) {{ liveOpsEventPhaseInfos[event.currentPhase].displayString }}

      template(#bottom-left)
        div(v-if="event.description") {{ event.description }}
        div(v-if="event.schedule !== null && !event.schedule?.isPlayerLocalTime && event.startTime && event.endTime")
          span Starting #[meta-time(:date="DateTime.fromISO(event.startTime)")]
          span &nbsp;and will run for #[meta-duration(:duration="DateTime.fromISO(event.endTime).diff(DateTime.fromISO(event.startTime))" showAs="exactDuration" hideMilliseconds)].

      template(#bottom-right)
        div(v-if="event.nextPhase && event.nextPhaseTime") {{ liveOpsEventPhaseInfos[event.nextPhase].displayString }} in #[meta-duration(:duration="DateTime.fromISO(event.nextPhaseTime).diffNow()" :showAs="getDurationFormat(event.nextPhaseTime)" hideMilliseconds)].
        MTextButton(
          :to="`/liveOpsEvents/${event.eventId}`"
          data-testid="view-live-ops-event"
          ) View event
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption, MetaListCard } from '@metaplay/meta-ui'
import { MListItem, MBadge, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { liveOpsEventPhaseInfos, makeListViewFilterSets, getDurationFormat } from '../../liveOpsEventUtils'
import {
  getAllLiveOpsEventsSubscriptionOptions,
  getLiveOpsEventTypesSubscriptionOptions,
} from '../../subscription_options/liveOpsEvents'

const { data: liveOpsEventsData } = useSubscription(getAllLiveOpsEventsSubscriptionOptions())

const { data: liveOpsEventTypesData } = useSubscription(getLiveOpsEventTypesSubscriptionOptions())

const eventTypeNames = computed(() => {
  return liveOpsEventTypesData.value?.map((type) => type.eventTypeName) ?? []
})

/**
 * Filtering options passed to the MetaListCard component.
 */
const upcomingLiveOpsEventFilterSets = computed(() => {
  if (liveOpsEventsData.value) {
    return makeListViewFilterSets(eventTypeNames.value, ['NotYetStarted', 'InPreview'], false)
  } else {
    return []
  }
})

const sortOptions = [
  new MetaListSortOption('Start time', 'schedule.enabledStartTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('Start time', 'schedule.enabledStartTime', MetaListSortDirection.Descending),
]
</script>

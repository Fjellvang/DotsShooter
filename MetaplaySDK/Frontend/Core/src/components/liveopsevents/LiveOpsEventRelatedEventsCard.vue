<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Related Events"
  icon="calendar-days"
  :itemList="singleLiveOpsEventData?.relatedEvents"
  :searchFields="['displayName']"
  :filterSets="filterSets"
  description="A list of events that share the same template as this event."
  data-testid="related-events"
  )
  template(#item-card="{ item: relatedEvent }")
    MListItem {{ relatedEvent.displayName }}
      template(#badge)
        MBadge(v-if="singleLiveOpsEventData?.eventId === relatedEvent.eventId") Current Event
      template(#bottom-left)
        span(v-if="relatedEvent.schedule")
          div Starts: #[meta-time(:date="relatedEvent.schedule?.enabledStartTime ?? ''" showAs="datetime")]
          div End: #[meta-time(:date="relatedEvent.schedule?.enabledEndTime ?? ''" showAs="datetime")]
        span(v-else)
          div No schedule
      template(#top-right)
        MBadge(:variant="liveOpsEventPhaseInfos[relatedEvent.currentPhase].badgeVariant") {{ liveOpsEventPhaseInfos[relatedEvent.currentPhase].displayString }}
      template(#bottom-right)
        MTextButton(
          :to="`/liveOpsEvents/${relatedEvent.eventId}`"
          :disabled-tooltip="singleLiveOpsEventData?.eventId === relatedEvent.eventId ? 'You already browsing this events detail page.' : undefined"
          ) View event
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListFilterSet, MetaListFilterOption, MetaListCard } from '@metaplay/meta-ui'
import { MListItem, MBadge, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type { LiveOpsEventBriefInfo } from '../../liveOpsEventServerTypes'
import { liveOpsEventPhaseInfos } from '../../liveOpsEventUtils'
import { getSingleLiveOpsEventsSubscriptionOptions } from '../../subscription_options/liveOpsEvents'

const props = defineProps<{
  /**
   * ID of the event to show.
   */
  eventId: string
}>()

/**
 * Information about the event.
 */
const { data: singleLiveOpsEventData } = useSubscription(getSingleLiveOpsEventsSubscriptionOptions(props.eventId))

/**
 * Filtering options.
 */
const filterSets = computed(() => {
  return [
    new MetaListFilterSet('current', [
      new MetaListFilterOption(
        'Exclude current',
        (event) => (event as LiveOpsEventBriefInfo).eventId !== props.eventId
      ),
    ]),
  ]
})
</script>

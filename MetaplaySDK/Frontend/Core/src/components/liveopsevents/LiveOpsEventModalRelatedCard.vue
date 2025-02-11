<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Related Events"
  icon="calendar-days"
  :itemList="relatedEvents"
  :searchFields="['displayName']"
  :filterSets="filterSets"
  :page-size="4"
  data-testid="modal-related-events"
  )
  template(#item-card="{ item: relatedEvent }")
    MListItem {{ relatedEvent.displayName }}
      template(#badge)
        MBadge(v-if="eventId === relatedEvent.eventId") Current Event
      template(#bottom-left)
        span(v-if="relatedEvent.schedule")
          div Starts: #[meta-time(:date="relatedEvent.schedule?.enabledStartTime ?? ''" showAs="datetime")]
          div End: #[meta-time(:date="relatedEvent.schedule?.enabledEndTime ?? ''" showAs="datetime")]
        span(v-else)
          div No schedule
      template(#top-right)
        MBadge(:variant="liveOpsEventPhaseInfos[relatedEvent.currentPhase].badgeVariant") {{ liveOpsEventPhaseInfos[relatedEvent.currentPhase].displayString }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListFilterSet, MetaListFilterOption, MetaListCard } from '@metaplay/meta-ui'
import { MListItem, MBadge } from '@metaplay/meta-ui-next'

import type { LiveOpsEventBriefInfo, LiveOpsEventDetailsInfo } from '../../liveOpsEventServerTypes'
import { liveOpsEventPhaseInfos } from '../../liveOpsEventUtils'

const props = defineProps<{
  /**
   * List of related events to show.
   */
  relatedEvents: LiveOpsEventBriefInfo[]

  /**
   * ID of the event to show.
   */
  eventId?: string
}>()

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

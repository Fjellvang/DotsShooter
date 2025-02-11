<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Note: error handling is completely missing from this page, but this requires a potential bigger refactor.
//- Adding the error handling now might be wasted work if this shares more similarities with other pages.
MViewContainer
  template(#overview)
    MPageOverviewCard(
      :title="entityType + ' Event Timeline'"
      :id="entityId"
      data-testid="overview"
      )
      template(#subtitle)
        p A timeline of most recent events for #[MBadge {{ entityId }}].
        p The amount of cached events on this page is limited to keep the database impact small. The full history of events has been sent to your analytics pipeline!

      div(v-if="eventStreamStats")
        div(class="tw-mb-1 tw-font-bold") #[fa-icon(icon="chart-line")] Statistics
        b-table-simple(
          small
          responsive
          )
          b-tbody
            b-tr
              b-td Total events
              b-td(class="tw-text-right") {{ eventStreamStats.numEvents }}
            b-tr(v-if="eventStreamStats.newestEventTime")
              b-td Most recent event
              b-td(class="tw-text-right") #[meta-time(:date="eventStreamStats.newestEventTime" showAs="timeagoSentenceCase")]
            b-tr(v-if="eventStreamStats.oldestEventTime")
              b-td Oldest event
              b-td(class="tw-text-right") #[meta-time(:date="eventStreamStats.oldestEventTime" showAs="timeagoSentenceCase")]
            b-tr(v-if="eventStreamStats.duration")
              b-td Range of events
              b-td(class="tw-text-right") #[meta-duration(:duration="eventStreamStats.duration" showAs="humanizedSentenceCase")]

  entity-event-log-card(
    :entityKind="entityType"
    :entityId="entityId"
    :searchPreHighlight="String(initialSearchString)"
    :keywordPreFilters="keywordFilters"
    :eventTypePreFilters="eventTypeFilters"
    utilitiesMode="highlight"
    utilitiesCondition="or"
    :showViewMoreLink="false"
    maxHeight="inherit"
    class="h-100"
    @stats="onStats"
    )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'

import type { EventStreamStats } from '@metaplay/event-stream'
import { MBadge, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'

import EntityEventLogCard from '../components/entityeventlogs/EntityEventLogCard.vue'
import { routeParamToSingleValue } from '../coreUtils'

const route = useRoute()
const eventStreamStats = ref<EventStreamStats | null>(null)

const entityType = computed(() => routeParamToSingleValue(route.params.type))
const entityId = computed(() => routeParamToSingleValue(route.params.id))
const initialSearchString = computed(() => route.query.search ?? '')
const keywordFilters = computed((): string[] => {
  if (route.query.keywords) {
    const query = route.query.keywords
    if (typeof query === 'string') {
      return decodeURIComponent(query).split(',')
    } else if (Array.isArray(query)) {
      return query.map((query) => decodeURIComponent(String(query)))
    }
  }

  return []
})
const eventTypeFilters = computed((): string[] => {
  if (route.query.eventTypes) {
    const query = route.query.eventTypes
    if (typeof query === 'string') {
      return decodeURIComponent(query).split(',')
    } else if (Array.isArray(query)) {
      return query.map((query) => decodeURIComponent(String(query)))
    }
  }

  return []
})

function onStats(stats: EventStreamStats): void {
  eventStreamStats.value = stats
}
</script>

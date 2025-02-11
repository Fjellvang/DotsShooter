<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.analytics_events.view"
  :is-loading="!event || !analyticsEventBigQueryExampleData"
  :error="analyticsEventBigQueryExampleError"
  )
  template(#overview)
    MPageOverviewCard(
      :title="event?.displayName"
      :subtitle="event?.docString || 'No description provided.'"
      :id="`${event.typeCode}`"
      data-testid="analytics-event-detail-overview-card"
      )
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        class="tw-mt-1"
        )
        b-tbody
          b-tr
            b-td Category
            b-td(class="tw-text-right") {{ event.categoryName }}
          b-tr
            b-td Type
            b-td(class="tw-text-right") {{ event.eventType }}
          b-tr
            b-td Schema version
            b-td(class="tw-text-right") {{ event.schemaVersion }}
          b-tr
            b-td Parameters
            //b-td.text-right {{ event.parameters.join('\n') }}
            b-td(class="tw-text-right")
              div(
                v-for="event in eventParameters"
                :key="event"
                )
                MBadge {{ event }}

  b-card(data-testid="big-query-event-card")
    b-card-title Example BigQuery Event

    p(class="text-muted small")
      | A hypothetical BigQuery row, formatted as JSON for coarse testing. All event parameters are dummy values and may not represent the true value domain.
      | All list-typed parameters are expanded as having 2 elements. All dynamically typed parameters are expanded by repeating
      | the field values for each possible type. A real event will never have more than one type.

    div(class="log text-monospace border rounded bg-light tw-w-full")
      pre {{ analyticsEventBigQueryExampleData }}

  meta-raw-data(
    :kvPair="event"
    name="event"
    )
  meta-raw-data(
    :kvPair="analyticsEventBigQueryExampleData"
    name="exampleEvent"
    )
</template>

<script lang="ts" setup>
import { keyBy } from 'lodash-es'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MBadge, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { routeParamToSingleValue } from '../coreUtils'
import {
  getAllAnalyticsEventsSubscriptionOptions,
  getAnalyticsEventBigQueryExampleSubscriptionOptions,
} from '../subscription_options/analyticsEvents'

const { data: allAnalyticsEventsData } = useSubscription(getAllAnalyticsEventsSubscriptionOptions())
const analyticsEventsByTypeCode = computed(() => keyBy(allAnalyticsEventsData.value, (ev) => ev.typeCode))

const route = useRoute()
const { data: analyticsEventBigQueryExampleData, error: analyticsEventBigQueryExampleError } = useSubscription(
  getAnalyticsEventBigQueryExampleSubscriptionOptions(routeParamToSingleValue(route.params.id) || '')
)

// TODO: Make a dedicated endpoint to get a single event.
const event = computed(() => analyticsEventsByTypeCode.value[routeParamToSingleValue(route.params.id)])
const eventParameters = computed<any[]>(() => event.value.parameters || [])
</script>

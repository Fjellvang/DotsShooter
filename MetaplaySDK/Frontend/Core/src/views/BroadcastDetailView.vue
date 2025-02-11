<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.broadcasts.view"
  :is-loading="!singleBroadcastData"
  :error="singleBroadcastError"
  )
  template(#overview)
    MPageOverviewCard(
      :id="`${decoratedBroadcasts.message.params.id}`"
      :title="decoratedBroadcasts.message.params.name"
      )
      template(#badge)
        MBadge(
          :variant="decoratedBroadcasts.decoration.variant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(:icon="decoratedBroadcasts.decoration.icon")
          | {{ decoratedBroadcasts.decoration.status }}

      div
        span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
        b-table-simple(
          small
          responsive
          class="tw-mt-1"
          )
          b-tbody
            b-tr
              b-td Audience Size Estimate
              b-td(class="tw-text-right") #[meta-audience-size-estimate(:sizeEstimate="decoratedBroadcasts.message.params.isTargeted ? audienceSizeEstimate : undefined")]
            b-tr
              b-td Start Time
              b-td(class="tw-text-right") #[meta-time(:date="decoratedBroadcasts.message.params.startAt" showAs="datetime")]
            b-tr
              b-td Expiry Time
              b-td(class="tw-text-right") #[meta-time(:date="decoratedBroadcasts.message.params.endAt" showAs="datetime")]
            b-tr
              b-td Received By
              b-td(class="tw-text-right") #[meta-abbreviate-number(:value="decoratedBroadcasts.message.stats.receivedCount" unit="player")]
            b-tr
              b-td Trigger
              b-td(class="tw-text-right") {{ triggerConditionDisplayName ? `On ${triggerConditionDisplayName}` : 'Immediate' }}

      template(#buttons)
        div(class="tw-inline-flex tw-space-x-2")
          //- Duplicate broadcast.
          broadcast-form-button(
            v-if="updatedBroadcast"
            button-text="Duplicate"
            :prefillData="updatedBroadcast"
            :editBroadcast="false"
            class="mr-2"
            @refresh="singleBroadcastRefresh()"
            )

          //- Edit broadcast.
          broadcast-form-button(
            button-text="Edit"
            :prefillData="updatedBroadcast"
            :disabledTooltip="decoratedBroadcasts.decoration.status === 'Expired' ? 'The broadcast has expired and can not be edited.' : undefined"
            :editBroadcast="true"
            class="mr-2"
            @refresh="singleBroadcastRefresh()"
            )

          broadcast-delete-form-button(:id="`${route.params.id}`") Delete

  core-ui-placement(
    placementId="Broadcasts/Details"
    :broadcastId="String(decoratedBroadcasts.message.params.id)"
    )

  meta-raw-data(
    :kvPair="singleBroadcastData"
    name="broadcast"
    )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MBadge, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaAudienceSizeEstimate from '../components/MetaAudienceSizeEstimate.vue'
import BroadcastDeleteFormButton from '../components/mails/BroadcastDeleteFormButton.vue'
import BroadcastFormButton from '../components/mails/BroadcastFormButton.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue } from '../coreUtils'
import { getAllAnalyticsEventsSubscriptionOptions } from '../subscription_options/analyticsEvents'
import { getSingleBroadcastSubscriptionOptions } from '../subscription_options/broadcasts'

const route = useRoute()

/**
 * Subscribe to analytics events data.
 */
const { data: analyticsEvents } = useSubscription(getAllAnalyticsEventsSubscriptionOptions())

/**
 * Subscribe to target broadcast data.
 */
const {
  data: singleBroadcastData,
  refresh: singleBroadcastRefresh,
  error: singleBroadcastError,
} = useSubscription(getSingleBroadcastSubscriptionOptions(routeParamToSingleValue(route.params.id) || ''))

/**
 * Broadcast data to be displayed in this card.
 */
const decoratedBroadcasts = computed(() => {
  if (!singleBroadcastData.value) return undefined
  const now = DateTime.now()
  const startAtDate = DateTime.fromISO(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    singleBroadcastData.value.message.params.startAt
  )
  const endAtDate = DateTime.fromISO(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    singleBroadcastData.value.message.params.endAt
  )
  if (endAtDate > now && startAtDate < now) {
    return {
      ...singleBroadcastData.value,
      decoration: {
        status: 'Active',
        variant: 'success',
        icon: 'broadcast-tower',
      },
    }
  } else if (endAtDate > now) {
    return {
      ...singleBroadcastData.value,
      decoration: {
        status: 'Scheduled',
        variant: 'primary',
        icon: 'calendar-alt',
      },
    }
  } else {
    return {
      ...singleBroadcastData.value,
      decoration: {
        status: 'Expired',
        variant: 'neutral',
        icon: 'times',
      },
    }
  }
})

/**
 * Estimated of number of players targeted in the broadcast.
 */
const audienceSizeEstimate = computed(() => {
  return decoratedBroadcasts.value?.audienceSizeEstimate
})

/**
 * Existing broadcast content which is to be edited or duplicated.
 */
const updatedBroadcast = computed(() => {
  return decoratedBroadcasts.value?.message.params
})

/**
 * Condition that must be true for a targeted broadcast to be sent.
 */
const triggerConditionDisplayName = computed((): string | undefined => {
  return decoratedBroadcasts.value.message.params.triggerCondition == null
    ? undefined
    : (analyticsEvents.value?.find(
        (x) => x.typeCode === decoratedBroadcasts.value.message?.params.triggerCondition.eventTypeCode
      )?.displayName ?? 'UNKNOWN')
})
</script>

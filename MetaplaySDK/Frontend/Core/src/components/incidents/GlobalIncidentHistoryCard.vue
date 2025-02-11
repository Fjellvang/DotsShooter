<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A card that shows incidents for all players. Defers rendering to the incident-history-card. -->

<template lang="pug">
IncidentHistoryCard(
  v-if="!incidentReportsError"
  :incidents="incidentReportsData"
  :isLimitedTo="count"
  :showMainPageLink="showMainPageLink"
  data-testid="global-incident-history-card"
  )

MCard(
  v-else
  title="Incident History"
  )
  template(#icon)
    fa-icon(:icon="['fas', 'ambulance']")
  p(class="tw-text-red-500") Failed to load the incident history.
  MErrorCallout(:error="incidentReportsError")
</template>

<script lang="ts" setup>
import { MCard, MErrorCallout } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getIncidentReportsSubscriptionOptions } from '../../subscription_options/general'
import IncidentHistoryCard from './IncidentHistoryCard.vue'

const props = defineProps<{
  /**
   * The maximum number of incidents to show.
   */
  count: number
  /**
   * Optional: Show incidents that match this fingerprint.
   */
  fingerprint?: string
  /**
   * Optional: Includes a link to the main incidents page inside the card.
   */
  showMainPageLink?: boolean
}>()

const { data: incidentReportsData, error: incidentReportsError } = useSubscription(
  getIncidentReportsSubscriptionOptions(props.count, props.fingerprint)
)
</script>

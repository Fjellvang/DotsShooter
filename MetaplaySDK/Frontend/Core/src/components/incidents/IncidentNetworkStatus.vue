<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Network status (for network errors)
MCard(
  v-if="playerIncidentData"
  title="Network Status"
  :variant="!hasNetworkInformation ? 'neutral' : 'primary'"
  )
  p(
    v-if="!hasNetworkInformation"
    class="tw-m-0 tw-text-center tw-italic tw-text-neutral-500"
    ) Network information not included in this incident report.

  div(v-if="hasNetworkConnectionFailure")
    b-table-simple(
      small
      responsive="xs"
      )
      b-tbody
        b-tr
          b-td(class="td-left") Error
          b-td(class="td-right tw-text-right") {{ playerIncidentData.subType }}
        b-tr
          b-td(class="td-left") Reachability
          b-td(class="td-right tw-text-right") {{ playerIncidentData.networkReachability }}
        b-tr(
          v-for="field in getObjectPrintableFields(playerIncidentData.endpoint)"
          :key="field.key"
          )
          b-td(class="td-left") {{ field.name }}
          b-td(class="td-right tw-text-right") {{ field.value }}
        b-tr
          b-td(class="td-left") TLS Peer
          b-td(class="td-right tw-text-right") {{ playerIncidentData.tlsPeerDescription }}

  network-diagnostic-report(
    :incidentId="incidentId"
    :playerId="playerId"
    )
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue'

import { getObjectPrintableFields } from '@metaplay/meta-ui'
import { MCard } from '@metaplay/meta-ui-next'
import { fetchSubscriptionDataOnceOnly } from '@metaplay/subscriptions'

import { getPlayerIncidentSubscriptionOptions } from '../../subscription_options/incidents'
import NetworkDiagnosticReport from './NetworkDiagnosticReport.vue'

const props = defineProps<{
  /**
   * ID of the incident to show.
   */
  incidentId: string
  /**
   * ID of the player to show.
   */
  playerId: string
}>()

/**
 * Player incident data displayed on this card
 */
const playerIncidentData = ref()

/**
 * Subscribe once to data needed to render this component.
 */
onMounted(async () => {
  fetchSubscriptionDataOnceOnly(getPlayerIncidentSubscriptionOptions(props.playerId, props.incidentId))
    .then((data) => {
      playerIncidentData.value = data
    })
    .catch((err) => {
      throw new Error(`Failed to load data from the server! Reason: ${err.message}.`)
    })
})

/**
 * Return true when a network connection error is detected.
 */
const hasNetworkConnectionFailure = computed(() => {
  return playerIncidentData.value?.networkError !== undefined
})

/**
 * Returns true when the connection error includes additional diagnostic information.
 */
const hasNetworkInformation = computed(() => {
  return !!(
    hasNetworkConnectionFailure.value ||
    playerIncidentData.value?.networkReport ||
    playerIncidentData.value?.networkReportObsolete
  )
})
</script>
<style scoped>
.td-left {
  min-width: 2rem;
}
.td-right {
  max-width: 10rem;
}
</style>

<template lang="pug">
MCard(
  :variant="playerIncidentData?.type === 'PlayerChecksumMismatch' && playerIncidentData?.playerModelDiff ? 'primary' : 'neutral'"
  :is-loading="!playerIncidentData"
  title="Player Model Desync Report"
  :subtitle="playerIncidentData?.type === 'PlayerChecksumMismatch' && playerIncidentData?.playerModelDiff ? 'The client and server models got out of sync. This is the raw output of the resulting diff.' : undefined"
  )
  pre(
    v-if="playerIncidentData?.playerModelDiff"
    style="max-height: 30rem"
    class="log tw-w-full tw-rounded tw-border tw-border-neutral-300 tw-bg-neutral-100"
    )
    span {{ playerIncidentData.playerModelDiff }}

  p(
    v-else
    class="tw-m-0 tw-text-center tw-italic tw-text-neutral-500"
    ) No player model desync included in this incident report.
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'

import { MCard } from '@metaplay/meta-ui-next'
import { fetchSubscriptionDataOnceOnly } from '@metaplay/subscriptions'

import { getPlayerIncidentSubscriptionOptions } from '../../subscription_options/incidents'

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
</script>

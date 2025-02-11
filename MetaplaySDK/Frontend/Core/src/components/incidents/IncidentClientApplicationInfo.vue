<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  v-if="playerIncidentData"
  title="Client Application Info"
  :variant="!playerIncidentData.applicationInfo ? 'neutral' : 'primary'"
  )
  p(
    v-if="!playerIncidentData.applicationInfo"
    class="m-3 text-muted tw-text-center"
    ) Application information not included in this incident report.
  b-table-simple(
    v-else
    small
    responsive
    )
    b-tbody
      b-tr
        b-td Application Version
        b-td(class="tw-text-right") {{ playerIncidentData.applicationInfo.buildVersion.version }}
      b-tr
        b-td Build Number
        b-td(class="tw-text-right") {{ stringToSentenceCase(playerIncidentData.applicationInfo.buildVersion.buildNumber) }}
      b-tr
        b-td Commit Id
        b-td(
          v-if="!playerIncidentData.applicationInfo.buildVersion.commitId"
          class="text-right text-muted"
          ) Not available
        b-td(
          v-else
          class="tw-text-right"
          ) {{ stringToSentenceCase(playerIncidentData.applicationInfo.buildVersion.commitId) }}
      b-tr
        b-td Device GUID
        b-td(class="tw-text-right") {{ playerIncidentData.applicationInfo.deviceGuid }}
      b-tr
        b-td Active Language
        b-td(class="tw-text-right") {{ stringToSentenceCase(playerIncidentData.applicationInfo.activeLanguage) }}
      b-tr
        b-td Highest Supported Logic Version
        b-td(class="tw-text-right") {{ playerIncidentData.applicationInfo.highestSupportedLogicVersion }}
</template>

<script lang="ts" setup>
import { onMounted, ref } from 'vue'

import { MCard } from '@metaplay/meta-ui-next'
import { stringToSentenceCase } from '@metaplay/meta-utilities'
import { fetchSubscriptionDataOnceOnly } from '@metaplay/subscriptions'

import { getPlayerIncidentSubscriptionOptions } from '../../subscription_options/incidents'

const props = defineProps<{
  /**
   * ID of the incident data displayed on this card.
   */
  incidentId: string
  /**
   * ID of the player whose data is shown on this card.
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

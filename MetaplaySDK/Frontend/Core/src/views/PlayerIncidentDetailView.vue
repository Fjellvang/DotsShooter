<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!playerIncidentData"
  :error="playerIncidentError"
  :alerts="alerts"
  :variant="isCloseToDeletionDate ? 'warning' : undefined"
  permission="api.incident_reports.view"
  )
  template(#alerts)
    MCallout(
      v-if="isCloseToDeletionDate"
      title="This incident will be deleted soon!"
      )
      div This incident report will be deleted
        |#[span(class="tw-font-semibold") #[MetaTime(:date="playerIncidentData.deletionDateTime" showAs="timeago")]]
        |
        | at #[MetaTime(:date="playerIncidentData.deletionDateTime" showAs="time" disableTooltip)]
        |
        | on #[MetaTime(:date="playerIncidentData.deletionDateTime" showAs="date" disableTooltip)].
      div(class="tw-mt-1") Incidents are deleted every now and then, we recommend saving the data elsewhere if it is important.

  template(#overview)
    MPageOverviewCard(
      :id="playerIncidentData.incidentId"
      title="Player Incident Report"
      )
      template(#subtitle)
        span(class="text-danger") {{ playerIncidentData.exceptionMessage }}
        MCallout(
          v-if="playerIncidentData.subType === 'NullReferenceException'"
          title="Did you know?"
          class="tw-mt-4"
          )
          div Null reference exceptions are a very common category of errors in Unity that do not cause the game crash outright, but often cause the game to appear stuck or otherwise 'broken' for the players.

      div(class="tw-mb-1 tw-font-semibold") #[fa-icon(icon="chart-bar")] Overview
      table(class="tw-w-full")
        tbody(class="tw-divide-y tw-divide-neutral-200 tw-border-t tw-border-neutral-200 *:*:tw-py-1")
          tr
            td Type
            td(class="tw-text-right") {{ playerIncidentData.type }}
          tr
            td Subtype
            td(class="tw-text-right") {{ playerIncidentData.subType }}
          tr
            td Uploaded At
            td(class="tw-text-right") #[MetaTime(:date="playerIncidentData.uploadedAt" showAs="datetime")]
          tr
            td Occurred At (device time)
            td(class="tw-text-right") #[MetaTime(:date="playerIncidentData.occurredAt" showAs="datetime")]
          tr
            td Deletion Time
            td(class="tw-text-right") #[MetaTime(:date="playerIncidentData.deletionDateTime" showAs="datetime")]
          tr
            td Player ID
            td(class="tw-text-right") #[MTextButton(:to="`/players/${route.params.playerId}`") {{ route.params.playerId }}]

  template(#default)
    core-ui-placement(
      placementId="PlayerIncidents/Details"
      :incidentId="incidentId"
      :playerId="playerId"
      )

    MetaRawData(
      :kvPair="playerIncidentData"
      name="incident"
      )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaRawData, MetaTime } from '@metaplay/meta-ui'
import {
  MCallout,
  MTextButton,
  MPageOverviewCard,
  MViewContainer,
  type MViewContainerAlert,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue } from '../coreUtils'
import { getPlayerIncidentSubscriptionOptions } from '../subscription_options/incidents'

const route = useRoute()
const playerId = routeParamToSingleValue(route.params.playerId)
const incidentId = routeParamToSingleValue(route.params.incidentId)

const { data: playerIncidentData, error: playerIncidentError } = useSubscription(
  getPlayerIncidentSubscriptionOptions(playerId, incidentId)
)

const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  if (!playerId) {
    allAlerts.push({
      title: 'No player ID parameter detected!',
      message: 'Fetching incident reports requires a player ID.',
      variant: 'danger',
      dataTest: 'incident-no-playerId',
    })
  } else if (!incidentId) {
    allAlerts.push({
      title: 'No incident ID parameter detected!',
      message: 'Fetching incident reports requires an incident ID',
      variant: 'warning',
      dataTest: 'incident-no-incidentId',
    })
  }

  return allAlerts
})

const isCloseToDeletionDate = computed((): boolean => {
  return (
    playerIncidentData.value &&
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    DateTime.fromISO(playerIncidentData.value.deletionDateTime).diffNow('days').days <= 3
  )
})
</script>

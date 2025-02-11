<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(permission="api.incident_reports.view")
  template(#overview)
    MPageOverviewCard(title="Player Incidents of Type")
      template(#subtitle)
        div View a list of all errors of this type collected from your game clients within the last #[MetaDuration(:duration="incidentReportRetentionPeriod" showAs="exactDuration")].

  core-ui-placement(
    placementId="PlayerIncidents/ByType"
    :fingerprint="route.params.fingerprint"
    )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { getRuntimeOptionsSubscriptionOptions } from '../subscription_options/general'

const route = useRoute()

/**
 * Runtime options for the game server.
 */
const { data: runtimeOptionsData } = useSubscription(getRuntimeOptionsSubscriptionOptions())

/**
 * The retention period for incident reports, fished out of the runtime options.
 */
const incidentReportRetentionPeriod = computed(() => {
  const options = runtimeOptionsData.value?.options.find((option: any) => option.name === 'System')
  if (!options) return 0
  return options.values.incidentReportRetentionPeriod
})
</script>

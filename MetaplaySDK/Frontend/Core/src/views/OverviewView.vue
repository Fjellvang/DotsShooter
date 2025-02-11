<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!backendStatusData || !databaseItemCountsData"
  :error="backendStatusError || databaseItemCountsError || errorCountsError"
  )
  template(#alerts)
    div(class="tw-space-y-2")
      MCallout(
        v-if="errorCountsData && errorCountsData.errorCount > 0"
        variant="danger"
        :title="maybePluralString(errorCountsData.errorCount, 'Game Server Error')"
        ) Server errors may indicate a problem with your game. Please investigate by looking at the #[MTextButton(to="/serverErrors" permission="api.system.view_error_logs") message center page].

      MCallout(
        v-if="telemetryMessagesData?.messages && telemetryMessagesData.messages.length > 0"
        :variant="getHighestTelemetryMessageVariant(telemetryMessagesData.messages)"
        :title="maybePluralString(telemetryMessagesData.messages.length, 'Telemetry Message')"
        ) One or more game server components have newer versions available. You can view them on the #[MTextButton(to="/serverErrors" permission="api.system.view_telemetry_messages") message center page].

  template(#overview)
    MPageOverviewCard(
      title="Game Server Status"
      :subtitle="`Build: ${staticInfos.gameServerInfo.buildNumber}`"
      data-testid="overview-card"
      )
      template(#caption) Commit ID: {{ staticInfos.gameServerInfo.commitId }}

      b-row
        b-col(
          sm
          class="border-right-md"
          )
          span(class="font-weight-bold") Game Server
          table(class="table table-sm tw-mt-1")
            tbody
              tr
                td Live Concurrents
                td(class="text-right") #[MetaAbbreviateNumber(:value="backendStatusData.numConcurrents")]
              tr(
                v-for="entry in coreStore.actorOverviews.overviewListEntries"
                :key="entry.key"
                )
                td {{ entry.displayName }}
                td(class="text-right") #[MetaAbbreviateNumber(:value="backendStatusData.liveEntityCounts[entry.key]")]
              tr
                td Maintenance Mode
                td(class="text-right")
                  MBadge(
                    v-if="backendStatusData.maintenanceStatus.isInMaintenance"
                    variant="danger"
                    ) On
                  MBadge(
                    v-else-if="backendStatusData.maintenanceStatus.scheduledMaintenanceMode"
                    variant="warning"
                    ) Scheduled
                  MBadge(v-else) Off
              tr
                td(:class="{ 'tw-text-red-500': errorCountsData && errorCountsData.errorCount > 0 }") Server Errors
                td(class="tw-text-right")
                  span(
                    v-if="!errorCountsData"
                    class="tw-italic tw-text-neutral-500"
                    ) Loading...
                  span(v-else-if="errorCountsData.errorCount === 0") 0
                  span(
                    v-else-if="!errorCountsData.overMaxErrorCount"
                    class="tw-text-red-500"
                    ) {{ errorCountsData.errorCount }}
                  span(
                    v-else
                    class="tw-text-red-500"
                    ) {{ errorCountsData.errorCount }}+

        b-col(sm)
          span(class="font-weight-bold") Database
          table(class="table table-sm tw-mt-1")
            tbody
              tr
                td Type
                td(class="text-right text-right"): MBadge {{ backendStatusData.databaseStatus.backend }}
              tr
                td Active Shards
                td(class="text-right text-right") {{ backendStatusData.databaseStatus.activeShards }}/{{ backendStatusData.databaseStatus.totalShards }}
              tr(
                v-for="entry in coreStore.actorOverviews.databaseListEntries"
                :key="entry.key"
                )
                td {{ entry.displayName }}
                td(class="text-right") #[MetaAbbreviateNumber(:value="databaseItemCount(entry.key)")]

  template(#default)
    CoreUiPlacement(placementId="OverviewView")

    MetaRawData(
      :kvPair="backendStatusData"
      name="backendStatusData"
      )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import { MetaAbbreviateNumber, MetaRawData } from '@metaplay/meta-ui'
import { MBadge, MPageOverviewCard, MViewContainer, MCallout, MTextButton, type Variant } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { useCoreStore } from '../coreStore'
import {
  getBackendStatusSubscriptionOptions,
  getDatabaseItemCountsSubscriptionOptions,
  getServerErrorsSubscriptionOptions,
  getTelemetryMessagesSubscriptionOptions,
} from '../subscription_options/general'

const { data: backendStatusData, error: backendStatusError } = useSubscription(getBackendStatusSubscriptionOptions())
const { data: databaseItemCountsData, error: databaseItemCountsError } = useSubscription(
  getDatabaseItemCountsSubscriptionOptions()
)
const { data: telemetryMessagesData, error: telemetryMessagesError } = useSubscription(
  getTelemetryMessagesSubscriptionOptions()
)

const coreStore = useCoreStore()

/**
 * Calculates number of items by type across all database shards.
 * @param itemType Type of item to count (Players, Guilds, etc).
 * @returns The number of items of the specified type in the database.
 */
function databaseItemCount(itemType: string): number {
  return databaseItemCountsData.value?.totalItemCounts?.[itemType + 's'] || 0
}

// Server errors ------------------------------------------------------------------------------------------------------

const staticInfos = useStaticInfos()
const { data: errorCountsData, error: errorCountsError } = useSubscription(getServerErrorsSubscriptionOptions())

const grafanaEnabled = computed(() => !!staticInfos.environmentInfo.grafanaUri)

/**
 * Link to the Grafana Loki logs.
 */
const grafanaLogsLink = computed(() => {
  if (staticInfos.environmentInfo.grafanaUri) {
    const namespaceStr = staticInfos.environmentInfo.kubernetesNamespace
      ? `,namespace=\\"${staticInfos.environmentInfo.kubernetesNamespace}\\"`
      : ''
    return `${staticInfos.environmentInfo.grafanaUri}/explore?orgId=1&left={"datasource": "Loki", "queries":[{"expr":"{app=\\"metaplay-server\\"${namespaceStr}}"}],"range":{"from":"now-1h","to":"now"}}`
  } else {
    return undefined
  }
})

// Telemetry messages --------------------------------------------------------------------------------------------------

function getHighestTelemetryMessageVariant(messages: Array<{ level: string }>): Variant {
  const levels = messages.map((msg) => msg.level)
  if (levels.includes('Error')) {
    return 'danger'
  } else if (levels.includes('Warning')) {
    return 'warning'
  } else {
    return 'primary'
  }
}
</script>

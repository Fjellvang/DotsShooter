<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="dashboard.environment.view"
  full-width
  )
  template(#overview)
    MPageOverviewCard(title="Environment Details")
      p Overview of the current environment this game server is running in.
      p(class="tw-mb-4 tw-mt-2 tw-text-xs tw-text-neutral-500") Easiest way to configure your game servers to act differently in different environments is through the runtime options system. Please have a look at the #[MTextButton(to="https://docs.metaplay.io/game-server-programming/how-to-guides/working-with-runtime-options.html") runtime options documentation] to get started.

      div(class="tw-mb-1 tw-font-semibold")
        fa-icon(
          icon="chart-bar"
          class="tw-mr-1"
          )
        span Overview
      table(class="tw-w-full")
        tbody(class="tw-divide-y tw-divide-neutral-200 tw-border-t tw-border-neutral-200 *:*:tw-py-1")
          tr
            td Environment Name
            td(class="tw-text-right") {{ staticInfos.projectInfo.projectName }}
          tr
            td Environment Type
            td(class="tw-text-right") {{ staticInfos.environmentInfo.environmentFamily }}
          tr
            td Chart Version
            td(
              class="tw-text-right"
              :class="{ 'tw-italic tw-text-neutral-500': chartVersion === 'Null' }"
              ) {{ chartVersion === 'Null' ? 'Not available' : chartVersion }}
          tr
            td Maintenance Mode
            td(class="tw-text-right")
              MBadge(:variant="backendStatusData?.maintenanceStatus.isInMaintenance ? 'warning' : 'neutral'") {{ backendStatusData?.maintenanceStatus.isInMaintenance ? 'On' : 'Off' }}
          tr
            td Total Players in Database
            td(class="tw-text-right")
              span(v-if="databaseItemCountsData") {{ databaseItemCountsData.totalItemCounts.Players }}
              span(v-else) Not available

  template(#default)
    MTabLayout(:tabs="tabOptions")
      template(#tab-0)
        //- Cluster
        MSingleColumnLayout(class="tw-mb-4")
          MErrorCallout(
            v-if="clusterError"
            title="Cluster Error"
            :error="clusterError"
            class="tw-mb-3"
            )
          MErrorCallout(
            v-if="clusterError"
            title="Load Tracking Error"
            :error="clusterError"
            class="tw-mb-3"
            )
          NodeSetsCard(v-else)
          //- Raw data
          meta-raw-data(
            :kvPair="clusterData"
            name="cluster"
            )

      template(#tab-1)
        //- Database
        MErrorCallout(
          v-if="databaseStatusError"
          title="Database Error"
          :error="databaseStatusError"
          )

        MTwoColumnLayout(
          v-else
          class="tw-mb-4"
          )
          DatabaseShardsCard
          DatabaseItemsCard
        //- Raw data
        meta-raw-data(
          :kvPair="databaseStatusData"
          name="databaseStatus"
          )

      template(#tab-2)
        MErrorCallout(
          v-if="runtimeOptionsError"
          title="Runtime Options Error"
          :error="runtimeOptionsError"
          )
        //- Runtime options
        RuntimeOptionsCard(v-else)
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import {
  MPageOverviewCard,
  MSingleColumnLayout,
  MTwoColumnLayout,
  MViewContainer,
  MTabLayout,
  type TabOption,
  MErrorCallout,
  MBadge,
  MTextButton,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import DatabaseItemsCard from '../components/enviroment/DatabaseItemsCard.vue'
import DatabaseShardsCard from '../components/enviroment/DatabaseShardsCard.vue'
import NodeSetsCard from '../components/enviroment/NodeSetsCard.vue'
import RuntimeOptionsCard from '../components/enviroment/RuntimeOptionsCard.vue'
import {
  getDatabaseStatusSubscriptionOptions,
  getClusterSubscriptionOptions,
  getRuntimeOptionsSubscriptionOptions,
  getBackendStatusSubscriptionOptions,
  getDatabaseItemCountsSubscriptionOptions,
} from '../subscription_options/general'

const staticInfos = useStaticInfos()
const { data: backendStatusData, error: backendStatusError } = useSubscription(getBackendStatusSubscriptionOptions())
const { data: databaseItemCountsData, error: databaseItemCountsError } = useSubscription(
  getDatabaseItemCountsSubscriptionOptions()
)

/**
 * Load tracking data displayed on this page.
 */
const { data: clusterData, error: clusterError } = useSubscription(getClusterSubscriptionOptions())

/**
 * Database status data displayed on this page.
 */
const { data: databaseStatusData, error: databaseStatusError } = useSubscription(getDatabaseStatusSubscriptionOptions())

/**
 * Subscribe to the runtime options data.
 */
const { data: runtimeOptionsData, error: runtimeOptionsError } = useSubscription(getRuntimeOptionsSubscriptionOptions())

/**
 * Tab options for the environment view.
 */
const tabOptions: TabOption[] = [
  {
    label: 'Servers',
  },
  {
    label: 'Database',
    permission: 'api.database.status',
  },
  {
    label: 'Runtime Options',
    permission: 'api.runtime_options.view',
  },
]

/**
 * Chart version of the current environment.
 */
const chartVersion = computed(() => {
  return (
    runtimeOptionsData.value?.options?.find((option: any) => option.name === 'Deployment')?.values?.chartVersion ||
    'Null'
  )
})
</script>

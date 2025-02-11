<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
b-row(class="justify-content-center")
  b-col(
    lg="9"
    xl="8"
    )
    b-card(
      title="Cluster Metrics & Logs"
      :class="grafanaEnabled ? '' : 'bg-light'"
      style="min-height: 11rem"
      class="shadow-sm tw-mb-4"
      data-testid="grafana-card"
      )
      div(v-if="grafanaEnabled")
        p Grafana is an industry standard tool for diving into the "engine room" level system health metrics and server logs.
        div(class="tw-space-x-1.5 tw-text-right")
          MButton(
            permission="dashboard.grafana.view"
            :to="grafanaMetricsLink"
            :disabled-tooltip="!grafanaEnabled ? 'Grafana has not been configured for this environment.' : undefined"
            ) View Metrics
            template(#icon): fa-icon(
              icon="external-link-alt"
              class="tw-mr-1 tw-h-3.5 tw-w-4"
              )
          MButton(
            permission="dashboard.grafana.view"
            :to="grafanaLogsLink"
            :disabled-tooltip="!grafanaEnabled ? 'Grafana has not been configured for this environment.' : undefined"
            ) View Logs
            template(#icon): fa-icon(
              icon="external-link-alt"
              class="tw-mr-1 tw-h-3.5 tw-w-4"
              )
      div(
        v-else
        class="mt-5 tw-text-center"
        )
        div(class="text-muted") Grafana has not been configured for this environment.
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import { MButton } from '@metaplay/meta-ui-next'

const staticInfos = useStaticInfos()

const grafanaEnabled = computed(() => !!staticInfos.environmentInfo.grafanaUri)

/**
 * Link to Grafana metrics dashboard.
 */
const grafanaMetricsLink = computed(() => {
  if (grafanaEnabled.value && staticInfos) {
    return staticInfos.environmentInfo.grafanaUri + '/d/rCI05Y4Mz/metaplay-server'
  } else {
    return undefined
  }
})

/**
 * Link to the Grafana Loki logs.
 */
const grafanaLogsLink = computed(() => {
  if (grafanaEnabled.value && staticInfos) {
    const namespaceStr = staticInfos.environmentInfo.kubernetesNamespace
      ? `,namespace=\\"${staticInfos.environmentInfo.kubernetesNamespace}\\"`
      : ''
    return `${staticInfos.environmentInfo.grafanaUri}/explore?orgId=1&left={"datasource": "Loki", "queries":[{"expr":"{app=\\"metaplay-server\\"${namespaceStr}}"}],"range":{"from":"now-1h","to":"now"}}`
  } else {
    return undefined
  }
})
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTwoColumnLayout
  MCard(
    title="Concurrents"
    :badge="backendStatus.numConcurrents"
    :badge-variant="backendStatus.numConcurrents > 0 ? 'primary' : 'neutral'"
    data-testid="concurrents-card"
    )
    meta-bar-chart(
      :data="allCharts.Concurrents"
      chart-id="concurrents-chart"
      )

  MCard(
    v-for="chart in coreStore.actorOverviews.charts"
    :key="chart.key"
    :title="chart.displayName"
    :badge="backendStatus.liveEntityCounts[chart.key]"
    :badge-variant="backendStatus.liveEntityCounts[chart.key] > 0 ? 'primary' : 'neutral'"
    :data-testid="camelCaseToKebabCase(chart.key) + '-card'"
    )
    meta-bar-chart(
      v-if="allCharts[chart.key]"
      :data="allCharts[chart.key]"
      chart-id="`${chart.key}-chart`"
      )
</template>

<script lang="ts" setup>
import { onBeforeUnmount, ref } from 'vue'

import { MTwoColumnLayout, MCard } from '@metaplay/meta-ui-next'
import { camelCaseToKebabCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../coreStore'
import { getBackendStatusSubscriptionOptions } from '../../subscription_options/general'
import type { StatusResponse } from '../../subscription_options/generalTypes'

const coreStore = useCoreStore()
const { data: backendStatus } = useSubscription(getBackendStatusSubscriptionOptions())

/**
 * Object containing all chart data. Initializes with empty data.
 */
const allCharts = ref<Record<string, { labels: string[]; datasets: Array<{ data: number[] }> }>>(
  Object.fromEntries(
    [{ key: 'Concurrents' }, ...coreStore.actorOverviews.charts].map((chart) => [
      chart.key,
      {
        labels: ['', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', ''],
        datasets: [
          {
            data: Array.from({ length: 20 }, () => 0),
          },
        ],
      },
    ])
  )
)

// Update charts on a timer.
const timer = ref<ReturnType<typeof setTimeout> | undefined>(undefined)

/**
 * Async function that polls the chart data.
 */
function poll(): void {
  if (backendStatus.value) {
    // Copy allCharts to a new object and mutate that object before saving it back to allCharts.
    const newAllCharts = JSON.parse(JSON.stringify(allCharts.value)) as typeof allCharts.value
    newAllCharts.Concurrents.datasets[0].data.shift()
    newAllCharts.Concurrents.datasets[0].data.push((backendStatus.value as StatusResponse).numConcurrents)

    for (const chart of coreStore.actorOverviews.charts) {
      newAllCharts[chart.key].datasets[0].data.shift()
      newAllCharts[chart.key].datasets[0].data.push(
        (backendStatus.value as StatusResponse).liveEntityCounts[chart.key] ?? 0
      )
    }
    allCharts.value = newAllCharts
  }
  timer.value = setTimeout(() => {
    poll()
  }, 5000)
}
onBeforeUnmount(() => {
  clearTimeout(timer.value)
})

poll()
</script>

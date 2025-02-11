<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.experiments.view"
  :is-loading="!allExperimentsData"
  :alerts="headerAlerts"
  :error="allExperimentsError"
  )
  template(#overview)
    MPageOverviewCard(
      title="View Experiments"
      data-testid="experiment-list-overview-card"
      )
      p(class="tw-mb-1") Experiments allow you to test out variants of your game config on your players.
      p(class="tw-mb-4 tw-text-xs tw-text-neutral-500") Experiments deliver variations of your game configs to a subset of your player base. You can then use your analytics to discover which variant performs the best. Define experiments and their variants in your #[MTextButton(to="/gameConfigs") game configs] and then configure and administer them from here.

      div(class="tw-mb-1 tw-font-semibold") Performance Tip
      p(class="tw-text-xs") Rolling out experiments introduces new combinations of your game configs that require memory on the game servers. You currently have #[meta-plural-label(:value="runningExperimentInfos.length" label="experiment")] running with a total of #[meta-plural-label(:value="allExperimentsData.combinations.currentCombinations" label="possible combination")]. Have a look at the #[MTextButton(to="https://docs.metaplay.io/feature-cookbooks/experiments-a-b-testing/working-with-experiments.html#technical-details-of-specialized-game-config-delivery") experiments documentation] to learn more.

  MSingleColumnLayout
    meta-list-card(
      title="All Experiments"
      :itemList="decoratedExperimentInfos"
      :searchFields="['experimentId', 'displayName', 'description']"
      :sortOptions="sortOptions"
      :filterSets="filterSets"
      :defaultSortOption="1"
      :pageSize="10"
      emptyMessage="No player experiments. Set them up in your game configs to start using the feature!"
      data-testid="all-experiments-list-card"
      )
      template(#item-card="{ item: experimentInfo }")
        MListItem {{ experimentInfo.displayName }} / {{ experimentInfo.experimentId }}
          template(#top-right)
            MBadge(:variant="experimentInfo.phaseInfo.titleVariant") {{ experimentInfo.phaseInfo.displayName }}
            span(
              v-if="experimentInfo.phaseStartedAt"
              class="tw-ml-1"
              ) since #[meta-time(:date="experimentInfo.phaseStartedAt")]
          template(#bottom-left) {{ experimentInfo.description }}
          template(#bottom-right)
            div(v-if="['Ongoing', 'Paused'].includes(experimentInfo.phase)") Total runtime: #[meta-duration(:duration="experimentInfo.totalTimeOngoing" showAs="humanizedSentenceCase")]
            div(v-else) Not started yet
            div #[meta-abbreviate-number(:value="experimentInfo.totalPlayerCount")] #[meta-plural-label(:value="experimentInfo.totalPlayerCount" label="participant" hideCount)]
            MTextButton(
              :to="`/experiments/${experimentInfo.experimentId}`"
              data-testid="view-experiment"
              ) View experiment

  meta-raw-data(
    :kvPair="experimentInfos"
    name="experimentInfos"
    )
</template>

<script lang="ts" setup>
import { DateTime, Duration } from 'luxon'
import { computed } from 'vue'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import {
  MBadge,
  MListItem,
  MPageOverviewCard,
  MSingleColumnLayout,
  MTextButton,
  MViewContainer,
} from '@metaplay/meta-ui-next'
import type { MViewContainerAlert } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { parseDotnetTimeSpanToLuxon } from '../coreUtils'
import { getAllExperimentsSubscriptionOptions } from '../subscription_options/experiments'

// Get the data.
const { data: allExperimentsData, error: allExperimentsError } = useSubscription(getAllExperimentsSubscriptionOptions())

// Massage the data.
const experimentInfos = computed(() => allExperimentsData.value?.experiments)
const decoratedExperimentInfos = computed((): any[] => {
  return experimentInfos.value.map((experimentInfo: any) => ({
    ...experimentInfo,
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    phaseInfo: getExperimentPhaseInfoById(experimentInfo.experimentId),
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    totalTimeOngoing: getTotalTimeOngoingById(experimentInfo.experimentId),
  }))
})
const runningExperimentInfos = computed(() =>
  allExperimentsData.value?.experiments.filter((x: any) => x.phase === 'Ongoing')
)

// Alerts.
const headerAlerts = computed(() => {
  const alerts: MViewContainerAlert[] = []

  // A lot of combinations.
  if (allExperimentsData.value?.combinations.exceedsThreshold === true) {
    alerts.push({
      title: 'High number of combinations',
      variant: 'warning',
      message: `You currently have ${runningExperimentInfos.value.length} experiments running with a total of ${allExperimentsData.value.combinations.currentCombinations} possible combinations. This may cause a high memory use on your game servers. Consider pausing or concluding some experiments.`,
    })
  }

  return alerts
})

// Utility functions.
function getExperimentPhaseInfoById(experimentId: string): {
  id: string
  displayName: string
  titleVariant: string
  sortOrder: number
} {
  const phaseInfos = [
    {
      id: 'Testing',
      displayName: 'Testing',
      titleVariant: 'primary',
      sortOrder: 1,
    },
    {
      id: 'Ongoing',
      displayName: 'Active',
      titleVariant: 'success',
      sortOrder: 2,
    },
    {
      id: 'Paused',
      displayName: 'Paused',
      titleVariant: 'warning',
      sortOrder: 3,
    },
    {
      id: 'Concluded',
      displayName: 'Concluded',
      titleVariant: 'neutral',
      sortOrder: 4,
    },
  ]
  const phase = String(getExperimentInfoById(experimentId).phase)
  const phaseTitle = phaseInfos.find((phaseInfo) => phaseInfo.id === phase)
  if (!phaseTitle) {
    throw new Error(`Unknown phase: ${phase}`)
  }
  return phaseTitle
}

function getExperimentInfoById(experimentId: string): any {
  return experimentInfos.value.find((x: any) => x.experimentId === experimentId)
}

function getTotalTimeOngoingById(experimentId: string): Duration | null {
  const info = getExperimentInfoById(experimentId)
  let duration: Duration | null = null
  if (info.ongoingDurationBeforeCurrentSpan !== null) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    duration = parseDotnetTimeSpanToLuxon(info.ongoingDurationBeforeCurrentSpan)
  }
  if (info.phase === 'Ongoing') {
    duration = duration ?? Duration.fromMillis(0)
    duration = duration.plus(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      DateTime.now().diff(DateTime.fromISO(info.phaseStartedAt))
    )
  }
  return duration
}

// Sorting.
const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Phase', 'phaseInfo.sortOrder', MetaListSortDirection.Ascending),
  new MetaListSortOption('Phase', 'phaseInfo.sortOrder', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'experimentId', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'experimentId', MetaListSortDirection.Descending),
  new MetaListSortOption('Total player count', 'totalPlayerCount', MetaListSortDirection.Ascending),
  new MetaListSortOption('Total player count', 'totalPlayerCount', MetaListSortDirection.Descending),
]

// Filtering based on the possible experiment phases.
const filterSets = [
  new MetaListFilterSet('phase', [
    new MetaListFilterOption('Testing', (x: any) => x.phaseInfo.id === 'Testing', true),
    new MetaListFilterOption('Active', (x: any) => x.phaseInfo.id === 'Ongoing', true),
    new MetaListFilterOption('Paused', (x: any) => x.phaseInfo.id === 'Paused', true),
    new MetaListFilterOption('Concluded', (x: any) => x.phaseInfo.id === 'Concluded'),
  ]),
]
</script>

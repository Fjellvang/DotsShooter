<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="All Leagues"
  :itemList="allLeaguesData"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :filterSets="filterSets"
  icon="trophy"
  emptyMessage="No leagues to list!"
  data-testid="league-list-card"
  )
  template(#item-card="{ item: leagueData }")
    MListItem {{ leagueData.displayName || 'League name not available.' }}
      template(#badge)
        MBadge(
          v-if="leagueData.enabled"
          variant="success"
          ) Enabled
        MBadge(
          v-else
          tooltip="This league is not enabled for this deployment. Contact your game team to set it up."
          ) Disabled
        MBadge(:variant="getPhaseVariant(leagueData.scheduleStatus.currentPhase.phase)") {{ schedulePhaseDisplayString(leagueData.scheduleStatus.currentPhase.phase) }}
      template(#top-right) {{ leagueData.leagueId }}
      template(#bottom-left) {{ leagueData.description || 'There is currently no description for this league.' }}
      template(#bottom-right)
        MTextButton(
          :to="`/leagues/${leagueData.leagueId}`"
          data-testid="view-league"
          ) View league
</template>

<script lang="ts" setup>
import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { schedulePhaseDisplayString } from '../../coreUtils'
import { getPhaseVariant } from '../../leagueUtils'
import { getAllLeaguesSubscriptionOptions } from '../../subscription_options/leagues'

const { data: allLeaguesData } = useSubscription<any[] | undefined>(getAllLeaguesSubscriptionOptions())

const searchFields = ['leagueId', 'displayName', 'description']

/**
 * Generate a sort order based on a league's phase.
 * @param item League data.
 */
function phaseSortOrder(item: any): number {
  // This is the sorting order that we want to apply to the phase types.
  const sortOrder = ['Preview', 'Active', 'EndingSoon']

  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const index = sortOrder.indexOf(item.scheduleStatus.currentPhase.phase)
  if (index !== -1) {
    return index
  } else {
    // For all other phases, we want them to be sorted last.
    return sortOrder.length + 1
  }
}

const sortOptions = [
  new MetaListSortOption('Phase', phaseSortOrder, MetaListSortDirection.Ascending),
  new MetaListSortOption('Phase', phaseSortOrder, MetaListSortDirection.Descending),
]

const filterSets = [
  new MetaListFilterSet('enabled', [
    new MetaListFilterOption('Enabled', (leagueData: any) => leagueData.enabled === true, false),
    new MetaListFilterOption('Disabled', (leagueData: any) => leagueData.enabled === false, false),
  ]),
]
</script>

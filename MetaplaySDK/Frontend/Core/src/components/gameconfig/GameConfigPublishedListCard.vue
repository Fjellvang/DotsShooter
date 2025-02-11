<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Publish History"
  :itemList="allGameConfigsData"
  :getItemKey="getItemKey"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  icon="table"
  data-testid="game-config-published-list-card"
  )
  template(#item-card="{ item: gameConfig }")
    MListItem
      | {{ gameConfig.name || 'No name available' }}

      template(#badge)
        MBadge(
          v-if="gameConfig.isActive"
          variant="success"
          ) Active
        MBadge(
          v-if="gameConfig.isArchived"
          variant="neutral"
          ) Archived
        MBadge(
          v-if="gameConfig.bestEffortStatus === 'Failed'"
          variant="danger"
          ) Failed

      template(#top-right)
        div(v-if="gameConfig.isActive")
          div(v-if="gameConfig.publishedAt != null") Published #[meta-time(:date="gameConfig.publishedAt")]
          div(v-else)
            MTooltip(content="The date was not recorded as it was published in an earlier version.") Date unavailable
        div(v-else)
          div(v-if="gameConfig.unpublishedAt != null") Unpublished #[meta-time(:date="gameConfig.unpublishedAt")]
          div(v-else)
            MTooltip(content="The date was not recorded as it was unpublished in an earlier version.") Date unavailable

      template(#bottom-left)
        div(
          v-if="gameConfig.buildReportSummary?.totalLogLevelCounts.Warning"
          class="tw-text-orange-500"
          ) {{ maybePluralString(gameConfig.buildReportSummary.totalLogLevelCounts.Warning, 'build warning') }}
        div {{ gameConfig.description || 'No description available' }}

      template(#bottom-right)
        div
          MTextButton(
            :to="`gameConfigs/diff?newRoot=${gameConfig.id}`"
            :disabled-tooltip="gameConfig.isActive ? 'The active game config cannot be compared to itself.' : undefined"
            data-testid="diff-config"
            ) Diff to active
        div
          MTextButton(
            :to="getDetailPagePath(gameConfig.id)"
            data-testid="view-config"
            ) View config
        game-config-action-publish(
          :gameConfigId="gameConfig.id"
          text-button
          )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTooltip, MTextButton } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import GameConfigActionPublish from '../../components/gameconfig/GameConfigActionPublish.vue'
import type { MinimalGameConfigInfo } from '../../gameConfigServerTypes'
import { getAllGameConfigsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const route = useRoute()
const detailsRoute = computed(() => route.path)

/**
 * Subscription to get all game configs.
 */
const { data: allGameConfigsDataRaw } = useSubscription(getAllGameConfigsSubscriptionOptions())

/**
 * Filtered list containing only game configs that have been published.
 */
const allGameConfigsData = computed(() => {
  if (allGameConfigsDataRaw.value !== undefined) {
    return (
      allGameConfigsDataRaw.value
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter((x: MinimalGameConfigInfo) => x.publishedAt !== null || x.unpublishedAt !== null || x.isActive)
    )
  } else {
    return undefined
  }
})

/**
 * Get detail page path by joining it to the current path,
 * but take into account if there's already a trailing slash.
 * \todo Do a general fix with router and get rid of this.
 */
function getDetailPagePath(gameConfigId: string): string {
  const path = detailsRoute.value
  const maybeSlash = path.endsWith('/') ? '' : '/'
  return path + maybeSlash + gameConfigId
}

/**
 * Sort key for the `MetaListCard`.
 * @param gameConfig Config data.
 */
function getItemKey(gameConfig: MinimalGameConfigInfo): string {
  return gameConfig.id
}

/**
 * Sort function for the `MetaListCard` to sort by published time (for the active configuration) or unpublished time
 * (for all other configurations).
 * @param gameConfig Config data.
 */
function getPublishedSortKey(gameConfig: MinimalGameConfigInfo): string | number {
  if (gameConfig.isActive) {
    // Force active to the top by giving it a time far into the future, when man may or may not still be alive.
    return '2525-12-25T00:00:00.0000000Z'
  } else {
    // Handle configs that don't have a published time by defaulting to epoch time.
    return gameConfig.unpublishedAt ?? '1970-01-01T00:00:00.0000000Z'
  }
}

/**
 * Sorting options for game configs.
 */
const defaultSortOption = 1
const sortOptions = [
  new MetaListSortOption('Time', getPublishedSortKey, MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', getPublishedSortKey, MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Descending),
]

/**
 * Search fields for game configs.
 */
const searchFields = ['id', 'name', 'description']

/**
 * Filtering options for game configs.
 */
const filterSets = [
  new MetaListFilterSet('archived', [
    new MetaListFilterOption('Archived', (x: any) => x.isArchived),
    new MetaListFilterOption('Not archived', (x: any) => !x.isArchived, true),
  ]),
]
</script>

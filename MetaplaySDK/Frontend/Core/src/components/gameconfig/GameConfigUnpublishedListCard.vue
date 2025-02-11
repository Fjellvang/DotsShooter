<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Unpublished Configs"
  :itemList="allGameConfigsData"
  :getItemKey="getItemKey"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  icon="table"
  data-testid="game-config-unpublished-list-card"
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
        div(
          v-if="gameConfig.bestEffortStatus !== 'Building'"
          class="tw-space-x-1"
          )
          span Built #[meta-time(:date="gameConfig.buildStartedAt")]
          game-config-action-archive(
            :gameConfigId="gameConfig.id"
            trigger-style="icon"
            )
        div(v-else)
          MBadge(variant="primary") Building...

      template(#bottom-left)
        div(
          v-if="gameConfig.buildReportSummary?.totalLogLevelCounts.Error"
          class="tw-text-red-500"
          ) {{ maybePluralString(gameConfig.buildReportSummary.totalLogLevelCounts.Error, 'build error') }}
        div(
          v-else-if="gameConfig.buildReportSummary?.totalLogLevelCounts.Warning"
          class="tw-text-orange-500"
          ) {{ maybePluralString(gameConfig.buildReportSummary.totalLogLevelCounts.Warning, 'build warning') }}
        div {{ gameConfig.description || 'No description available' }}

      template(#bottom-right)
        span(v-if="gameConfig.bestEffortStatus !== 'Building'")
          div
            MTextButton(
              :to="`gameConfigs/diff?newRoot=${gameConfig.id}`"
              :disabled-tooltip="gameConfig.bestEffortStatus === 'Failed' ? 'Unable to diff because this game config failed to build.' : undefined"
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

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import GameConfigActionArchive from '../../components/gameconfig/GameConfigActionArchive.vue'
import GameConfigActionPublish from '../../components/gameconfig/GameConfigActionPublish.vue'
import type { MinimalGameConfigInfo } from '../../gameConfigServerTypes'
import { getAllGameConfigsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const gameServerApi = useGameServerApi()
const route = useRoute()
const detailsRoute = computed(() => route.path)

/**
 * Subscription to get all game configs.
 */
const { data: allGameConfigsDataRaw } = useSubscription(getAllGameConfigsSubscriptionOptions())

/**
 * Filtered list containing only game configs that have not already been published.
 */
const allGameConfigsData = computed(() => {
  if (allGameConfigsDataRaw.value !== undefined) {
    return (
      allGameConfigsDataRaw.value
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter((x: MinimalGameConfigInfo) => !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive))
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
 * Sorting options for game configs.
 */
const defaultSortOption = 1
const sortOptions = [
  new MetaListSortOption('Build Time', 'buildStartedAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Build Time', 'buildStartedAt', MetaListSortDirection.Descending),
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
  new MetaListFilterSet('status', [
    new MetaListFilterOption('Building', (x: any) => x.bestEffortStatus === 'Building'),
    new MetaListFilterOption('Success', (x: any) => x.bestEffortStatus === 'Success'),
    new MetaListFilterOption('Failed', (x: any) => x.bestEffortStatus === 'Failed'),
  ]),
  new MetaListFilterSet('archived', [
    new MetaListFilterOption('Archived', (x: any) => x.isArchived),
    new MetaListFilterOption('Not archived', (x: any) => !x.isArchived, true),
  ]),
]
</script>

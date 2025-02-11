<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Publish History"
  :itemList="allLocalizationsData"
  :getItemKey="getItemKey"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  icon="language"
  data-testid="localization-published-list-card"
  )
  template(#item-card="{ item: localization }")
    MListItem
      | {{ localization.name || 'No name available' }}

      template(#badge)
        MBadge(
          v-if="localization.isActive"
          variant="success"
          ) Active
        MBadge(
          v-if="localization.isArchived"
          variant="neutral"
          ) Archived
        MBadge(
          v-if="localization.bestEffortStatus === 'Failed'"
          variant="danger"
          ) Failed

      template(#top-right)
        div(v-if="localization.isActive")
          div(v-if="localization.publishedAt != null") Published #[meta-time(:date="localization.publishedAt")]
          div(v-else)
            MTooltip(content="The date was not recorded as it was published in an earlier version.") Date unavailable
        div(v-else)
          div(v-if="localization.unpublishedAt != null") Unpublished #[meta-time(:date="localization.unpublishedAt")]
          div(v-else)
            MTooltip(content="The date was not recorded as it was unpublished in an earlier version.") Date unavailable

      template(#bottom-right)
        div
          MTextButton(
            :to="`localizations/diff?newRoot=${localization.id}`"
            :disabled-tooltip="localization.isActive ? 'The active localization cannot be compared to itself.' : undefined"
            data-testid="diff-localization"
            ) Diff to active
        div
          MTextButton(
            :to="getDetailPagePath(localization.id)"
            data-testid="view-localization"
            ) View localization
        localization-action-publish(
          :localizationId="localization.id"
          text-button
          )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MBadge, MListItem, MTextButton, MTooltip } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import LocalizationActionPublish from '../../components/localization/LocalizationActionPublish.vue'
import type { MinimalLocalizationInfo } from '../../localizationServerTypes'
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'

const route = useRoute()
const detailsRoute = computed(() => route.path)

/**
 * Subscription to get all localizations.
 */
const { data: allLocalizationsDataRaw } = useSubscription(getAllLocalizationsSubscriptionOptions())

/**
 * Filtered list containing only localizations that have been published.
 */
const allLocalizationsData = computed(() => {
  if (allLocalizationsDataRaw.value !== undefined) {
    return (
      allLocalizationsDataRaw.value
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter((x: MinimalLocalizationInfo) => x.publishedAt !== null || x.unpublishedAt !== null || x.isActive)
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
function getDetailPagePath(localizationId: string): string {
  const path = detailsRoute.value
  const maybeSlash = path.endsWith('/') ? '' : '/'
  return path + maybeSlash + localizationId
}

/**
 * Sort key for the `MetaListCard`.
 * @param localization Localization data.
 */
function getItemKey(localization: MinimalLocalizationInfo): string {
  return localization.id
}

/**
 * Sort function for the `MetaListCard` to sort by published time (for the active localization) or unpublished time
 * (for all other localizations).
 * @param localization Localization data.
 */
function getPublishedSortKey(localization: MinimalLocalizationInfo): string | number {
  if (localization.isActive) {
    // Force active to the top by giving it a time far into the future, when man may or may not still be alive.
    return '2525-12-25T00:00:00.0000000Z'
  } else {
    // Handle localizations that don't have a published time by defaulting to epoch time.
    return localization.unpublishedAt ?? '1970-01-01T00:00:00.0000000Z'
  }
}

/**
 * Sorting options for localizations.
 */
const defaultSortOption = 1
const sortOptions = [
  new MetaListSortOption('Time', getPublishedSortKey, MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', getPublishedSortKey, MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Descending),
]

/**
 * Search fields for localizations.
 */
const searchFields = ['id', 'name', 'description']

/**
 * Filtering options for localizations.
 */
const filterSets = [
  new MetaListFilterSet('archived', [
    new MetaListFilterOption('Archived', (x: any) => x.isArchived),
    new MetaListFilterOption('Not archived', (x: any) => !x.isArchived, true),
  ]),
]
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Unpublished Localizations"
  :itemList="allLocalizationsData"
  :getItemKey="getItemKey"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  icon="language"
  data-testid="localization-unpublished-list-card"
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
        div(
          v-if="localization.bestEffortStatus !== 'Building'"
          class="tw-space-x-1"
          )
          span Built #[meta-time(:date="localization.buildStartedAt")]
          localization-action-archive(
            :localizationId="localization.id"
            trigger-style="icon"
            )
        div(v-else)
          MBadge(variant="primary") Building...

      template(#bottom-left)
        div {{ localization.description || 'No description available' }}

      template(#bottom-right)
        span(v-if="localization.bestEffortStatus !== 'Building'")
          div
            MTextButton(
              :to="`localizations/diff?newRoot=${localization.id}`"
              :disabled-tooltip="localization.bestEffortStatus === 'Failed' ? 'Unable to diff because this localization failed to build.' : undefined"
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
import { MBadge, MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import LocalizationActionArchive from '../../components/localization/LocalizationActionArchive.vue'
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
 * Filtered list containing only localizations that have not already been published.
 */
const allLocalizationsData = computed(() => {
  if (allLocalizationsDataRaw.value !== undefined) {
    return (
      allLocalizationsDataRaw.value
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter((x: MinimalLocalizationInfo) => !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive))
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
 * Sorting options for localizations.
 */
const defaultSortOption = 1
const sortOptions = [
  new MetaListSortOption('Build Time', 'buildStartedAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Build Time', 'buildStartedAt', MetaListSortDirection.Descending),
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

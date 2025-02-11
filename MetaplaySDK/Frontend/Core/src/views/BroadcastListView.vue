<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.broadcasts.view"
  :is-loading="!allBroadcastsData"
  :error="allBroadcastsError"
  )
  template(#overview)
    MPageOverviewCard(
      title="Create a New Broadcast"
      data-testid="broadcast-list-overview-card"
      )
      template(#buttons)
        broadcast-form-button(
          button-text="Create New"
          button-icon="plus-square"
          class="mr-2 tw-mt-1"
          @refresh="allBroadcastsRefresh"
          data-testid="create-new-broadcast"
          )

      p(class="tw-mb-1") Broadcasts are scheduled in-game mail campaigns.
      div(class="small text-muted") Players who match the targeting criteria of an active broadcast will get an in-game mail generated into their inbox when they login. A player can receive every broadcast only once to prevent duplicate mails.

  MSingleColumnLayout
    meta-list-card#broadcasts-list(
      title="All broadcasts"
      :getItemKey="getBroadcastId"
      :itemList="decoratedBroadcasts"
      :searchFields="['params.id', 'params.name']"
      :filterSets="filterSets"
      :sortOptions="sortOptions"
      :defaultSortOption="1"
      :pageSize="20"
      icon="broadcast-tower"
      data-testid="all-broadcasts-list-card"
      )
      template(#item-card="slot")
        MListItem
          | {{ slot.item.params.name }} #[span(class="tw-text-xs+ tw-font-normal tw-text-neutral-500") for {{ audienceSummary(slot.item.params) }}]
          template(#top-right)
            MBadge(:variant="slot.item.decoration.variant")
              template(#icon)
                fa-icon(:icon="slot.item.decoration.icon")
              | {{ slot.item.decoration.status }}

          template(#bottom-left)
            div(v-if="slot.item.decoration.status === 'Scheduled'") Start time: #[meta-time(:date="slot.item.params.startAt")]
            div(v-else) End time: #[meta-time(:date="slot.item.params.endAt")]

          template(#bottom-right)
            div Received by #[meta-plural-label(:value="slot.item.stats.receivedCount" label="player")]
            MTextButton(
              :to="getDetailPagePath(slot.item.params.id)"
              data-testid="view-broadcast"
              ) View broadcast

  meta-raw-data(
    :kvPair="decoratedBroadcasts"
    name="decoratedBroadcasts"
    )/
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaListSortOption, MetaListSortDirection, MetaListFilterSet, MetaListFilterOption } from '@metaplay/meta-ui'
import {
  MBadge,
  MListItem,
  MPageOverviewCard,
  MSingleColumnLayout,
  MTextButton,
  MViewContainer,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import BroadcastFormButton from '../components/mails/BroadcastFormButton.vue'
import { getAllBroadcastsSubscriptionOptions } from '../subscription_options/broadcasts'

const route = useRoute()
const detailsRoute = computed(() => route.path)

/**
 * Subscribe to broadcast data.
 * Unused variable broacastsRefresh, a TODO that was forgotten?
 */
const {
  data: allBroadcastsData,
  error: allBroadcastsError,
  refresh: allBroadcastsRefresh,
} = useSubscription<any[]>(getAllBroadcastsSubscriptionOptions())

/**
 * List of broadcasts to be displayed.
 */
const decoratedBroadcasts = computed(() => {
  if (!allBroadcastsData.value) return undefined
  const now = DateTime.now()
  return allBroadcastsData.value.map((broadcast) => {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const startAtDate = DateTime.fromISO(broadcast.params.startAt)
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const endAtDate = DateTime.fromISO(broadcast.params.endAt)
    if (endAtDate > now && startAtDate < now) {
      return {
        ...broadcast,
        decoration: {
          status: 'Active',
          variant: 'success',
          icon: 'broadcast-tower',
        },
      }
    } else if (endAtDate > now) {
      return {
        ...broadcast,
        decoration: {
          status: 'Scheduled',
          variant: 'primary',
          icon: 'calendar-alt',
        },
      }
    } else {
      return {
        ...broadcast,
        decoration: {
          status: 'Expired',
          variant: 'neutral',
          icon: 'times',
        },
      }
    }
  })
})

/**
 * List of sort options to be passed to the meta-list-card component.
 */
const sortOptions = [
  new MetaListSortOption('Time', 'params.startAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', 'params.startAt', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'params.name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'params.name', MetaListSortDirection.Descending),
  new MetaListSortOption('Received by', 'stats.receivedCount', MetaListSortDirection.Ascending),
  new MetaListSortOption('Received by', 'stats.receivedCount', MetaListSortDirection.Descending),
]

/**
 * List of filter sets to be passed to the meta-list-card component.
 */
const filterSets = [
  new MetaListFilterSet('status', [
    new MetaListFilterOption('Scheduled', (x: any) => x.decoration.status === 'Scheduled'),
    new MetaListFilterOption('Active', (x: any) => x.decoration.status === 'Active'),
    new MetaListFilterOption('Expired', (x: any) => x.decoration.status === 'Expired'),
  ]),
]

/**
 * Retrieves the Id of the respective broadcast.
 * @param broadcast broadcast data.
 */
function getBroadcastId(broadcast: any): string {
  return broadcast.params.id
}

/**
 * Get detail page path by joining it to the current path,
 * but take into account if there's already a trailing slash.
 * \todo Do a general fix with router and get rid of this.
 */
function getDetailPagePath(detailId: string): string {
  const path = detailsRoute.value
  const maybeSlash = path.endsWith('/') ? '' : '/'
  return path + maybeSlash + detailId
}

/**
 * Summary estimation of number of players targeted in the campaign.
 */
function audienceSummary(broadcastParams: any): string | undefined {
  if (!broadcastParams.isTargeted) {
    return 'everyone'
  } else if (broadcastParams.targetPlayers?.length && broadcastParams.targetCondition) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return `Segment based and ${maybePluralString(broadcastParams.targetPlayers.length, 'player')}`
  } else if (broadcastParams.targetPlayers?.length) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return maybePluralString(broadcastParams.targetPlayers.length, 'player')
  } else if (broadcastParams.targetCondition) {
    return 'Segment based'
  }
}
</script>

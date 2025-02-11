<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  :itemList="allMatchmakersData"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  title="Async Matchmakers"
  emptyMessage="No async matchmakers to list!"
  data-testid="async-matchmakers-list-card"
  )
  template(#item-card="{ item: matchmaker }")
    MListItem {{ matchmaker.data.name }}
      template(#top-right) {{ matchmaker.id }}
      template(#bottom-left) {{ matchmaker.data.description }}
      template(#bottom-right)
        div {{ matchmaker.data.playersInBuckets }} participants
        div {{ Math.round(matchmaker.data.bucketsOverallFillPercentage * 10000) / 100 }}% full
        MTextButton(
          :to="`/matchmakers/${matchmaker.id}`"
          data-testid="view-matchmaker"
          ) View matchmaker
</template>

<script lang="ts" setup>
import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllMatchmakersSubscriptionOptions } from '../../subscription_options/matchmaking'

/**
 * Subscribe to all matchmakers data.
 */
const { data: allMatchmakersData } = useSubscription<any[] | undefined>(getAllMatchmakersSubscriptionOptions())

// Search, sort
const searchFields = ['id', 'data.name', 'data.description']
const sortOptions = [
  new MetaListSortOption('Name', 'data.name', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'data.name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Participants', 'data.playersInBuckets', MetaListSortDirection.Ascending),
  new MetaListSortOption('Participants', 'data.playersInBuckets', MetaListSortDirection.Descending),
  new MetaListSortOption('Fill rate', 'data.bucketsOverallFillPercentage', MetaListSortDirection.Ascending),
  new MetaListSortOption('Fill rate', 'data.bucketsOverallFillPercentage', MetaListSortDirection.Descending),
]
</script>

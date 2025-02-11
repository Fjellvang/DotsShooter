<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="All Developer Players"
  :itemList="developerPlayersData"
  :searchFields="['name', 'id']"
  :sortOptions="sortOptions"
  icon="user"
  emptyMessage="No developer players. You have not marked any players as developers."
  permission="api.players.view_developers"
  style="margin-bottom: 2rem"
  data-testid="developer-players-list"
  )
  template(#item-card="{ item: developerPlayer }")
    MListItem
      span(v-if="developerPlayer.deserializedSuccessfully") {{ developerPlayer.name }}
      span(
        v-else
        class="tw-text-red-500"
        ) Player deserialization failed

      template(#top-right) {{ developerPlayer.id }}
      template(#bottom-left)
        span(v-if="developerPlayer.deserializedSuccessfully") Last login on #[meta-time(:date="developerPlayer.lastLoginAt || ''" showAs="date")]
        span(v-else) Last login unknown

      template(#bottom-right)
        MTextButton(:to="`/players/${developerPlayer.id}`") View player
</template>

<script lang="ts" setup>
import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getDeveloperPlayersSubscriptionOptions } from '../../subscription_options/players'

const { data: developerPlayersData } = useSubscription(getDeveloperPlayersSubscriptionOptions())

const sortOptions = [
  new MetaListSortOption('Default', 'deserializedSuccessfully', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Descending),
  new MetaListSortOption('Last login', 'lastLoginAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Last login', 'lastLoginAt', MetaListSortDirection.Descending),
]
</script>

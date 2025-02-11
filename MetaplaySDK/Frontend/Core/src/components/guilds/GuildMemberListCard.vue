<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card#guild-member-list-card(
  title="Members"
  icon="users"
  :itemList="members"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  emptyMessage="This guild is empty!"
  )
  template(#item-card="{ item: guildMember }")
    //- TODO: replace this with a data driven, generic representation of guild member identifiers
    MListItem
      //- Online status
      span(class="tw-mr-1")
        fa-icon(
          v-if="guildMember.isOnline"
          size="xs"
          icon="circle"
          class="text-success"
          )
        fa-icon(
          v-else-if="hasRecentlyLoggedIn(guildMember.lastOnlineAt)"
          size="xs"
          :icon="['far', 'circle']"
          class="text-success"
          )
        fa-icon(
          v-else
          size="xs"
          :icon="['far', 'circle']"
          class="text-dark"
          )
      //- Player name and rank
      span(class="tw-mr-1") {{ guildMember.displayName || 'n/a' }}
      span(:class="guildMember.role === 'Leader' ? 'tw-text-orange-400 tw-text-sm' : 'tw-text-neutral-500 tw-text-sm'") {{ guildRoleDisplayString(guildMember.role) }}

      //- Game specific info. Feel free to replace with whatever is most relevant in your game!
      template(#bottom-left)
        span Poked: {{ guildMember.numTimesPoked }} times |
        span(class="tw-ml-1") Vanity points: {{ guildMember.numVanityPoints }} |
        span(class="tw-ml-1") Vanity rank: {{ guildMember.numVanityRanksConsumed }}

      template(#top-right): MTextButton(:to="`/players/${guildMember.id}`") View player
</template>

<script lang="ts" setup>
import { cloneDeep } from 'lodash-es'
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { guildRoleDisplayString } from '../../coreUtils'
import { getSingleGuildSubscriptionOptions } from '../../subscription_options/guilds'

const props = defineProps<{
  guildId: string
}>()

const { data: guildData } = useSubscription(getSingleGuildSubscriptionOptions(props.guildId))

const searchFields = ['displayName', 'id', 'role']

const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Role', 'role', MetaListSortDirection.Ascending),
  new MetaListSortOption('Role', 'role', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'displayName', MetaListSortDirection.Descending),
]

// TODO: consider changing the API response to make this transformation unnecessary
const members = computed(() => {
  const list = []
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  for (const key of Object.keys(guildData.value.model.members)) {
    const payload = cloneDeep(guildData.value.model.members[key])
    payload.id = key
    list.push(payload)
  }
  return list
})

function hasRecentlyLoggedIn(isoDateTime: string): boolean {
  return Math.abs(DateTime.fromISO(isoDateTime).diffNow('hours').hours) < 12
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  id="guild-member-list-card"
  title="Invites"
  icon="people-arrows"
  tooltip="A list of invites that are currently active for this guild."
  :itemList="invites"
  :searchFields="searchFields"
  :sortOptions="sortOptions"
  :filterSets="filterSets"
  emptyMessage="This guild has no active invites!"
  )
  template(#item-card="slot")
    MListItem {{ slot.item.inviteCode }}
      template(#top-right)
        MTextButton(:to="`/players/${slot.item.playerId}`") View player
      template(#bottom-left)
        span Used: {{ slot.item.numTimesUsed }} #[span(v-if="slot.item.numMaxUsages > 0") /{{ slot.item.numMaxUsages }}]
      template(#bottom-right)
        div Created: #[meta-time(:date="slot.item.createdAt")]
        div Expires: #[meta-time(:date="slot.item.expiresAt")]
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { parseDotnetTimeSpanToLuxon } from '../../coreUtils'
import { getSingleGuildSubscriptionOptions } from '../../subscription_options/guilds'

const props = defineProps<{
  guildId: string
}>()

const { data: guildData } = useSubscription(getSingleGuildSubscriptionOptions(props.guildId))

const searchFields = ['inviteCode', 'playerId']

const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Player', 'playerId', MetaListSortDirection.Ascending),
  new MetaListSortOption('Player', 'playerId', MetaListSortDirection.Descending),
]

const invites = computed(() => {
  const list = []
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  for (const member of Object.values(guildData.value.model.members) as any) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    for (const invite of Object.values(member.invites) as any) {
      list.push({
        ...invite,
        playerId: member.id,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        expiresAt: DateTime.fromISO(invite.createdAt).plus(
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          parseDotnetTimeSpanToLuxon(invite.expiresAfter)
        ),
      })
    }
  }
  return list
})

const filterSets = [
  new MetaListFilterSet('usage', [
    new MetaListFilterOption('Unused', (x: any) => x.numTimesUsed === 0),
    new MetaListFilterOption('Part used', (x: any) => x.numTimesUsed > 0 && x.numTimesUsed < x.numMaxUsages),
    new MetaListFilterOption('Fully used', (x: any) => x.numTimesUsed === x.numMaxUsages),
  ]),
]
</script>

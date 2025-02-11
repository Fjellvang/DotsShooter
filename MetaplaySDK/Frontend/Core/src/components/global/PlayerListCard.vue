<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  :title="title"
  icon="user"
  :emptyMessage="emptyMessage"
  :searchFields="['name', 'id']"
  :itemList="playersWithInfo"
  data-testid="player-list-card"
  )
  template(#item-card="{ item: option }")
    MListItem(
      v-if="!option?.deserializedSuccessfully"
      class="text-danger"
      )
      | 🛑 {{ option?.name }}
      template(#top-right) {{ option?.id }}
      template(#bottom-left) Failed to load player!
      template(#bottom-right)
        MTextButton(:to="`/players/${option.id}`") View player

    MListItem(
      v-else-if="option?.deletionStatus.startsWith('Deleted')"
      class="text-danger"
      )
      | ☠️ {{ option.name }}
      template(#top-right) {{ option.id }}
      template(#bottom-left) Player deleted
      template(#bottom-right)
        MTextButton(:to="`/players/${option.id}`") View player

    MListItem(v-else) {{ option?.name }}
      template(#badge)
        div(class="tw-flex")
          MTooltip(
            v-if="option?.totalIapSpend > 0"
            :content="'Total IAP spend: $' + option.totalIapSpend.toFixed(2)"
            noUnderline
            )
            fa-icon(
              icon="money-check-alt"
              size="sm"
              class="text-muted"
              )
          MTooltip(
            v-if="option?.isDeveloper"
            content="This player is a developer."
            noUnderline
            )
            fa-icon(
              icon="user-astronaut"
              size="sm"
              class="text-muted"
              )
      template(#top-right) {{ option?.id }}
      template(#bottom-left) Level {{ option?.level }}
      template(#bottom-right)
        div Joined #[meta-time(:date="option?.createdAt" showAs="date")]
        MTextButton(:to="`/players/${option.id}`") View player
</template>

<script lang="ts" setup>
import { isEqual } from 'lodash-es'
import { ref, watch } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { BulkListInfo, PlayerListItem } from '@metaplay/meta-ui'
import { MListItem, MTextButton, MTooltip } from '@metaplay/meta-ui-next'

const gameServerApi = useGameServerApi()

const props = withDefaults(
  defineProps<{
    /**
     * The list of target players' IDs.
     */
    playerIds: string[]
    /**
     * Optional: The title of this card. Defaults to "Players".
     */
    title?: string
    /**
     * Optional: The message to be displayed when the list is empty.
     */
    emptyMessage?: string
  }>(),
  {
    title: 'Players',
    emptyMessage: 'No players.',
  }
)

/**
 * The list of target players' data to be displayed on this card.
 */
const playersWithInfo = ref<PlayerListItem[]>()

/**
 * Watch `playerIds` prop and fetch target player(s) data from the game server when it changes.
 */
watch(
  () => props.playerIds,
  async (newValue, oldValue) => {
    if (!isEqual(newValue, oldValue)) {
      await fetchPlayerInfos()
    }
  },
  { deep: true, immediate: true }
)

/**
 * Fetches target player(s) data from the game server.
 */
async function fetchPlayerInfos(): Promise<void> {
  const response = await gameServerApi.post('players/bulkValidate', {
    PlayerIds: props.playerIds,
  })
  playersWithInfo.value = (response.data as BulkListInfo[])
    .filter((player) => player.validId)
    .map((player) => player.playerData)
}
</script>

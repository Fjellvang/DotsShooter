<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- TODO:
  - Infinite scroll?
  - Nicer "no results" message?

meta-input-select(
  :value="value"
  :placeholder="`Search for a player...`"
  :options="search"
  no-clear
  @input="$emit('input', $event)"
  data-testid="input-player-select"
  )
  template(#selectedOption="{ option }")
    div {{ option?.name }}

  template(#option="{ option }")
    MListItem(
      v-if="!option?.isInitialized"
      class="text-muted !tw-px-0 !tw-py-0"
      )
      | 🚫 Uninitialized
      template(#top-right) {{ option?.id }}
      template(#bottom-left) Player not initialized!

    MListItem(
      v-else-if="!option?.deserializedSuccessfully"
      class="text-danger !tw-px-0 !tw-py-0"
      )
      | 🛑 {{ option?.name }}
      template(#top-right) {{ option?.id }}
      template(#bottom-left) Failed to load player!

    MListItem(
      v-else-if="option?.deletionStatus.startsWith('Deleted')"
      class="text-danger !tw-px-0 !tw-py-0"
      )
      | ☠️ {{ option.name }}
      template(#top-right) {{ option.id }}
      template(#bottom-left) Player deleted

    MListItem(
      v-else
      class="!tw-px-0 !tw-py-0"
      ) {{ option?.name }}
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
      template(#bottom-right) Joined #[meta-time(:date="option?.createdAt" showAs="date")]
</template>

<script lang="ts" setup>
import { useGameServerApi } from '@metaplay/game-server-api'
import { MTooltip, MListItem } from '@metaplay/meta-ui-next'

import type { MetaInputSelectOption, PlayerListItem } from '../additionalTypes'

const props = defineProps<{
  /**
   * Optional: The currently selected entity.
   */
  value?: PlayerListItem
  /**
   * Optional: Don't allow selection of list of players.
   */
  ignorePlayerIds?: string[]
}>()

defineEmits(['input'])

const gameServerApi = useGameServerApi()

/**
 * Returns a list of players that match the given search term.
 * TODO: This doesn't actually search for IDs!
 * @param query Name or Id of the player we are searching for.
 */
async function search(query?: string): Promise<Array<MetaInputSelectOption<PlayerListItem>>> {
  const res = await gameServerApi.get(`/players/?query=${encodeURIComponent(query ?? '')}`)
  return (res.data as PlayerListItem[]).map((entity) => {
    return {
      id: entity.id,
      value: entity,
      disabled: props.ignorePlayerIds ? props.ignorePlayerIds.includes(entity.id) : false,
    }
  })
}
</script>

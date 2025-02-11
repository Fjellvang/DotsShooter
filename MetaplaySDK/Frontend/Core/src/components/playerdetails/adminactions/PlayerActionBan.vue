<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  MActionModalButton(
    modal-title="Change Banned Status"
    :action="updateBannedStatus"
    :trigger-button-label="playerData.model.isBanned ? 'Un-Ban Player' : 'Ban Player'"
    variant="warning"
    ok-button-label="Save Settings"
    :ok-button-disabled-tooltip="!isStatusChanged ? `Toggle the switch to ${playerData.model.isBanned ? 'un-ban' : 'ban'} this player.` : undefined"
    permission="api.players.ban"
    @show="resetModal"
    data-testid="action-ban-player"
    )
    template(#default)
      div(class="tw-flex tw-justify-between")
        span(class="tw-font-semibold") Player Banned
        MInputSwitch(
          :model-value="isCurrentlyBanned"
          class="tw-relative tw-top-1 tw-mr-3"
          name="isPlayerBanned"
          size="small"
          @update:model-value="isCurrentlyBanned = $event"
          data-testid="player-ban-toggle"
          )
      span(class="tw-text-xs+ tw-text-neutral-500") Banning will disconnect the player and prevent them from logging into the game.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MInputSwitch, MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const { showSuccessNotification } = useNotifications()

const props = defineProps<{
  /**
   * ID of the player to ban.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
const isCurrentlyBanned = ref(false)

const isStatusChanged = computed(() => {
  return isCurrentlyBanned.value !== playerData.value.model.isBanned
})

function resetModal(): void {
  isCurrentlyBanned.value = playerData.value.model.isBanned
}

async function updateBannedStatus(): Promise<void> {
  const isBanned = isCurrentlyBanned.value // \note Copy, because this.isBanned might get modified before toast is shown
  await gameServerApi.post(`/players/${playerData.value.id}/ban`, { isBanned })
  showSuccessNotification(`${playerData.value.model.playerName || 'n/a'} ${isBanned ? 'banned' : 'un-banned'}.`)
  playerRefresh()
}
</script>

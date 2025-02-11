<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Reset Player State"
  :action="resetPlayerState"
  trigger-button-label="Force Reset Player"
  variant="danger"
  ok-button-label="Force Reset Player"
  permission="api.players.reset_player"
  data-testid="action-force-reset-player-state"
  )
  template(#default)
    p Force resetting the player will re-initialize the entire player state, including important things like purchases and connected devices.
    p The authentication records are preserved, so it will be possible for the player to re-attach to the player after reset.
</template>

<script lang="ts" setup>
import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to set as developer.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const { showSuccessNotification } = useNotifications()

async function resetPlayerState(): Promise<void> {
  await gameServerApi.post(`/players/${props.playerId}/forceResetState`)
  showSuccessNotification('Player state force reset to defaults.')
  playerRefresh()
}
</script>

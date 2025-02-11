<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  MActionModalButton(
    modal-title="Manage Developer Status"
    :action="updateDeveloperStatus"
    :trigger-button-label="playerData.model.isDeveloper ? 'Remove Dev Status' : 'Mark as Developer'"
    variant="warning"
    ok-button-label="Save Settings"
    :ok-button-disabled-tooltip="!isStatusChanged ? `Toggle the switch to ${playerData.model.isDeveloper ? 'remove' : 'assign'} this player as a developer.` : undefined"
    permission="api.players.set_developer"
    @show="resetModal"
    data-testid="action-set-developer"
    )
    template(#default)
      div(class="tw-flex tw-justify-between")
        span(class="tw-font-semibold") Developer Status
        MInputSwitch(
          :model-value="isDeveloper"
          class="tw-relative tw-top-1 tw-mr-3"
          name="isPlayerDeveloper"
          size="small"
          @update:model-value="isDeveloper = $event"
          data-testid="developer-status-toggle"
          )
      div(class="tw-text-xs+ tw-text-neutral-500")
        div(class="tw-mb-1") Developer players have special powers. For instance, developers can:
        ul(class="tw-ps-1.5")
          li - Login during maintenance.
          li - Execute development-only actions in production.
          li - Allow validating iOS sandbox in-app purchases.
          li - Bypass logic version downgrade check in production.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MInputSwitch, MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to set as developer.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
const isDeveloper = ref(false)

const isStatusChanged = computed(() => {
  return isDeveloper.value !== playerData.value.model.isDeveloper
})

function resetModal(): void {
  isDeveloper.value = playerData.value.model.isDeveloper
}

const { showSuccessNotification } = useNotifications()

async function updateDeveloperStatus(): Promise<void> {
  const response = await gameServerApi.post(
    `/players/${playerData.value.id}/developerStatus?newStatus=${isDeveloper.value}`
  )
  showSuccessNotification(
    `${playerData.value.model.playerName ?? playerData.value.id} ${response.data.isDeveloper ? 'set as developer' : 'no longer set as developer'}.`
  )
  playerRefresh()
}
</script>

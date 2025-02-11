<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  v-if="playerData"
  modal-title="Update Logic Version"
  :action="updateLogicVersion"
  trigger-button-label="Update Logic Version"
  variant="warning"
  ok-button-label="Save Settings"
  :ok-button-disabled-tooltip="!isStatusChanged ? `Select a different logic version to update the player.` : undefined"
  :trigger-button-disabled-tooltip="playerData.model.logicVersion == coreStore.supportedLogicVersions.maxVersion ? 'Player is already at the highest active logic version.' : undefined"
  permission="api.players.update_logic_version"
  @show="resetModal"
  data-testid="action-update-logic-version-player"
  )
  div(class="tw-flex tw-justify-between")
    span(class="tw-font-semibold") Logic Version
    MInputSingleSelectRadio(
      :model-value="selectedLogicVersion"
      class="tw-relative"
      name="logicVersionInput"
      size="small"
      :options="logicVersionOptions"
      @update:model-value="selectedLogicVersion = $event"
      data-testid="player-logic-version-input"
      )
  span(class="tw-text-xs+ tw-text-neutral-500") Changing the player's logic version will disconnect the player and force them to update to a newer version of the game client before they can reconnect.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MInputSingleSelectRadio, MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../../coreStore'
import { getBackendStatusSubscriptionOptions } from '../../../subscription_options/general'
import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const { showSuccessNotification } = useNotifications()

const props = defineProps<{
  /**
   * ID of the player.
   */
  playerId: string
}>()

const coreStore = useCoreStore()

const gameServerApi = useGameServerApi()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const selectedLogicVersion = ref(playerData.value.model.logicVersion)

const { data: backendStatusData } = useSubscription(getBackendStatusSubscriptionOptions())

function resetModal(): void {
  selectedLogicVersion.value = playerData.value.model.logicVersion
}

const isStatusChanged = computed(() => {
  return selectedLogicVersion.value !== playerData.value.model.logicVersion
})

const logicVersionOptions = computed((): Array<{ label: string; value: number }> => {
  if (!coreStore.supportedLogicVersionOptions) return []

  return coreStore.supportedLogicVersionOptions
    .filter(
      (version) =>
        (backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.minVersion <= version &&
          backendStatusData.value.clientCompatibilitySettings.activeLogicVersionRange.maxVersion >= version) ||
        version === playerData.value.model.logicVersion
    )
    .map((version) => ({
      label: version === playerData.value.model.logicVersion ? `${version} (current)` : String(version),
      value: version,
      disabled: version < playerData.value.model.logicVersion,
    }))
})

async function updateLogicVersion(): Promise<void> {
  await gameServerApi.post(`/players/${playerData.value.id}/updateLogicVersion`, {
    newLogicVersion: selectedLogicVersion.value,
  })
  showSuccessNotification(`${playerData.value.model.playerName || 'n/a'} update to ${selectedLogicVersion.value}.`)
  playerRefresh()
}
</script>

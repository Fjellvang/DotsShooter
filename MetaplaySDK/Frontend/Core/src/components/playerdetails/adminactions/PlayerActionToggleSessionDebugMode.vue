<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  MActionModalButton(
    modal-title="Control Session Debug Mode"
    :action="toggleSessionDebugMode"
    :trigger-button-label="playerData.model.sessionDebugModeOverride ? 'Control Debug Mode' : 'Enable Debug Mode'"
    trigger-button-full-width
    variant="warning"
    ok-button-label="Save Settings"
    :ok-button-disabled-tooltip="saveSettingsButtonDisabledText"
    permission="api.players.toggle_debug_mode"
    @show="resetModal"
    data-testid="action-toggle-debug-mode"
    )
    template(#default)
      div(class="tw-mb-3") Session debug mode enables additional debug tools for an individual player's sessions, allowing developers to pinpoint issues associated with that player.

      div(class="tw-flex tw-justify-between")
        span(class="tw-font-semibold") Session Debug Mode Enabled
        MInputSwitch(
          :model-value="debugModeEnabled"
          class="tw-relative tw-top-1 tw-mr-3"
          name="isPlayerSessionDebugModeEnabled"
          size="small"
          @update:model-value="debugModeEnabled = $event"
          data-testid="player-debug-mode-toggle"
          )

      div(class="tw-mt-3")
        MInputNumber(
          label="Number of Sessions"
          :disabled="!debugModeEnabled"
          :model-value="forNextNumSessions"
          :min="1"
          :hint-message="`Session debug mode will automatically become disabled for this player after ${maybePluralString(forNextNumSessions, 'session')}.`"
          @update:model-value="forNextNumSessions = $event ?? 1"
          )

      div(class="tw-mt-3 tw-flex tw-justify-between")
        span(
          :class="['tw-font-bold tw-text-sm tw-leading-6', { 'tw-text-neutral-900': debugModeEnabled, 'tw-text-neutral-400': !debugModeEnabled }]"
          ) Extra Logic Execution Checks
        MInputSwitch(
          :disabled="!debugModeEnabled"
          :model-value="enableEntityDebugConfig"
          class="tw-relative tw-top-1 tw-mr-3"
          name="enableEntityDebugConfig"
          size="small"
          @update:model-value="enableEntityDebugConfig = $event"
          )
      MInputHintMessage Enable additional consistency checks for game logic execution. These checks are computationally expensive but can help pinpoint bugs more easily.

      div(class="tw-mt-3")
        MInputSingleSelectDropdown(
          label="Incident Report Uploads"
          :disabled="!debugModeEnabled"
          :model-value="incidentUploadMode"
          :options="incidentUploadModeOptions"
          :hint-message="incidentUploadModeDescription"
          @update:model-value="incidentUploadMode = $event"
          )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MInputSwitch,
  MInputNumber,
  MInputSingleSelectDropdown,
  MInputHintMessage,
  MActionModalButton,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const gameServerApi = useGameServerApi()
const { showSuccessNotification } = useNotifications()

const props = defineProps<{
  /**
   * ID of the player to set as developer.
   */
  playerId: string
}>()

const debugModeEnabled = ref(false)

const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

// Inputs -------------------------------------------------------------------------------------------------------------

const defaultForNextNumSessions = 3
const forNextNumSessions = ref(defaultForNextNumSessions)

const enableEntityDebugConfig = ref(false)

type IncidentUploadMode = 'Normal' | 'SilentlyOmitUploads' | 'RejectIncidents'
const incidentUploadModeOptions: Array<{ label: string; value: IncidentUploadMode }> = [
  { label: 'Normal', value: 'Normal' },
  { label: "Don't upload (keep on client)", value: 'SilentlyOmitUploads' },
  { label: "Don't upload (discard from client)", value: 'RejectIncidents' },
]
const incidentUploadMode = ref<IncidentUploadMode>('Normal')

// UI -----------------------------------------------------------------------------------------------------------------

function resetModal(): void {
  debugModeEnabled.value = !!playerData.value.model.sessionDebugModeOverride
  forNextNumSessions.value =
    playerData.value.model.sessionDebugModeOverride?.forNextNumSessions ?? defaultForNextNumSessions
  enableEntityDebugConfig.value =
    playerData.value.model.sessionDebugModeOverride?.parameters.enableEntityDebugConfig ?? false
  incidentUploadMode.value = playerData.value.model.sessionDebugModeOverride?.parameters.incidentUploadMode ?? 'Normal'
}

const saveSettingsButtonDisabledText = computed(() => {
  if (!playerData.value.model.sessionDebugModeOverride && !debugModeEnabled.value) {
    // Disabled -> disabled: not changed.
    return 'No changes to save.'
  } else if (!!playerData.value.model.sessionDebugModeOverride && !debugModeEnabled.value) {
    // Enabled -> disabled: changed.
    return undefined
  } else if (!playerData.value.model.sessionDebugModeOverride && debugModeEnabled.value) {
    // Disabled -> enabled...
    if (!enableEntityDebugConfig.value && incidentUploadMode.value === 'Normal') {
      // ... but no individual parameter was enabled.
      return 'At least one of the individual parameters must be enabled in order for the debug mode to take effect.'
    } else {
      // ... and at least some parameter was enabled.
      return undefined
    }
  } else {
    // Enabled -> enabled: changed if any sub-parameter was changed.
    const changed =
      forNextNumSessions.value !== playerData.value.model.sessionDebugModeOverride.forNextNumSessions ||
      enableEntityDebugConfig.value !==
        playerData.value.model.sessionDebugModeOverride.parameters.enableEntityDebugConfig ||
      incidentUploadMode.value !== playerData.value.model.sessionDebugModeOverride.parameters.incidentUploadMode
    if (!changed) {
      return 'No changes to save.'
    } else {
      return undefined
    }
  }
})

const incidentUploadModeDescription = computed(() => {
  const descriptions: Record<IncidentUploadMode, string | undefined> = {
    Normal: 'Incident reports will be uploaded normally.',
    SilentlyOmitUploads:
      "The client won't upload incidents reports to the server, but will keep them locally in case you change this option later.",
    RejectIncidents:
      "The client won't upload incidents reports to the server, and will delete them from its local storage.",
  }
  return descriptions[incidentUploadMode.value] ?? '<unknown>'
})

// Server calls -------------------------------------------------------------------------------------------------------

async function toggleSessionDebugMode(): Promise<void> {
  const body = {
    forNextNumSessions: forNextNumSessions.value,
    parameters: {
      enableEntityDebugConfig: enableEntityDebugConfig.value,
      incidentUploadMode: incidentUploadMode.value,
    },
  }
  const response = await gameServerApi.post(
    `/players/${playerData.value.id}/sessionDebugMode?enabled=${debugModeEnabled.value}`,
    body
  )
  showSuccessNotification(
    `${playerData.value.model.playerName || 'n/a'} debug mode ${debugModeEnabled.value ? 'enabled' : 'disabled'}.`
  )
  playerRefresh()
}
</script>

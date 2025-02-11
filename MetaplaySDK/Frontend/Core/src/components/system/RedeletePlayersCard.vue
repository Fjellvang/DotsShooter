<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Card.
MCard(title="Re-Delete Players")
  template(#subtitle)
    p Batch re-delete players that have been marked for deletion, but the deletion was un-done during a backup restore.

  //- Action Modal.
  div(class="tw-flex tw-justify-end")
    MActionModalButton(
      modal-title="Re-Delete Players"
      :action="redeletePlayers"
      trigger-button-label="Open Re-Delete Menu"
      ok-button-label="Re-Delete Players"
      :ok-button-disabled-tooltip="!isFormValid ? 'Upload a valid log file to proceed.' : undefined"
      permission="api.system.player_redelete"
      @show="resetModal()"
      )
      template(#default)
        p You can use this tool to upload player deletion logs and recover the deletion status of players in case something may have been lost during backup recovery.
        p(class="tw-text-sm tw-mt-2 tw-text-neutral-500 tw-mb-2") This feature is a practical way to respect GDPR and the right for your players to be forgotten even during backup recovery scenarios!

        div(class="tw-space-y-2")
          MInputSingleFile(
            label="Log File"
            :model-value="file"
            :variant="file ? (error ? 'danger' : 'success') : 'default'"
            @update:model-value="onFileUpdated"
            )

          MInputDateTime(
            label="Re-Delete Cutoff Time (UTC)"
            :model-value="cutoffTime"
            max-date-time="utcNow"
            @update:model-value="onDateUpdated"
            )

      template(#right-panel)
        h6(class="tw-mb-2") Select Players
        div(v-if="players && players.length > 0")
          MList(:showBorder="true")
            MListItem(
              v-for="(player, key) in players"
              :key="key"
              class="tw-px-3"
              )
              span {{ player.playerName || 'n/a' }}
              template(#top-right)
                span {{ player.playerId }}
              template(#bottom-left)
                div(class="tw-text-xs+ tw-text-neutral-500 tw-break-words") Deleted #[meta-time(:date="player.scheduledDeletionTime")] by {{ player.deletionSource }}
                span
                  span Marked for re-deletion:
                  MInputCheckbox(
                    :model-value="player.redelete"
                    name="isPlayerMarkedForRedeletion"
                    @update:model-value="player.redelete = $event"
                    )
                div(class="tw-mt-2")
                  MBadge(
                    v-if="player.redelete"
                    variant="danger"
                    ) To Be Deleted
                  MBadge(v-else) Skip

        div(v-else-if="players && players.length == 0")
          MCallout(
            variant="danger"
            title="No Players Found"
            ) Based on the selected log file and cutoff time, there are no players who are eligible for re-deletion.

        div(v-else)
          p(class="tw-text-neutral-500 tw-italic") Choose a valid log file to preview players for re-deletion.

        div(v-if="players" class="tw-mt-2" :class="[{ 'tw-text-neutral-500': players }, 'tw-mb-3']")
          h6(class="tw-mb-1") Confirm
          div(class="tw-flex tw-justify-between")
            span I know what I am doing
            MInputSwitch(
              :model-value="confirmRedeletion"
              :disabled="!players || players.length == 0"
              class
              name="confirmRedeletion"
              size="small"
              @update:model-value="confirmRedeletion = $event"
              )

        MErrorCallout(
          v-if="error"
          :error="error"
          )

      template(#bottom-panel)
        meta-no-seatbelts(class="tw-w-7/12 tw-mx-auto")
</template>

<script lang="ts" setup>
import { DateTime, Duration } from 'luxon'
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  type DisplayError,
  MActionModalButton,
  MBadge,
  MCallout,
  MCard,
  MErrorCallout,
  MInputCheckbox,
  MInputDateTime,
  MInputSingleFile,
  MInputSwitch,
  MList,
  MListItem,
  useNotifications,
} from '@metaplay/meta-ui-next'

const gameServerApi = useGameServerApi()
const players = ref<any>(null)
const cutoffTime = ref<DateTime>(DateTime.now())
const result = ref<any>(null)
const error = ref<DisplayError>()
const confirmRedeletion = ref(false)
const file = ref<File>()

const isFormValid = computed(() => {
  return players.value !== null && confirmRedeletion.value
})
const defaultCutoffTime = computed((): DateTime => {
  const offset = Duration.fromDurationLike({ days: 60 })
  return DateTime.now().minus(offset)
})

function resetModal(): void {
  players.value = null
  result.value = null
  confirmRedeletion.value = false
  cutoffTime.value = defaultCutoffTime.value
  file.value = undefined
  error.value = undefined
}

const { showSuccessNotification } = useNotifications()

function onFileUpdated(value: File | undefined): void {
  file.value = value
  void fetchRedeletionList()
}

async function redeletePlayers(): Promise<void> {
  if (!file.value) return

  // Build the request.
  const formData = new FormData()
  formData.append('file', file.value)
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  formData.append('cutoffTime', cutoffTime.value.toISO()!)
  players.value
    .filter((p: any) => p.redelete)
    .forEach((p: any) => {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      formData.append('playerIds', p.playerId)
    })
  // Send.
  await gameServerApi.post('redeletePlayers/execute', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  const message = 'Player re-deletion started.'
  showSuccessNotification(message)
}

/**
 * Utility function to prevent undefined inputs.
 */
async function onDateUpdated(value?: DateTime): Promise<void> {
  if (!value) return
  cutoffTime.value = value

  // Handle empty state.
  if (file.value !== null) {
    await fetchRedeletionList()
  }
}

async function fetchRedeletionList(): Promise<void> {
  // Reset the results.
  players.value = null
  confirmRedeletion.value = false
  error.value = undefined

  if (!file.value) return

  // Build the request.
  const formData = new FormData()
  formData.append('file', file.value)
  // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
  formData.append('cutoffTime', cutoffTime.value.toISO()!)

  // Send.
  try {
    const res = (
      await gameServerApi.post('redeletePlayers/list', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
    ).data

    // Process results.
    players.value = res.playerInfos.map((p: any) => ({ ...p, redelete: true }))
  } catch (e: any) {
    // TODO: This doesn't look nice. Make it nice.
    error.value = e.response.data.error
  }
}
</script>

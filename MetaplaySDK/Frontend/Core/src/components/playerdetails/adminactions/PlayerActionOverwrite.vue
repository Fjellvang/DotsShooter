<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Button to display the modal
MActionModalButton(
  v-if="playerData"
  modal-title="Overwrite Player"
  :action="() => overwritePlayer(entityArchiveFile ?? entityArchiveText)"
  trigger-button-label="Overwrite Player"
  variant="danger"
  ok-button-label="Overwrite Player"
  :ok-button-disabled-tooltip="!isFormValid ? 'Paste in or upload a valid player to proceed.' : undefined"
  permission="api.players.overwrite"
  @show="resetModal"
  @hidden="resetModal"
  data-testid="action-overwrite-player"
  )
  template(#default)
    h6(class="tw-mb-1") Paste Player Data
    p(class="tw-mb-2") You can copy & paste the serialized data of a compatible player here to overwrite parts of #[MBadge {{ playerData.model.playerName || 'n/a' }}].
    p(class="tw-mb-4") This is an #[span(class="tw-text-danger") advanced development feature] and should probably never be used in production!

    MInputTextArea(
      class="tw-mb-2"
      label="Paste in an archive..."
      :model-value="entityArchiveText"
      :placeholder="entityArchiveFile != null ? 'File upload selected' : `{'entities':{'player':...`"
      :variant="isFormValid !== null ? (isFormValid ? 'success' : 'danger') : 'default'"
      :rows="5"
      :disabled="!!entityArchiveFile"
      @update:model-value="onEntityArchiveTextChange"
      data-testid="entity-archive-text"
      )

    MInputSingleFileContents(
      label="...or upload a file..."
      :model-value="entityArchiveFile"
      :placeholder="entityArchiveText !== '' ? 'Manual paste selected' : 'Choose or drop an entity archive file'"
      :variant="isFormValid !== null ? (isFormValid ? 'success' : 'danger') : 'default'"
      :disabled="entityArchiveText !== ''"
      accepted-file-types=".json"
      @update:model-value="onEntityArchiveFileChange"
      data-testid="entity-archive-file"
      )

  template(#right-panel)
    h6(class="tw-mb-4") Preview Incoming Data
    MCallout(
      v-if="entityContainedExtraTypes"
      title="Extra Entity Types"
      variant="warning"
      class="tw-mb-2"
      )
      p Archive contained the following extra entity types that were removed: #[span(v-for="entity in entityContainedExtraTypes" class="tw-mr-1") {{ entity }}]

    MCallout(
      v-if="!validationResultString && !displayError"
      title="Preview"
      variant="neutral"
      class="tw-mb-2"
      ) Paste in a valid player from a compatible game version to preview what data will be copied over.

    MErrorCallout(
      v-if="displayError"
      :error="displayError"
      class="tw-mb-2"
      )

    div(v-if="validationResultString")
      div(
        style="max-height: 20.3rem"
        class="code-box text-monospace border rounded bg-light tw-w-full"
        )
        pre {{ validationResultString }}

  template(
    v-if="isFormValid"
    #bottom-panel
    )
    meta-no-seatbelts(
      class="tw-mx-auto tw-w-7/12"
      :name="playerData.model.playerName || 'n/a'"
      )
</template>

<script lang="ts" setup>
import axios, { type CancelTokenSource } from 'axios'
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  MInputTextArea,
  MInputSingleFileContents,
  MCallout,
  MErrorCallout,
  DisplayError,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player to target the overwrite action at.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to the player whose data will be overwritten.
 */
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * The entity archive text pasted into the text field.
 */
const entityArchiveText = ref('')

/**
 * The entity archive file that the user has selected.
 */
const entityArchiveFile = ref<string>()

/**
 * The validation result string.
 */
const validationResultString = ref<string>()

/**
 * When true the entity archive player model is valid to overwrite.
 */
const playerModelValidToOverwrite = ref(false)

/**
 * The extra entity types that were removed from the entity archive.
 */
const entityContainedExtraTypes = ref<string[]>()

/**
 * Error to be displayed.
 */
const displayError = ref<DisplayError>()

/**
 * The cancel token source for the validation request.
 */
const cancelTokenSource = ref<CancelTokenSource>()

/**
 * Whether the form is valid.
 */
const isFormValid = computed(() => {
  const hasEntityArchive = entityArchiveText.value !== '' || entityArchiveFile.value
  if (!hasEntityArchive || (hasEntityArchive && !displayError.value && !validationResultString.value)) {
    return null
  } else if (validationResultString.value && playerModelValidToOverwrite.value) {
    return true
  } else {
    return false
  }
})

function onEntityArchiveTextChange(value: string): void {
  entityArchiveText.value = value
  void validatePlayer(value)
}

function onEntityArchiveFileChange(value: string | undefined): void {
  entityArchiveFile.value = value
  void validatePlayer(value)
}

/**
 * Reset the modal to its initial state.
 */
function resetModal(): void {
  entityArchiveText.value = ''
  entityArchiveFile.value = undefined
  validationResultString.value = undefined
  playerModelValidToOverwrite.value = false
  displayError.value = undefined
}

/**
 * Validates the player model data against the server.
 * @param rawData The raw player model data.
 */
async function validatePlayer(rawData?: string): Promise<void> {
  displayError.value = undefined
  validationResultString.value = undefined
  entityContainedExtraTypes.value = undefined
  playerModelValidToOverwrite.value = false

  if (rawData) {
    // Get the payload data that we want to validate.
    let payload
    try {
      payload = calculatePayload(rawData)
    } catch (e: any) {
      displayError.value = new DisplayError(
        'Calculating payload failed',
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        e.message
      )
      return
    }

    // Send the payload to the server to validate it.
    try {
      if (cancelTokenSource.value) {
        cancelTokenSource.value.cancel('Request canceled by user interaction.')
      }
      cancelTokenSource.value = axios.CancelToken.source()
      const result = (
        await gameServerApi.post(`/players/${playerData.value.id}/validateOverwrite`, payload, {
          cancelToken: cancelTokenSource.value.token,
        })
      ).data
      if (result.error) {
        // If the result has an error object then it failed.
        displayError.value = new DisplayError(
          'Validation failed',
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          result.error.message,
          500,
          undefined,
          [{ title: 'Details', content: result.error.details }]
        )
      } else {
        // Otherwise there must be a data object, indicating success.
        if (result.data.diff === '') {
          validationResultString.value = 'No differences in player model data.\nDid you paste in the correct player?'
        } else {
          validationResultString.value = result.data.diff
          playerModelValidToOverwrite.value = true
        }
      }
    } catch (e: any) {
      if (axios.isCancel(e)) {
        // Ignore
      } else if (e.response) {
        displayError.value = new DisplayError(
          'Validaiton failed',
          // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
          e.response.data.error.message
        )
      }
    }
  }
}

const { showSuccessNotification } = useNotifications()

/**
 * Overwrites the player with the data in the entity archive.
 */
async function overwritePlayer(newData: string): Promise<void> {
  const payload = calculatePayload(newData)
  await gameServerApi.post(`/players/${playerData.value.id}/importOverwrite`, payload)
  showSuccessNotification('Player import succeeded.')
  playerRefresh()
}

/**
 * Preview the diffs between the current and the 'new' player model that will be sent to the server.
 * @param rawData The raw player model data.
 */
function calculatePayload(rawData: string): any {
  let payload: any
  try {
    payload = JSON.parse(rawData)
  } catch (e: any) {
    throw new Error(`Could not parse archive. Got '${e.message}'.`)
  }

  // Client-side validatation of the entity archive.
  if (typeof payload !== 'object') {
    throw new Error(`Entity archive must be an object. Got '${typeof payload}'.`)
  }
  if (Array.isArray(payload)) {
    throw new Error("Entity archive must be an object. Got 'array'.")
  }
  if (!('entities' in payload)) {
    throw new Error('Entity archive must contain entities but none found.')
  }
  if (!('player' in payload.entities)) {
    throw new Error('Entity archive must contain player entities but none found.')
  }
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const players = Object.keys(payload.entities.player)
  if (players.length !== 1) {
    throw new Error(`Entity archive may only contain exactly one player entity. Got ${players.length}.`)
  }

  // Remove any non-player entity types.
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const keys = Object.keys(payload.entities).filter((e) => e !== 'player')
  // TODO: dynamic delete is not a great pattern. Consider refactoring this implementation.
  // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
  keys.forEach((key) => delete payload.entities[key])
  if (keys.length) {
    entityContainedExtraTypes.value = keys
  }

  // Create the remap data.
  const sourcePlayerId = players[0]
  const targetPlayerId = playerData.value.id
  payload.remaps = {
    player: {},
  }
  payload.remaps.player[sourcePlayerId] = targetPlayerId
  return payload
}
</script>

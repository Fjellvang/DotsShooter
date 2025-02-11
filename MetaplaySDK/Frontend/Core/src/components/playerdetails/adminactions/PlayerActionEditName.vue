<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  v-if="playerData"
  modal-title="Edit Player Name"
  :action="updateName"
  trigger-button-label="Edit Name"
  variant="warning"
  :ok-button-disabled-tooltip="getDisabledReason(validationState)"
  permission="api.players.edit_name"
  @show="resetModal"
  data-testid="action-edit-name"
  )
  template(#default)
    h6(class="tw-mb-1 tw-font-semibold") Current Name
    p(class="tw-mb-4") {{ playerData.model.playerName ?? 'n/a' }}

    MInputText(
      label="New Name"
      :model-value="newName"
      :variant="newNameInputVariant"
      hint-message="Same rules are applied to name validation as changing it in-game."
      placeholder="DarkAngel87"
      @update:model-value="(event) => (newName = event)"
      data-testid="name-input"
      )
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { useGameServerApi, makeAxiosActionHandler, makeActionDebouncer } from '@metaplay/game-server-api'
import { MInputText, MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to rename.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to player data used to render this component.
 */
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * New name as entered by the user.
 */
const newName = ref('')

/**
 * State of the name validation. `no-change` means that the name has not been changed. `validating` means we are
 * debouncing the request and then waiting for the server to respond.
 */
const validationState = ref<'valid' | 'invalid' | 'no-change' | 'validating'>('valid')

/**
 * Reset the modal to its initial state.
 */
function resetModal(): void {
  newName.value = playerData.value?.model.playerName ?? ''
}

/**
 * Request type for the name validation endpoint.
 */
interface ValidateNameRequest {
  newName: string
}

/**
 * Response type for the name validation endpoint.
 */
interface ValidateNameResponse {
  nameWasValid: boolean
}

function getDisabledReason(state: string): string | undefined {
  if (state === 'no-change') {
    return 'Change the name to proceed.'
  } else if (state === 'validating') {
    return 'Validating name...'
  } else if (state === 'invalid') {
    return 'The chosen name is not valid. Fill in a valid name to proceed.'
  } else return undefined
}

/**
 * Action debouncer to make validation requests to the server.
 */
const validationDebouncedAction = makeActionDebouncer(
  makeAxiosActionHandler<ValidateNameRequest, ValidateNameResponse>(),
  (response) => {
    if (response.data.nameWasValid) {
      if (newName.value === playerData.value?.model.playerName) {
        // Name change is a no-op.
        validationState.value = 'no-change'
      } else {
        // Name change is valid.
        validationState.value = 'valid'
      }
    } else {
      // Name change is invalid.
      validationState.value = 'invalid'
    }
  },
  () => {
    // Errors are unhandled.
  },
  500
)

/**
 * Watch the `newName` input and validate it on the server when it changes.
 */
watch(newName, () => {
  // Change the UI validation state to `validating` if the value has changed from the original. This will get set to
  // the correct state when the server responds. Note that we still do the validation even if the name hasn't changed.
  // This is because the rules on what is valid might have changed since the value was last set and, in that case, we
  // would expect the state to settle on `invalid`.
  if (newName.value === playerData.value?.model.playerName) {
    validationState.value = 'no-change'
  } else {
    validationState.value = 'validating'
  }

  // Make a debounced request.
  validationDebouncedAction.requestAction({
    url: `/players/${playerData.value.id}/validateName`,
    method: 'post',
    data: { newName: newName.value },
  })
})

/**
 * Maps the `validationState` to a color variant for the input.
 */
const newNameInputVariant = computed(() => {
  const validationStateToVariant: Record<string, string> = {
    valid: 'success',
    invalid: 'danger',
    'no-change': 'default',
    validating: 'loading',
  }
  const variant = validationStateToVariant[validationState.value] ?? 'danger'

  // Ugly casting here because MInputText has no exported variant type.
  return variant as 'default' | 'danger' | 'success' | 'loading'
})

const { showSuccessNotification } = useNotifications()

/**
 * Update the player's name on the server.
 */
async function updateName(): Promise<void> {
  try {
    await gameServerApi.post(`/players/${playerData.value.id}/changeName`, {
      NewName: newName.value,
    })
    const message = `Player '${playerData.value.id}' is now '${newName.value}'.`
    showSuccessNotification(message)
  } finally {
    playerRefresh()
  }
}
</script>

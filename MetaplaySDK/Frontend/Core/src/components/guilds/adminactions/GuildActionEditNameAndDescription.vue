<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Edit Guild Name and Description"
  :action="updateNameAndDescription"
  trigger-button-label="Edit Name and Description"
  :trigger-button-disabled-tooltip="triggerButtonDisabledReason"
  ok-button-label="Update"
  :ok-button-disabled-tooltip="okButtonDisabledReason"
  permission="api.guilds.edit_details"
  @show="resetModal"
  data-testid="action-edit-details"
  )
  MInputText(
    label="Display Name"
    :model-value="newDisplayName"
    :variant="validationStateToVariant(validationStateDisplayName)"
    hint-message="Same rules are applied to name validation as changing it in-game."
    class="tw-mb-4"
    @update:model-value="(event) => (newDisplayName = event)"
    )

  MInputText(
    label="Description"
    :model-value="newDescription"
    :variant="validationStateToVariant(validationStateDescription)"
    @update:model-value="(event) => (newDescription = event)"
    )
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { useGameServerApi, makeAxiosActionHandler, makeActionDebouncer } from '@metaplay/game-server-api'
import { MActionModalButton, MInputText, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleGuildSubscriptionOptions } from '../../../subscription_options/guilds'

const props = defineProps<{
  /**
   * ID of the guild to edit.
   */
  guildId: string
}>()
const gameServerApi = useGameServerApi()

/**
 * Subscribe to guild data used to render this component.
 */
const { data: guildData, refresh: guildTriggerRefresh } = useSubscription(
  getSingleGuildSubscriptionOptions(props.guildId)
)

/**
 * New name as entered by the user.
 */
const newDisplayName = ref('')

/**
 * New description as entered by the user.
 */
const newDescription = ref('')

/**
 * Type of the validation state. `no-change` means that the value has not been changed. `validating` means we are
 * debouncing the request and then waiting for the server to respond.
 */
type ValidationState = 'valid' | 'invalid' | 'no-change' | 'validating'

/**
 * State of the name validation.
 */
const validationStateDisplayName = ref<ValidationState>('valid')

/**
 * State of the description validation.
 */
const validationStateDescription = ref<ValidationState>('valid')

/**
 * Reset the modal to its initial state.
 */
function resetModal(): void {
  newDisplayName.value = guildData.value.model.displayName
  newDescription.value = guildData.value.model.description
}

/**
 * Request type for the validation endpoint.
 */
interface ValidateNameAndDescriptionRequest {
  newDisplayName: string
  newDescription: string
}

/**
 * Response type for the validation endpoint.
 */
interface ValidateNameAndDescriptionResponse {
  displayNameWasValid: boolean
  descriptionWasValid: boolean
}

/**
 * Action debouncer to make validation requests to the server.
 */
const validationDebouncedAction = makeActionDebouncer(
  makeAxiosActionHandler<ValidateNameAndDescriptionRequest, ValidateNameAndDescriptionResponse>(),
  (response) => {
    if (response.data.displayNameWasValid) {
      if (newDisplayName.value === guildData.value?.model.displayName) {
        // Name change is a no-op.
        validationStateDisplayName.value = 'no-change'
      } else {
        // Name change is valid.
        validationStateDisplayName.value = 'valid'
      }
    } else {
      // Name change is invalid.
      validationStateDisplayName.value = 'invalid'
    }

    if (response.data.descriptionWasValid) {
      if (newDescription.value === guildData.value?.model.description) {
        // Name change is a no-op.
        validationStateDescription.value = 'no-change'
      } else {
        // Name change is valid.
        validationStateDescription.value = 'valid'
      }
    } else {
      // Name change is invalid.
      validationStateDescription.value = 'invalid'
    }
  },
  () => {
    // Errors are unhandled.
  },
  500
)

/**
 * Watch the inputs and validate them on the server when they changes.
 */
watch(
  [newDisplayName, newDescription],
  (oldValues, newValues) => {
    // Change the UI validation state to `validating` if the value has changed from the original. This will get set to
    // the correct state when the server responds. Note that we still do the validation even if the name hasn't changed.
    // This is because the rules on what is valid might have changed since the value was last set and, in that case, we
    // would expect the state to settle on `invalid`.
    if (newDisplayName.value === guildData.value.model.displayName) {
      validationStateDisplayName.value = 'no-change'
    } else if (oldValues[0] !== newValues[0]) {
      validationStateDisplayName.value = 'validating'
    }
    if (newDescription.value === guildData.value.model.description) {
      validationStateDescription.value = 'no-change'
    } else if (oldValues[1] !== newValues[1]) {
      validationStateDescription.value = 'validating'
    }

    // Make a debounced request.
    validationDebouncedAction.requestAction({
      url: `/guilds/${guildData.value.id}/validateDetails`,
      method: 'post',
      data: {
        newDisplayName: newDisplayName.value,
        newDescription: newDescription.value,
      },
    })
  },
  { deep: true }
)

/**
 * Maps the validation states to a color variant for the input.
 */
function validationStateToVariant(validationState: ValidationState): 'default' | 'danger' | 'success' | 'loading' {
  const validationStateToVariant: Record<ValidationState, string> = {
    valid: 'success',
    invalid: 'danger',
    'no-change': 'default',
    validating: 'loading',
  }
  const variant = validationStateToVariant[validationState] ?? 'danger'

  // Ugly casting here because MInputText has no exported variant type.
  return variant as 'default' | 'danger' | 'success' | 'loading'
}

/**
 * Whether the OK button should be disabled.
 */
const okButtonDisabledReason = computed((): string | undefined => {
  if (validationStateDisplayName.value === 'validating' || validationStateDescription.value === 'validating') {
    return 'Validating...'
  }
  if (validationStateDisplayName.value === 'invalid')
    return 'Invalid name. Check that it is filled in correctly to proceed.'
  else if (validationStateDescription.value === 'invalid')
    return 'Invalid description. Check that it is filled in correctly to proceed.'
  else if (validationStateDisplayName.value === 'no-change' && validationStateDescription.value === 'no-change') {
    // Can't update if neither field was changed.
    return 'Change the name or description to proceed.'
  } else {
    return undefined
  }
})

const { showSuccessNotification } = useNotifications()

/**
 * Update the guild's name and description on the server.
 */
async function updateNameAndDescription(): Promise<void> {
  try {
    await gameServerApi.post(`/guilds/${guildData.value.id}/changeDetails`, {
      NewDisplayName: newDisplayName.value,
      NewDescription: newDescription.value,
    })
    const message = `Guild renamed to '${newDisplayName.value}'.`
    showSuccessNotification(message)
  } finally {
    guildTriggerRefresh()
  }
}

/**
 * Disable the trigger button under certain conditions.
 */
const triggerButtonDisabledReason = computed(() => {
  if (guildData.value.model.lifecyclePhase === 'Closed') {
    return 'Guild is closed.'
  } else {
    return undefined
  }
})
</script>

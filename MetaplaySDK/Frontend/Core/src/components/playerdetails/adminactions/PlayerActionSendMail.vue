<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(v-if="playerData")
  MActionModalButton(
    modal-title="Send In-Game Mail"
    :action="sendMail"
    trigger-button-label="Send Mail"
    ok-button-label="Send Mail"
    :ok-button-disabled-tooltip="okButtonDisabledReason"
    permission="api.players.mail"
    @show="resetModal"
    data-testid="action-mail"
    )
    template(#action-button-icon)
      fa-icon(
        icon="paper-plane"
        class="mr-2"
        )/
    template(#modal-subtitle)
      span(class="tw-mr-32 tw-text-xs+ tw-text-neutral-500") Player's language:&#32;
        meta-language-label(
          :language="playerData.model.language"
          variant="badge"
          )
    template(#default)
      //- Styling issue where there is y scroll bar for overflow.
      //- TODO: Need to remove it.
      meta-generated-form(
        v-model="mail"
        :typeName="'Metaplay.Core.InGameMail.MetaInGameMail'"
        :forcedLocalization="playerData.model.language"
        :page="'PlayerActionSendMail'"
        class="!tw-overflow-x-hidden"
        :logic-version="playerData.model.logicVersion"
        @status="isValid = $event"
        )
</template>

<script lang="ts" setup>
import { ref, computed } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'
import MetaLanguageLabel from '../../MetaLanguageLabel.vue'
import MetaGeneratedForm from '../../generatedui/components/MetaGeneratedForm.vue'

const props = defineProps<{
  /**
   * ID of the player to send the mail to.
   */
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

const mail = ref<any>({})
const isValid = ref(false)

function resetModal(): void {
  mail.value = {}
  isValid.value = false
}

const okButtonDisabledReason = computed(() => {
  if (!mail.value) {
    return 'Fill in the required fields to proceed.'
  } else if (!isValid.value) {
    return 'Some fields contain invalid data. Check that the fields are filled in correctly to proceed.'
  } else {
    return undefined
  }
})

const { showSuccessNotification } = useNotifications()

async function sendMail(): Promise<void> {
  await gameServerApi.post(`/players/${playerData.value.id}/sendMail`, mail.value)
  showSuccessNotification(`In-game mail sent to ${playerData.value.model.playerName || 'n/a'}.`)
  playerRefresh()
}
</script>

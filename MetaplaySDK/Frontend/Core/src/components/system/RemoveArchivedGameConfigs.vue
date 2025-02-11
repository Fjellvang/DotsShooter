<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Card.
MCard(
  title="Remove Archived Configs"
  :is-loading="archivedGameConfigCount === undefined"
  )
  template(#subtitle)
    p(v-if="archivedGameConfigCount === 0") You have no archived game configs to remove.
    p(v-else) {{ archivedGameConfigUiTexts.subtitle }}
      span(
        v-if="archivedGameConfigCount && archivedGameConfigCount > 0"
        class="tw-ml-1"
        ) {{ archivedGameConfigUiTexts.prune }}
  //- Action Modal.
  div(class="tw-flex tw-justify-end")
    MActionModalButton(
      modal-title="Remove Archived Configs"
      :action="removeArchived"
      trigger-button-label="Open Removal Menu"
      :trigger-button-disabled-tooltip="archivedGameConfigCount === 0 ? 'There are no archived game configs to remove.' : undefined"
      ok-button-label="Remove"
      :ok-button-disabled-tooltip="confirmRemoval ? undefined : 'Toggle the confirmation switch to enable.'"
      permission="api.game_config.delete"
      @show="onShow()"
      )
      template(#default)
        p {{ archivedGameConfigUiTexts.modalDescription }}

        h6(class="tw-mt-4") Confirm
        div(class="tw-flex tw-justify-between")
          span I know what I am doing
          MInputSwitch(
            :model-value="confirmRemoval"
            name="confirmRemoval"
            size="small"
            @update:model-value="(event) => (confirmRemoval = event)"
            )

        MetaNoSeatbelts(
          :message="archivedGameConfigUiTexts.noSeatbelts"
          class="tw-mt-4"
          )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, MCard, MInputSwitch, useNotifications } from '@metaplay/meta-ui-next'
import { maybePluralPrefixString, maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { sleep } from '../../coreUtils'
import { getAllGameConfigsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const { data: allGameConfigsData, refresh: allGameConfigsTriggerRefresh } = useSubscription(
  getAllGameConfigsSubscriptionOptions()
)

const gameServerApi = useGameServerApi()

/**
 * State of the "Confirm" toggle.
 */
const confirmRemoval = ref(false)

/**
 * Reset the modal on open.
 */
function onShow(): void {
  confirmRemoval.value = false
}

const { showSuccessNotification, showErrorNotification } = useNotifications()

/**
 * Make the request to remove the archived configs.
 */
async function removeArchived(): Promise<void> {
  const result = await gameServerApi.delete('gameConfig/deleteArchived')

  let tasksResponse: { completed: boolean; failure: string } | null
  do {
    await sleep(1000)
    tasksResponse = (await gameServerApi.get('backgroundTasks?taskId=' + result.data)).data
  } while (tasksResponse != null && !tasksResponse.completed)

  if (tasksResponse != null) {
    if (tasksResponse.failure === undefined || tasksResponse.failure == null) {
      showSuccessNotification(archivedGameConfigUiTexts.value.removed)
    } else {
      console.error(tasksResponse.failure)
      showErrorNotification('Failed to remove archived game configs, see logs for more information.')
    }
  } else {
    showErrorNotification('We lost track of the archived game config removal, likely due to the server restarting.')
  }

  allGameConfigsTriggerRefresh()
}

/**
 * Get a count of archived game configs.
 */
const archivedGameConfigCount = computed((): number | undefined => {
  if (allGameConfigsData.value) {
    return allGameConfigsData.value.reduce((accumulator: number, config) => {
      if (config.isArchived) return accumulator + 1
      else return accumulator
    }, 0)
  } else {
    return undefined
  }
})

/**
 * Human readable string for UI texts.
 */
const archivedGameConfigUiTexts = computed(
  (): {
    subtitle: string
    prune: string
    modalDescription: string
    noSeatbelts: string
    removed: string
  } => {
    const count = archivedGameConfigCount.value ?? 0
    return {
      subtitle: `You have ${maybePluralString(count, 'archived game config', true)} that ${maybePluralPrefixString(count, 'is', 'are')} not being used.`,
      prune: `You can prune ${maybePluralPrefixString(count, 'it', 'them')} here to keep your server running smoothly.`,
      modalDescription: `You currently have ${maybePluralString(count, 'archived game config', true)}. ${maybePluralPrefixString(count, 'This config is', 'These configs are')} not being used and can be safely removed. Removing ${maybePluralPrefixString(count, 'this config', 'these configs')} will improve your server performance.`,
      noSeatbelts: `Removing ${maybePluralPrefixString(count, 'this', 'these')} ${maybePluralString(count, 'archived game config', true)} from the server is permanent - ${maybePluralPrefixString(count, 'it', 'they')} can not be recovered.`,
      removed: `Removed ${maybePluralString(count, 'archived game config', true)}.`,
    }
  }
)
</script>

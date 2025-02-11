<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Card.
MCard(
  title="Remove Archived Localizations"
  :is-loading="archivedLocalizationCount === undefined"
  )
  template(#subtitle)
    p(v-if="archivedLocalizationCount === 0") You have no archived localizations to remove.
    p(v-else) {{ archivedLocalizationUiTexts.subtitle }}
      span(
        v-if="archivedLocalizationCount && archivedLocalizationCount > 0"
        class="tw-ml-1"
        ) {{ archivedLocalizationUiTexts.prune }}
  //- Action Modal.
  div(class="tw-flex tw-justify-end")
    MActionModalButton(
      modal-title="Remove Archived Localizations"
      :action="removeArchived"
      trigger-button-label="Open Removal Menu"
      :trigger-button-disabled-tooltip="archivedLocalizationCount === 0 ? 'There are no archived localizations to remove.' : undefined"
      ok-button-label="Remove"
      :ok-button-disabled-tooltip="confirmRemoval ? undefined : 'Toggle the confirmation switch to enable.'"
      permission="api.localization.delete"
      @show="onShow()"
      )
      template(#default)
        p {{ archivedLocalizationUiTexts.modalDescription }}

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
          :message="archivedLocalizationUiTexts.noSeatbelts"
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
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'

const { data: allLocalizationsData, refresh: allLocalizationsTriggerRefresh } = useSubscription(
  getAllLocalizationsSubscriptionOptions()
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
 * Make the request to remove the archived localizations.
 */
async function removeArchived(): Promise<void> {
  const result = await gameServerApi.delete('localization/deleteArchived')

  let tasksResponse: { completed: boolean; failure: string } | null
  do {
    await sleep(1000)
    tasksResponse = (await gameServerApi.get('backgroundTasks?taskId=' + result.data)).data
  } while (tasksResponse != null && !tasksResponse.completed)

  if (tasksResponse != null) {
    if (tasksResponse.failure === undefined || tasksResponse.failure == null) {
      showSuccessNotification(archivedLocalizationUiTexts.value.removed)
    } else {
      console.error(tasksResponse.failure)
      showErrorNotification('Failed to remove archived localizations, see logs for more information.')
    }
  } else {
    showErrorNotification('We lost track of the archived localization removal, likely due to the server restarting.')
  }

  allLocalizationsTriggerRefresh()
}

/**
 * Get a count of archived localizations.
 */
const archivedLocalizationCount = computed((): number | undefined => {
  if (allLocalizationsData.value) {
    return allLocalizationsData.value.reduce((accumulator: number, localization) => {
      if (localization.isArchived) return accumulator + 1
      else return accumulator
    }, 0)
  } else {
    return undefined
  }
})

/**
 * Human readable string for UI texts.
 */
const archivedLocalizationUiTexts = computed(
  (): {
    subtitle: string
    prune: string
    modalDescription: string
    noSeatbelts: string
    removed: string
  } => {
    const count = archivedLocalizationCount.value ?? 0
    return {
      subtitle: `You have ${maybePluralString(count, 'archived localization', true)} that ${maybePluralPrefixString(count, 'is', 'are')} not being used.`,
      prune: `You can prune ${maybePluralPrefixString(count, 'it', 'them')} here to keep your server running smoothly.`,
      modalDescription: `You currently have ${maybePluralString(count, 'archived localization', true)}. ${maybePluralPrefixString(count, 'This localization is', 'These localizations are')} not being used and can be safely removed. Removing ${maybePluralPrefixString(count, 'this localization', 'these localizations')} will improve your server performance.`,
      noSeatbelts: `Removing ${maybePluralPrefixString(count, 'this', 'these')} ${maybePluralString(count, 'archived localization', true)} from the server is permanent - ${maybePluralPrefixString(count, 'it', 'they')} can not be recovered.`,
      removed: `Removed ${maybePluralString(count, 'archived localization', true)}.`,
    }
  }
)
</script>

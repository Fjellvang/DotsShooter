<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Trigger button is an icon.
MIconButton(
  v-if="triggerStyle == 'icon'"
  permission="api.localization.edit"
  :aria-label="!singleLocalizationWithoutContents?.isArchived ? 'Archive this localization.' : 'Unarchive this localization'"
  :disabled-tooltip="singleLocalizationWithoutContents?.isActive ? 'Cannot archive the active localization.' : undefined"
  @click="localizationArchiveModal?.open()"
  data-testid="`archive-localization-${localizationId}`"
  )
  //- Archive icon button.
  fa-icon(
    v-if="!singleLocalizationWithoutContents?.isArchived"
    icon="box-archive"
    class="tw-relative -tw-bottom-[3px] tw-h-4 tw-w-4"
    )

  //- Unarchive icon button.
  svg(
    v-else
    xmlns="http://www.w3.org/2000/svg"
    fill="currentColor"
    class="tw-inline-flex tw-h-4 tw-w-5"
    viewBox="0 0 640 512"
    )
    path(
      d="M256 48c0-26.5 21.5-48 48-48H592c26.5 0 48 21.5 48 48V464c0 26.5-21.5 48-48 48H381.3c1.8-5 2.7-10.4 2.7-16V253.3c18.6-6.6 32-24.4 32-45.3V176c0-26.5-21.5-48-48-48H256V48zM571.3 347.3c6.2-6.2 6.2-16.4 0-22.6l-64-64c-6.2-6.2-16.4-6.2-22.6 0l-64 64c-6.2 6.2-6.2 16.4 0 22.6s16.4 6.2 22.6 0L480 310.6V432c0 8.8 7.2 16 16 16s16-7.2 16-16V310.6l36.7 36.7c6.2 6.2 16.4 6.2 22.6 0zM0 176c0-8.8 7.2-16 16-16H368c8.8 0 16 7.2 16 16v32c0 8.8-7.2 16-16 16H16c-8.8 0-16-7.2-16-16V176zm352 80V480c0 17.7-14.3 32-32 32H64c-17.7 0-32-14.3-32-32V256H352zM144 320c-8.8 0-16 7.2-16 16s7.2 16 16 16h96c8.8 0 16-7.2 16-16s-7.2-16-16-16H144z"
      )
//- Trigger button is a button.
MButton(
  v-else
  permission="api.localization.edit"
  :disabled-tooltip="singleLocalizationWithoutContents?.isActive ? 'Cannot archive the active localization.' : undefined"
  @click="localizationArchiveModal?.open()"
  ) {{ !singleLocalizationWithoutContents?.isArchived ? 'Archive' : 'Unarchive' }}

//- The action modal.
MActionModal(
  ref="localizationArchiveModal"
  :title="!singleLocalizationWithoutContents?.isArchived ? 'Archive Localization' : 'Unarchive Localization'"
  :action="onOk"
  :ok-button-label="!singleLocalizationWithoutContents?.isArchived ? 'Archive' : 'Unarchive'"
  data-testid="`archive-localization-${localizationId}`"
  )
  //- Heading text.
  div(v-if="!singleLocalizationWithoutContents?.isArchived")
    span You are about to archive the localization #[MBadge {{ singleLocalizationWithoutContents?.name }}].&nbsp;
    span Archiving a localization will hide it from the list of available localizations. An archived localization can be unarchived at any time.
  div(v-else)
    span You are about to unarchive the localization #[MBadge {{ singleLocalizationWithoutContents?.name }}].&nbsp;
    span This will make the localization visible in the list of available localizations again.

  //- Optional "archive older localizations" section.
  div(
    v-if="!singleLocalizationWithoutContents?.isArchived && archivableLocalizationIds?.length > 0"
    :class="['tw-mt-4 tw-border tw-rounded-md tw-py-2 tw-px-3 tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600']"
    )
    div(class="tw-flex tw-justify-between")
      div(class="tw-font-semibold") Also archive {{ maybePluralString(archivableLocalizationIds?.length, 'older localization') }}
      MInputSwitch(
        :model-value="archiveOlderLocalizationsState"
        name="archiveAllOlderState"
        size="small"
        @update:model-value="(event) => (archiveOlderLocalizationsState = event)"
        data-testid="localization-archive-all-older-toggle"
        )
    div(class="small text-muted") At the same time as archiving this localization, you can also automatically archive {{ maybePluralString(archivableLocalizationIds?.length, 'older, unpublished localization') }}. This is useful in keeping your localization history manageable.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModal, MBadge, MIconButton, MInputSwitch, MButton, useNotifications } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalLocalizationInfo } from '../../localizationServerTypes'
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  /**
   * ID of the localization to archive.
   */
  localizationId: string
  /**
   * How to show the trigger button.
   */
  triggerStyle: 'icon' | 'button'
}>()

/**
 * Model for the toggle switch.
 */
const archiveOlderLocalizationsState = ref(false)

/**
 * Reference to the localization archive modal.
 */
const localizationArchiveModal = ref<typeof MActionModal>()

/**
 * Fetch all available localizations.
 */
const { data: allLocalizationsData, refresh: allLocalizationsRefresh } = useSubscription(
  getAllLocalizationsSubscriptionOptions()
)

/**
 * Localization data without the detailed content.
 */
const singleLocalizationWithoutContents = computed((): MinimalLocalizationInfo | undefined => {
  if (allLocalizationsData.value) {
    return allLocalizationsData.value.find((x) => x.id === props.localizationId)
  } else {
    return undefined
  }
})

/**
 * Get a list of "archivable" localization IDs. These are localizations that are older than the one we're looking at, and
 * have not been published themselves.
 */
const archivableLocalizationIds = computed((): string[] => {
  const targetLocalization = allLocalizationsData.value?.find(
    (x: MinimalLocalizationInfo) => x.id === props.localizationId
  )
  if (targetLocalization) {
    return (
      (allLocalizationsData.value ?? ([] as MinimalLocalizationInfo[]))
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter(
          (x: MinimalLocalizationInfo) =>
            !x.isArchived &&
            !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive) &&
            x.buildStartedAt < targetLocalization.buildStartedAt
        )
        .map((x: MinimalLocalizationInfo) => x.id)
    )
  } else {
    return []
  }
})

const { showSuccessNotification } = useNotifications()

/**
 * Archive the selected localizations.
 */
async function onOk(): Promise<void> {
  const params = {
    isArchived: !singleLocalizationWithoutContents.value?.isArchived,
  }
  await gameServerApi.post(`/localization/${props.localizationId}`, params)

  if (archiveOlderLocalizationsState.value && archivableLocalizationIds.value.length > 0) {
    await gameServerApi.post('/localization/archive', archivableLocalizationIds.value)
    showSuccessNotification(`Archived ${archivableLocalizationIds.value.length + 1} localizations.`)
  } else {
    showSuccessNotification(`Localization ${params.isArchived ? 'archived' : 'unarchived'}.`)
  }

  allLocalizationsRefresh()
}
</script>

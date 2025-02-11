<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Trigger button is an icon.
MIconButton(
  v-if="triggerStyle === 'icon'"
  permission="api.game_config.edit"
  :aria-label="!singleGameConfigWithoutContents?.isArchived ? 'Archive this game config.' : 'Unarchive this game config'"
  :disabled-tooltip="singleGameConfigWithoutContents?.isActive ? 'Cannot archive the active game config.' : undefined"
  @click="gameConfigArchiveModal?.open"
  data-testid="`archive-config-${gameConfigId}`"
  )
  //- Archive icon button.
  fa-icon(
    v-if="!singleGameConfigWithoutContents?.isArchived"
    icon="box-archive"
    class="tw-size-3.5"
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
  permission="api.game_config.edit"
  :disabled-tooltip="singleGameConfigWithoutContents?.isActive ? 'Cannot archive the active game config.' : undefined"
  full-width
  @click="gameConfigArchiveModal?.open()"
  ) {{ !singleGameConfigWithoutContents?.isArchived ? 'Archive' : 'Unarchive' }}

//- The action modal.
MActionModal(
  ref="gameConfigArchiveModal"
  :title="!singleGameConfigWithoutContents?.isArchived ? 'Archive Game Config' : 'Unarchive Game Config'"
  :action="onOk"
  :ok-button-label="!singleGameConfigWithoutContents?.isArchived ? 'Archive' : 'Unarchive'"
  data-testid="`archive-config-${gameConfigId}`"
  )
  //- Heading text.
  div(v-if="!singleGameConfigWithoutContents?.isArchived")
    span You are about to archive the game config #[MBadge {{ singleGameConfigWithoutContents?.name }}].&nbsp;
    span Archiving a game config will hide it from the list of available game configs. An archived game config can be unarchived at any time.
  div(v-else)
    span You are about to unarchive the game config #[MBadge {{ singleGameConfigWithoutContents?.name }}].&nbsp;
    span This will make the game config visible in the list of available game configs again.

  //- Optional "archive older configs" section.
  div(
    v-if="!singleGameConfigWithoutContents?.isArchived && archivableGameConfigIds?.length > 0"
    :class="['tw-mt-4 tw-border tw-rounded-md tw-py-2 tw-px-3 tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600']"
    )
    div(class="tw-flex tw-justify-between")
      div(class="tw-font-semibold") Also archive {{ maybePluralString(archivableGameConfigIds?.length, 'older game config') }}
      MInputSwitch(
        :model-value="archiveOlderConfigsState"
        name="archiveAllOlderState"
        size="small"
        @update:model-value="(event) => (archiveOlderConfigsState = event)"
        data-testid="config-archive-all-older-toggle"
        )
    div(class="small text-muted") At the same time as archiving this game config, you can also automatically archive {{ maybePluralString(archivableGameConfigIds?.length, 'older, unpublished game config') }}. This is useful in keeping your game config history manageable.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModal, MBadge, MIconButton, MInputSwitch, MButton, useNotifications } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalGameConfigInfo } from '../../gameConfigServerTypes'
import { getAllGameConfigsSubscriptionOptions } from '../../subscription_options/gameConfigs'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  /**
   * ID of the game config to archive.
   */
  gameConfigId: string
  /**
   * How to show the trigger button.
   */
  triggerStyle: 'icon' | 'button'
}>()

/**
 * Model for the toggle switch.
 */
const archiveOlderConfigsState = ref(false)

/**
 * Reference to the game config archive modal.
 */
const gameConfigArchiveModal = ref<typeof MActionModal>()

/**
 * Fetch all available game configs.
 */
const { data: allGameConfigsData, refresh: allGameConfigsRefresh } = useSubscription(
  getAllGameConfigsSubscriptionOptions()
)

/**
 * Game config data without the detailed content.
 */
const singleGameConfigWithoutContents = computed((): MinimalGameConfigInfo | undefined => {
  if (allGameConfigsData.value) {
    return allGameConfigsData.value.find((x) => x.id === props.gameConfigId)
  } else {
    return undefined
  }
})

/**
 * Get a list of "archivable" game config IDs. These are game configs that are older than the one we're looking at, and
 * have not been published themselves.
 */
const archivableGameConfigIds = computed((): string[] => {
  const targetGameConfig = allGameConfigsData.value?.find((x: MinimalGameConfigInfo) => x.id === props.gameConfigId)
  if (targetGameConfig) {
    return (
      (allGameConfigsData.value ?? ([] as MinimalGameConfigInfo[]))
        // Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is also
        // needed here for now. (18/12/23)
        .filter(
          (x: MinimalGameConfigInfo) =>
            !x.isArchived &&
            !(x.publishedAt !== null || x.unpublishedAt !== null || x.isActive) &&
            x.buildStartedAt < targetGameConfig.buildStartedAt
        )
        .map((x: MinimalGameConfigInfo) => x.id)
    )
  } else {
    return []
  }
})

const { showSuccessNotification } = useNotifications()

/**
 * Archive the selected game configs.
 */
async function onOk(): Promise<void> {
  const params = {
    isArchived: !singleGameConfigWithoutContents.value?.isArchived,
  }
  await gameServerApi.post(`/gameConfig/${props.gameConfigId}`, params)

  if (archiveOlderConfigsState.value && archivableGameConfigIds.value.length > 0) {
    await gameServerApi.post('/gameConfig/archive', archivableGameConfigIds.value)
    showSuccessNotification(`Archived ${archivableGameConfigIds.value.length + 1} game configs.`)
  } else {
    showSuccessNotification(`Game config ${params.isArchived ? 'archived' : 'unarchived'}.`)
  }

  allGameConfigsRefresh()
}
</script>

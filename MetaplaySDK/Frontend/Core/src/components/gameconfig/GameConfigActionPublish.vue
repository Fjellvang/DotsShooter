<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTextButton(
  v-if="textButton"
  :disabled-tooltip="bestGuessDisallowPublishReason"
  permission="api.game_config.edit"
  @click="gameConfigPublishModal?.open"
  ) Publish

MButton(
  v-else
  :disabled-tooltip="bestGuessDisallowPublishReason"
  permission="api.game_config.edit"
  full-width
  @click="gameConfigPublishModal?.open()"
  ) Publish

//- Publish game config modal.
MActionModal(
  ref="gameConfigPublishModal"
  title="Publish Game Config"
  :action="publishConfig"
  ok-button-label="Publish Config"
  :ok-button-disabled-tooltip="validatedDisallowPublish ? 'Cannot publish a game config that contains errors.' : undefined"
  @show="onShow"
  )
  div(
    v-if="validatedDisallowPublish === undefined"
    class="my-5 tw-w-full tw-text-center"
    )
    div(class="font-weight-bold") Validating Game Config
    div(class="small text-muted tw-mt-1") Please wait while we check that this config is valid for publishing.
    b-spinner(
      label="Validating..."
      class="tw-mt-3"
      )
  div(v-else-if="validatedDisallowPublish")
    MCallout(
      title="Cannot Publish Game Config"
      variant="danger"
      )
      span This config cannot be validated for publishing because it contains errors.
      span Please view the #[MTextButton(:to="`/gameConfigs/${gameConfigId}`") config's details page] to see the errors.
  div(v-else)
    p Publishing #[MBadge(variant="neutral") {{ gameConfigName }}] will make it the active game config, effective immediately.
    p(class="small text-muted") Other people using the LiveOps Dashboard at the moment may be disrupted by the game data changing while they work, so make sure to let them know you are publishing an update!
    div(class="tw-mb-1 tw-flex tw-justify-between")
      div(class="tw-font-semibold") Force Clients to Update
      MInputSwitch(
        :model-value="kickConnectedClients"
        size="small"
        @update:model-value="(event) => (kickConnectedClients = event)"
        )
    p(class="small text-muted") Players will download the new config the next time they login.
      span(
        v-if="kickConnectedClients"
        class="tw-ml-1"
        ) Currently live players will be immediately disconnected and forced to update to the new config.
      span(
        v-else
        class="tw-ml-1"
        ) Currently live players will continue using the config they started with until the end of their current play session.

  //- Optional "archive older game configs" section.
  div(
    v-if="(archivableGameConfigIds?.length ?? []) > 0"
    :class="['tw-mt-4 tw-border tw-rounded-md tw-py-2 tw-px-3 tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600']"
    )
    div(class="tw-mb-1 tw-flex tw-justify-between")
      div(class="tw-font-semibold") Archive {{ maybePluralString(archivableGameConfigIds?.length, 'Older Unpublished Config') }}
      MInputSwitch(
        :model-value="archiveOlderConfigs"
        size="small"
        @update:model-value="(event) => (archiveOlderConfigs = event)"
        )
    div(class="small text-muted") At the same time as publishing this game config, you can also automatically archive {{ maybePluralString(archivableGameConfigIds?.length, 'older unpublished config') }}. This is useful in keeping your game config history manageable.
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { useUiStore } from '@metaplay/meta-ui'
import {
  MActionModal,
  MBadge,
  MButton,
  MCallout,
  MInputSwitch,
  MTextButton,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { fetchSubscriptionDataOnceOnly, useSubscription } from '@metaplay/subscriptions'

import type { LibraryCountGameConfigInfo, MinimalGameConfigInfo } from '../../gameConfigServerTypes'
import {
  getAllGameConfigsSubscriptionOptions,
  getSingleGameConfigCountsSubscriptionOptions,
} from '../../subscription_options/gameConfigs'

const gameServerApi = useGameServerApi()
const uiStore = useUiStore()

const props = defineProps<{
  /**
   * ID of the game config to publish.
   */
  gameConfigId: string
  /**
   * Optional: Whether to use a text button instead of a regular button.
   */
  textButton?: boolean
  /**
   * Optional: Publish is not possible.
   */
  publishBlocked?: boolean
}>()

/**
 * Reference to the game config publish modal.
 */
const gameConfigPublishModal = ref<typeof MActionModal>()

/**
 * Get a list of "archivable" game config IDs. These are game configs that are older than the one being published, and
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

/**
 * If `true` then we also automatically archive older unpublished game configs when publishing this one.
 */
const archiveOlderConfigs = ref(false)

/**
 * If `true` then kick connected clients to force them to update to this version of the game configs.
 */
const kickConnectedClients = ref(false)

/**
 * Watch for changes in `archiveOlderConfigs` and automatically update the UI store.
 */
watch(
  () => archiveOlderConfigs.value,
  (newValue) => {
    uiStore.toggleAutoArchiveWhenPublishing(newValue)
  }
)

/**
 * Name of the game config.
 */
const gameConfigName = computed(() => {
  return singleGameConfigWithoutContents.value?.name ?? 'No name available'
})

/**
 * Fetch all available game configs
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
 * Returns a reason why this config cannot be published, or undefined if it can. This is based on limited information
 * so it's a best guess only. It's possible that this returns true yet the config can still not be published.
 */
const bestGuessDisallowPublishReason = computed((): string | undefined => {
  if (singleGameConfigWithoutContents.value?.isActive) {
    return 'This game config is already active.'
  } else if (singleGameConfigWithoutContents.value?.publishBlockingErrors.length ?? props.publishBlocked) {
    return 'Cannot publish a game config that contains errors.'
  } else {
    return undefined
  }
})

/**
 * Can the config really be published? This is expensive to fetch, so we only fetch when the modal is opened.
 */
const validatedDisallowPublish = ref<boolean>()

/**
 * Called when the modal is about to be shown.
 */
function onShow(): void {
  // Figure out if the config can really be published. Note that this request will complete almost immediately in some
  // cases, causing a messy visual flick as the loading spinner is shown and then hidden again. To avoid this, we add
  // an artificial short delay first so that the spinner is always visible.
  validatedDisallowPublish.value = undefined
  setTimeout(() => {
    void fetchSubscriptionDataOnceOnly(getSingleGameConfigCountsSubscriptionOptions(props.gameConfigId)).then(
      (data) => {
        validatedDisallowPublish.value = data.publishBlockingErrors.length > 0
      }
    )
  }, 1000)

  // Default value for deleting older archives.
  archiveOlderConfigs.value = uiStore.autoArchiveWhenPublishing
}

const { showSuccessNotification } = useNotifications()

/**
 * Publish the displayed game config to the server.
 */
async function publishConfig(): Promise<void> {
  // Publish the config.
  await gameServerApi.post(
    `/gameConfig/publish?parentMustMatchActive=false&kickConnectedClients=${kickConnectedClients.value}`,
    { Id: props.gameConfigId }
  )
  showSuccessNotification('Game config published.')

  // Archive old configs.
  if (archiveOlderConfigs.value && archivableGameConfigIds.value.length > 0) {
    await gameServerApi.post('/gameConfig/archive', archivableGameConfigIds.value)
    showSuccessNotification(`Archived ${archivableGameConfigIds.value.length} game configs.`)
  }

  // Force reload the page as new configs are now in play.
  // TODO: look into hot-loading the configs instead to solve this for all other dash users as well while making it less intrusive.
  // NOTE: This will lose the toasts that we just created..
  window.location.reload()
}
</script>

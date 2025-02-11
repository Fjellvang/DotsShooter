<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTextButton(
  v-if="textButton"
  :disabled-tooltip="bestGuessDisallowPublishReason"
  permission="api.localization.edit"
  @click="localizationPublishModal?.open"
  data-testid="`publish-localization-${localizationId}`"
  ) Publish

MButton(
  v-else
  :disabled-tooltip="bestGuessDisallowPublishReason"
  permission="api.localization.edit"
  @click="localizationPublishModal?.open()"
  data-testid="`publish-localization-${localizationId}`"
  ) Publish

//- Publish game config modal.
MActionModal(
  ref="localizationPublishModal"
  title="Publish Localization"
  :action="publishLocalization"
  ok-button-label="Publish Localization"
  :ok-button-disabled-tooltip="validatedDisallowPublish ? 'Cannot publish a localization that contains errors.' : undefined"
  @show="onShow"
  data-testid="`publish-localization-${localizationId}`"
  )
  div(
    v-if="validatedDisallowPublish === undefined"
    class="my-5 tw-w-full tw-text-center"
    )
    div(class="font-weight-bold") Validating Localization
    div(class="small text-muted tw-mt-1") Please wait while we check that this localization is valid for publishing.
    b-spinner(
      label="Validating..."
      class="tw-mt-3"
      )
  div(v-else-if="validatedDisallowPublish")
    MCallout(
      title="Cannot Publish Localization"
      variant="danger"
      )
      span This localization cannot be validated for publishing because it contains errors.
      span Please view the #[MTextButton(:to="`/localizations/${localizationId}`") localization's details page] to see the errors.
  div(v-else)
    div Publishing #[MBadge(variant="neutral") {{ localizationName }}] will make it the active localization, effective immediately.

    div(class="small text-muted tw-mt-2")
      | Any player logging in after this publish action will download and use the new localization.

    div(class="small text-muted tw-mt-2")
      | Live players will download the new localization in the background and, depending on the client's
      |         #[span(class="text-monospace small tw-break-all") IMetaplayLocalizationDelegate.AutoActivateLanguageUpdates]
      | setting, will either switch to it immediately when the download completes or on the next login. By default, live players will switch to the new localization only on the next login.

    div(class="small text-muted tw-mt-2")
      | Other people using the LiveOps Dashboard at the moment may be disrupted by the game data changing while they work, so make sure to let them know you are publishing an update!

  //- Optional "archive older localizations" section.
  div(
    v-if="(archivableLocalizationIds?.length ?? []) > 0"
    :class="['tw-mt-4 tw-border tw-rounded-md tw-py-2 tw-px-3 tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600']"
    )
    div(class="tw-mb-1 tw-flex tw-justify-between")
      div(class="tw-font-semibold") Archive {{ maybePluralString(archivableLocalizationIds?.length, 'Older Unpublished Localization') }}
      MInputSwitch(
        :model-value="archiveOlderLocalizations"
        size="small"
        @update:model-value="(event) => (archiveOlderLocalizations = event)"
        )
    div(class="small text-muted") At the same time as publishing this localization, you can also automatically archive {{ maybePluralString(archivableLocalizationIds?.length, 'older unpublished localization') }}. This is useful in keeping your localization history manageable.
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { useUiStore } from '@metaplay/meta-ui'
import {
  MActionModal,
  MBadge,
  MCallout,
  MInputSwitch,
  MButton,
  MTextButton,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalLocalizationInfo } from '../../localizationServerTypes'
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'

const gameServerApi = useGameServerApi()
const uiStore = useUiStore()

const props = defineProps<{
  /**
   * ID of the localization to publish.
   */
  localizationId: string
  /**
   * Optional: Whether to use a link button instead of a regular button.
   */
  link?: boolean
  /**
   * Optional: Publish is not possible.
   */
  publishBlocked?: boolean
  /**
   * Optional: Whether to use a text button instead of a regular button.
   */
  textButton?: boolean
}>()

/**
 * Reference to the game config publish modal.
 */
const localizationPublishModal = ref<typeof MActionModal>()

/**
 * Get a list of "archivable" localization IDs. These are localizations that are older than the one being published, and
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

/**
 * If `true` then we also automatically archive older unpublished localizations when publishing this one.
 */
const archiveOlderLocalizations = ref(false)

/**
 * Watch for changes in `archiveOlderLocalizations` and automatically update the UI store.
 */
watch(
  () => archiveOlderLocalizations.value,
  (newValue) => {
    uiStore.toggleAutoArchiveWhenPublishing(newValue)
  }
)

/**
 * Name of the localization.
 */
const localizationName = computed(() => {
  return singleLocalizationWithoutContents.value?.name ?? 'No name available'
})

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
 * Returns a reason why this localization cannot be published, or undefined if it can. This is based on limited information
 * so it's a best guess only. It's possible that this returns true yet the localization can still not be published.
 */
const bestGuessDisallowPublishReason = computed((): string | undefined => {
  if (singleLocalizationWithoutContents.value?.isActive) {
    return 'This localization is already active.'
  } else if (singleLocalizationWithoutContents.value?.publishBlockingErrors.length ?? props.publishBlocked) {
    return 'Cannot publish a localization that contains errors.'
  } else {
    return undefined
  }
})

/**
 * Can the localization really be published? This is expensive to fetch, so we only fetch when the modal is opened.
 */
const validatedDisallowPublish = ref<boolean>()

/**
 * Called when the modal is about to be shown.
 */
function onShow(): void {
  // Figure out if the localization can really be published. Note that this request will complete almost immediately in some
  // cases, causing a messy visual flick as the loading spinner is shown and then hidden again. To avoid this, we add
  // an artificial short delay first so that the spinner is always visible.
  validatedDisallowPublish.value = undefined
  setTimeout(() => {
    // In game configs we have to actually make a request to server to check if the config can be published. We don't
    // have that here, so we just set it to true if there are any errors. We want to keep the same flow in this
    // component (ie: the `validation` state) so that the two components remain broadly similar.
    validatedDisallowPublish.value = !!singleLocalizationWithoutContents.value?.publishBlockingErrors.length
  }, 1000)

  // Default value for deleting older archives.
  archiveOlderLocalizations.value = uiStore.autoArchiveWhenPublishing
}

const { showSuccessNotification } = useNotifications()

/**
 * Publish the displayed localization to the server.
 */
async function publishLocalization(): Promise<void> {
  // Publish the localization.
  await gameServerApi.post('/localization/publish?parentMustMatchActive=false', { Id: props.localizationId })
  showSuccessNotification('Localization published.')

  // Archive old localizations.
  if (archiveOlderLocalizations.value && archivableLocalizationIds.value.length > 0) {
    await gameServerApi.post('/localization/archive', archivableLocalizationIds.value)
    showSuccessNotification(`Archived ${archivableLocalizationIds.value.length} localizations.`)
  }

  // Force reload the page as new localizations are now in play.
  // TODO: look into hot-loading the localizations instead to solve this for all other dash users as well while making it less intrusive.
  // NOTE: This will lose the toasts that we just created..
  window.location.reload()
}
</script>

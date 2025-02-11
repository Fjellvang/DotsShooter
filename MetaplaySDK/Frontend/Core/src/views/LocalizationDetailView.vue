<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :variant="errorVariant"
  :is-loading="!localizationData"
  :error="allLocalizationsError ?? localizationMissingError"
  full-width
  :alerts="alerts"
  permission="api.localization.view"
  )
  template(#overview)
    MPageOverviewCard(
      v-if="localizationData"
      :title="localizationData.name"
      :subtitle="localizationData.description"
      :id="localizationId"
      data-testid="localization-detail-overview-card"
      )
      //- Overview.
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Status
            b-td(class="text-right")
              MBadge(
                v-if="localizationData.isArchived"
                variant="neutral"
                class="tw-mr-1"
                ) Archived
              MBadge(
                v-if="localizationData.isActive"
                variant="success"
                class="tw-mr-1"
                ) Active
              MBadge(v-else) Not active
          b-tr
            b-td Last Published At
            b-td(class="text-right")
              meta-time(
                v-if="localizationData?.publishedAt"
                :date="localizationData?.publishedAt"
                showAs="timeagoSentenceCase"
                )
              //- Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is
              //- also needed here for now. (18/12/23)
              span(
                v-else-if="localizationData?.isActive"
                class="text-muted tw-italic"
                ) No time recorded
              span(
                v-else
                class="text-muted tw-italic"
                ) Never
          b-tr
            b-td Last Unpublished At
            b-td(class="text-right")
              meta-time(
                v-if="localizationData?.unpublishedAt"
                :date="localizationData?.unpublishedAt"
                showAs="timeagoSentenceCase"
                )
              span(
                v-else
                class="text-muted tw-italic"
                ) Never

      //- Build Status.
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Build Status
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Build Status
            b-td(class="text-right")
              div {{ localizationData?.bestEffortStatus }}
          b-tr
            b-td Built By
            b-td(class="text-right")
              MBadge(v-if="localizationData.source === 'disk'") Built-in with the server
              meta-username(
                v-else
                :username="localizationData.source"
                )
          b-tr(:class="{ 'text-danger': localizationData.publishBlockingErrors.length > 0 }")
            b-td Publish Blocking Errors
            b-td(class="text-right")
              span(v-if="localizationData.publishBlockingErrors.length") {{ localizationData.publishBlockingErrors.length }}
              span(
                v-else
                class="text-muted tw-italic"
                ) None

      //- Technical Details.
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Technical Details
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Built At
            b-td(class="text-right") #[meta-time(:date="localizationData?.buildStartedAt" showAs="timeagoSentenceCase")]
          b-tr
            b-td Last Modified At
            b-td(class="text-right") #[meta-time(v-if="localizationData" :date="localizationData?.lastModifiedAt" showAs="timeagoSentenceCase")]
          b-tr
            b-td Version Hash
            b-td(
              v-if="!localizationData"
              class="text-right text-muted tw-italic"
              ) Loading...
            b-td(
              v-else
              class="text-right"
              )
              div(class="text-monospace small") {{ localizationData.versionHash }}

      template(#buttons)
        localization-action-archive(
          :localizationId="localizationId"
          trigger-style="button"
          )

        MActionModalButton(
          modal-title="Edit Localization Archive"
          :action="sendUpdatedLocalizationDataToServer"
          trigger-button-label="Edit"
          ok-button-label="Update"
          permission="api.localization.edit"
          @show="resetForm"
          data-testid="edit-config"
          )
          MInputText(
            label="Name"
            :model-value="editModalConfig.name"
            :variant="editModalConfig.name.length > 0 ? 'success' : 'default'"
            placeholder="For example: 1.0.4 release candidate"
            class="tw-mb-2"
            @update:model-value="editModalConfig.name = $event"
            )

          MInputTextArea(
            label="Description"
            :model-value="editModalConfig.description"
            :variant="editModalConfig.description.length > 0 ? 'success' : 'default'"
            placeholder="What is unique about this config build that will help you find it later?"
            :rows="3"
            @update:model-value="editModalConfig.description = $event"
            )
          div(class="tw-inline-flex tw-w-full tw-items-center tw-justify-between")
            span(class="tw-font-semibold") Archived
            MTooltip(
              :content="!canArchive ? 'This config cannot be archived as it is currently active.' : undefined"
              no-underline
              )
              MInputSwitch(
                :model-value="editModalConfig.isArchived"
                :disabled="!canArchive"
                name="isConfigBuildArchived"
                size="small"
                @update:model-value="editModalConfig.isArchived = $event"
                )
          span(class="small text-muted") Archived localization builds are hidden from the localizations list by default. Localization builds that are active cannot be archived.

        MButton(
          :disabled-tooltip="disallowDiffToActiveReason"
          :to="`diff?newRoot=${localizationId}`"
          ) Diff to Active

        localization-action-publish(
          :localizationId="localizationId"
          :publishBlocked="localizationData?.publishBlockingErrors.length > 0"
          )

  template(#default)
    MTabLayout(:tabs="tabOptions")
      template(#tab-0)
        core-ui-placement(
          :placementId="tabUiPlacements[0]"
          :localizationId="localizationId"
          alwaysFullWidth
          )
      template(#tab-1)
        core-ui-placement(
          :placementId="tabUiPlacements[1]"
          :localizationId="localizationId"
          )

    MetaRawData(
      :kvPair="localizationData"
      name="localizationData"
      )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaRawData } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MBadge,
  MButton,
  MInputSwitch,
  MInputText,
  MInputTextArea,
  MTooltip,
  useHeaderbar,
  MPageOverviewCard,
  MViewContainer,
  type MViewContainerAlert,
  DisplayError,
  useNotifications,
  MTabLayout,
  type TabOption,
} from '@metaplay/meta-ui-next'
import { sentenceCaseToKebabCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import LocalizationActionArchive from '../components/localization/LocalizationActionArchive.vue'
import LocalizationActionPublish from '../components/localization/LocalizationActionPublish.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { useCoreStore } from '../coreStore'
import { routeParamToSingleValue } from '../coreUtils'
import type { UiPlacement } from '../integration_api/uiPlacementApis'
import { getAllLocalizationsSubscriptionOptions } from '../subscription_options/localization'

const gameServerApi = useGameServerApi()
const route = useRoute()
const coreStore = useCoreStore()

// Load localization data ----------------------------------------------------------------------------------------------

/**
 * Id of localization that is to be displayed.
 */
const localizationId = routeParamToSingleValue(route.params.id)

/**
 * Fetch all available localizations
 */
const {
  data: allLocalizationsData,
  refresh: allLocalizationsRefresh,
  error: allLocalizationsError,
} = useSubscription(getAllLocalizationsSubscriptionOptions())

/**
 * Localization data to be displayed. We pull this from the list of all localizations.
 */
const localizationData = computed(() => {
  return allLocalizationsData.value?.find((localization) => localization.id === localizationId)
})

/**
 * A synthetic error for when the localization is not found.
 */
const localizationMissingError = computed(() => {
  if (!allLocalizationsData.value?.find((localization) => localization.id === localizationId)) {
    // Localization does not exist.
    return new DisplayError(
      'Localization Not Found',
      `The localization with the ID '${localizationId}' does not exist. Are you looking in the right deployment?.`
    )
  } else {
    // Localization exists.
    return undefined
  }
})

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(
  localizationData,
  (localizationRef) => `View ${localizationRef.value?.name ?? 'Localization'}`
)

// UI Alerts ----------------------------------------------------------------------------------------------------------

/**
 * Array of error messages to be displayed in the event something goes wrong.
 */
const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  if (localizationData.value?.publishBlockingErrors.length) {
    allAlerts.push({
      title: 'Build Cannot Be Published',
      message: 'This build contains errors and cannot be published.',
      variant: 'danger',
      dataTest: 'cannot-publish-alert',
    })
  }

  if (localizationData.value?.bestEffortStatus === 'Building') {
    allAlerts.push({
      title: 'Config building...',
      message: 'This localization is still building and has no content to view for now.',
      variant: 'warning',
      dataTest: 'building-alert',
    })
  }
  return allAlerts
})

/**
 * Custom background color that indicates the type of alert message.
 */
const errorVariant = computed(() => {
  if (alerts.value.find((alert) => alert.variant === 'danger')) return 'danger'
  else if (alerts.value.find((alert) => alert.variant === 'warning')) {
    return 'warning'
  } else return undefined
})

// Modify the localization --------------------------------------------------------------------------------------------

/**
 * Information that is to be modified in the localization modal.
 */
interface LocalizationModalInfo {
  /**
   * Display name of the localization.
   */
  name: string
  /**
   * Optional description of what is unique about the localization build.
   */
  description: string
  /**
   * Indicates whether the localization has been archived.
   */
  isArchived: boolean
}

/**
 * Localization data to be modified in the modal.
 */
const editModalConfig = ref<LocalizationModalInfo>({
  name: '',
  description: '',
  isArchived: false,
})

/**
 * Reset edit modal.
 */
function resetForm(): void {
  editModalConfig.value = {
    name: localizationData.value?.name ?? '',
    description: localizationData.value?.description ?? '',
    isArchived: localizationData.value?.isArchived ?? false,
  }
}

const { showSuccessNotification } = useNotifications()

/**
 * Take localization build data from the modal and send it to the server.
 */
async function sendUpdatedLocalizationDataToServer(): Promise<void> {
  const params = {
    name: editModalConfig.value.name,
    description: editModalConfig.value.description,
    isArchived: editModalConfig.value.isArchived,
  }
  await gameServerApi.post(`/localization/${localizationId}`, params)
  showSuccessNotification('Localization updated.')
  allLocalizationsRefresh()
}

/**
 * Returns a reason why this localization cannot be diffed against the active localization, or undefined if it can.
 */
const disallowDiffToActiveReason = computed((): string | undefined => {
  if (localizationData.value?.isActive) {
    return 'Cannot diff this localization against itself.'
  } else if (localizationData.value?.bestEffortStatus !== 'Success') {
    return 'Cannot diff a localization that is not in a valid state.'
  } else {
    return undefined
  }
})

/**
 * Can the displayed localization can be archived?
 */
const canArchive = computed(() => {
  return !localizationData.value?.isActive
})

// Tabs ---------------------------------------------------------------------------------------------------------------

const tabOptions: TabOption[] = [{ label: 'Details' }, { label: 'Audit Log' }]

const tabUiPlacements: UiPlacement[] = ['Localizations/Details/Tab0', 'Localizations/Details/Tab1']
</script>

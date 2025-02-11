<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :variant="errorVariant"
  :is-loading="!gameConfigData"
  :error="gameConfigError"
  full-width
  :alerts="alerts"
  permission="api.game_config.view"
  )
  template(#overview)
    MPageOverviewCard(
      v-if="gameConfigData"
      :title="gameConfigData.name"
      :subtitle="gameConfigData.description"
      :id="gameConfigId"
      data-testid="game-config-detail-overview-card"
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
            b-td(class="tw-text-right")
              MBadge(
                v-if="gameConfigData.isArchived"
                variant="neutral"
                class="tw-mr-1"
                ) Archived
              MBadge(
                v-if="gameConfigData.isActive"
                variant="success"
                class="tw-mr-1"
                ) Active
              MBadge(v-else) Not active
          b-tr
            b-td Experiments
            b-td(
              v-if="totalExperiments === undefined"
              class="text-right text-muted tw-italic"
              ) Loading...
            b-td(
              v-else-if="totalExperiments > 0"
              class="tw-text-right"
              ) {{ totalExperiments }}
            b-td(
              v-else
              class="text-right text-muted tw-italic"
              ) None
          b-tr
            b-td Last Published At
            b-td(class="tw-text-right")
              meta-time(
                v-if="gameConfigData?.publishedAt"
                :date="gameConfigData?.publishedAt"
                showAs="timeagoSentenceCase"
                )
              //- Legacy builds may not have a publishedAt date, even though they are active, so the isActive check is
              //- also needed here for now. (18/12/23)
              span(
                v-else-if="gameConfigData?.isActive"
                class="text-muted tw-italic"
                ) No time recorded
              span(
                v-else
                class="text-muted tw-italic"
                ) Never
          b-tr
            b-td Last Unpublished At
            b-td(class="tw-text-right")
              meta-time(
                v-if="gameConfigData?.unpublishedAt"
                :date="gameConfigData?.unpublishedAt"
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
            b-td(class="tw-text-right")
              MBadge(
                v-if="gameConfigData?.status === 'Success'"
                variant="success"
                ) {{ gameConfigData?.status }}
              MBadge(
                v-if="gameConfigData?.status === 'Failed'"
                variant="danger"
                ) {{ gameConfigData?.status }}
              MBadge(
                v-if="gameConfigData?.status === 'Building'"
                variant="primary"
                ) {{ gameConfigData?.status }}
          b-tr
            b-td Built By
            b-td(class="tw-text-right")
              MBadge(v-if="gameConfigData.source === 'disk'") Built-in with the server
              meta-username(
                v-else
                :username="gameConfigData.source"
                )
          b-tr(:class="{ 'text-danger': gameConfigData.buildReportSummary?.totalLogLevelCounts.Error }")
            b-td Logged Errors
            b-td(
              v-if="gameConfigData?.buildReportSummary?.totalLogLevelCounts.Error"
              class="tw-text-right"
              ) {{ gameConfigData.buildReportSummary?.totalLogLevelCounts.Error }}
            b-td(
              v-else-if="gameConfigData?.buildReportSummary === null"
              class="text-right text-muted tw-italic"
              ) Not available
            b-td(
              v-else
              class="text-right text-muted tw-italic"
              ) None
          b-tr(:class="{ 'text-warning': gameConfigData?.buildReportSummary?.totalLogLevelCounts.Warning }")
            b-td Logged Warnings
            b-td(
              v-if="gameConfigData?.buildReportSummary?.totalLogLevelCounts.Warning"
              class="tw-text-right"
              ) {{ gameConfigData.buildReportSummary?.totalLogLevelCounts.Warning }}
            b-td(
              v-else-if="gameConfigData?.buildReportSummary === null"
              class="text-right text-muted tw-italic"
              ) Not available
            b-td(
              v-else
              class="text-right text-muted tw-italic"
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
            b-td(class="tw-text-right") #[meta-time(:date="gameConfigData?.buildStartedAt" showAs="timeagoSentenceCase")]
          b-tr
            b-td Last Modified At
            b-td(class="tw-text-right") #[meta-time(v-if="gameConfigData" :date="gameConfigData?.lastModifiedAt" showAs="timeagoSentenceCase")]
          b-tr
            b-td Full Config Archive Version
            b-td(
              v-if="!gameConfigData"
              class="text-right text-muted tw-italic"
              ) Loading...
            b-td(
              v-else
              class="tw-text-right"
              )
              div(
                v-if="gameConfigData?.fullConfigVersion"
                class="text-monospace small"
                ) {{ gameConfigData.fullConfigVersion }}
              div(
                v-else
                class="text-muted tw-italic"
                ) Not available
          b-tr
            b-td Client Facing Version
            b-td(
              v-if="!gameConfigData"
              class="text-right text-muted tw-italic"
              ) Loading...
            b-td(
              v-else
              class="tw-text-right"
              )
              div(
                v-if="gameConfigData?.cdnVersion"
                class="text-monospace small"
                ) {{ gameConfigData.cdnVersion }}
              MTooltip(
                v-else
                content="Only available for the currently active game config."
                class="text-muted tw-italic"
                ) Not available

      template(#buttons)
        game-config-action-archive(
          v-if="gameConfigId"
          :gameConfigId="gameConfigId"
          trigger-style="button"
          )

        MActionModalButton(
          modal-title="Edit Game Config Archive"
          :action="sendUpdatedConfigDataToServer"
          trigger-button-label="Edit"
          ok-button-label="Update"
          permission="api.game_config.edit"
          trigger-button-full-width
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

        MButton(
          :disabled-tooltip="disallowDiffToActiveReason"
          :to="`diff?newRoot=${gameConfigId}`"
          full-width
          ) Diff to Active

        game-config-action-publish(
          v-if="gameConfigId"
          :gameConfigId="gameConfigId"
          :publishBlocked="gameConfigData?.publishBlockingErrors.length > 0"
          )

  template(#default)
    MTabLayout(
      :tabs="tabOptions"
      :current-tab="currentTab"
      )
      template(#tab-0)
        core-ui-placement(
          :placementId="tabUiPlacements[0]"
          :gameConfigId="gameConfigId"
          alwaysFullWidth
          )
      template(#tab-1)
        core-ui-placement(
          :placementId="tabUiPlacements[1]"
          :gameConfigId="gameConfigId"
          )
      template(#tab-2)
        core-ui-placement(
          :placementId="tabUiPlacements[2]"
          :gameConfigId="gameConfigId"
          )

    MetaRawData(
      :kvPair="gameConfigData"
      name="gameConfigData"
      )
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MetaRawData } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MBadge,
  MButton,
  MInputText,
  MInputTextArea,
  MTooltip,
  useHeaderbar,
  type MViewContainerAlert,
  MViewContainer,
  MPageOverviewCard,
  useNotifications,
  MTabLayout,
  type TabOption,
} from '@metaplay/meta-ui-next'
import { maybePluralPrefixString, maybePluralString, sentenceCaseToKebabCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import GameConfigActionArchive from '../components/gameconfig/GameConfigActionArchive.vue'
import GameConfigActionPublish from '../components/gameconfig/GameConfigActionPublish.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { useCoreStore } from '../coreStore'
import { routeParamToSingleValue } from '../coreUtils'
import type { UiPlacement } from '../integration_api/uiPlacementApis'
import {
  getAllGameConfigsSubscriptionOptions,
  getSingleGameConfigCountsSubscriptionOptions,
} from '../subscription_options/gameConfigs'

const gameServerApi = useGameServerApi()
const route = useRoute()
const coreStore = useCoreStore()

// Load game config data ----------------------------------------------------------------------------------------------

/**
 *  There are two sources of information for this page:
 * 1 - We subscribe to all gameconfig data and pull out just the game config that we're interested in. This loads fast
 *     and allows us to show the overview card very quickly while..
 * 2 - ..we are also subscribed to the full data for the game config that we're interested in. This is much slower to
 *    load because it includes the archive contents. We only need this contents for the the experiments info on the
 *    overview card so we don't want to have to wait for it to be loaded.
 */

/**
 * Reference to the game config archive modal.
 */
const gameConfigArchiveModalRef = ref()

/**
 * Fetch all available game configs.
 */
const { refresh: allGameConfigsRefresh } = useSubscription(getAllGameConfigsSubscriptionOptions())

/**
 * Fetch data for the specific game config that we are viewing.
 */
const {
  data: gameConfigData,
  error: gameConfigError,
  refresh: gameConfigRefresh,
} = useSubscription(getSingleGameConfigCountsSubscriptionOptions(routeParamToSingleValue(route.params.id)))

/**
 * Id of game config that we are viewing. Note that we don't take this from the route, but from the fetched data
 * itself. This is because the route might be `$active`, which the server then translates into a proper GUID for us.
 */
const gameConfigId = computed(() => gameConfigData.value?.id)

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(gameConfigData, (gameConfigRef) => `View ${gameConfigRef.value?.name ?? 'Config'}`)

/**
 * Experiment data.
 */
const totalExperiments = computed(() => {
  return gameConfigData.value?.contents.serverLibraries.PlayerExperiments ?? 0
})

// UI Alerts ----------------------------------------------------------------------------------------------------------

/**
 * Array of error messages to be displayed in the event something goes wrong.
 */
const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  if (gameConfigData.value?.publishBlockingErrors.length) {
    allAlerts.push({
      title: 'Unpublishable Game Config',
      message: 'This game config has errors and can not be published.',
      variant: 'danger',
      dataTest: 'cannot-publish-alert',
    })
  }

  if (gameConfigData.value?.status === 'Building') {
    allAlerts.push({
      title: 'Config Building...',
      message: 'This game config is still building and has no content to view for now.',
      variant: 'warning',
      dataTest: 'building-alert',
    })
  } else if (
    gameConfigData.value?.libraryImportErrors &&
    Object.keys(gameConfigData.value.libraryImportErrors).length > 0
  ) {
    allAlerts.push({
      title: 'Library Errors',
      message: 'One or more libraries failed to import.',
      variant: 'danger',
      dataTest: 'libraries-fail-to-parse-alert',
    })
  } else if (gameConfigData.value?.buildReportSummary?.totalLogLevelCounts.Warning) {
    const warningCount = gameConfigData.value.buildReportSummary.totalLogLevelCounts.Warning ?? 0
    allAlerts.push({
      title: `${gameConfigData.value.buildReportSummary.totalLogLevelCounts.Warning} Build Warnings`,
      message: `There ${maybePluralPrefixString(warningCount, 'was', 'were')} ${maybePluralString(warningCount, 'warning')} when building this config.
        You can still publish it, but it may not work as expected. You can view the full build log for more information.`,
      variant: 'warning',
      dataTest: 'build-warnings-alert',
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

// Modify the game config ---------------------------------------------------------------------------------------------

/**
 * Information that is to be modified in the game config modal.
 */
interface GameConfigModalInfo {
  /**
   * Display name of the game config.
   */
  name: string
  /**
   * Optional description of what is unique about the game config build.
   */
  description: string
  /**
   * Indicates whether the game config has been archived.
   */
  isArchived: boolean
}

/**
 * Game config data to be modified in the modal.
 */
const editModalConfig = ref<GameConfigModalInfo>({
  name: '',
  description: '',
  isArchived: false,
})

/**
 * Reset edit modal.
 */
function resetForm(): void {
  editModalConfig.value = {
    name: gameConfigData.value?.name ?? '',
    description: gameConfigData.value?.description ?? '',
    isArchived: gameConfigData.value?.isArchived ?? false,
  }
}

const { showSuccessNotification } = useNotifications()

/**
 * Take game config build data from the modal and send it to the server.
 */
async function sendUpdatedConfigDataToServer(): Promise<void> {
  const params = {
    name: editModalConfig.value.name,
    description: editModalConfig.value.description,
    isArchived: editModalConfig.value.isArchived,
  }
  await gameServerApi.post(`/gameConfig/${gameConfigId.value}`, params)
  showSuccessNotification('Game config updated.')
  allGameConfigsRefresh()
  gameConfigRefresh()
}

/**
 * Returns a reason why this config cannot be diffed against the active config, or undefined if it can.
 */
const disallowDiffToActiveReason = computed((): string | undefined => {
  if (gameConfigData.value?.isActive) {
    return 'Cannot diff this config against itself.'
  } else if (gameConfigData.value?.status === 'Building') {
    return "Cannot diff a config while it's being built"
  } else if (!gameConfigData.value?.fullConfigVersion) {
    return 'Cannot diff a config that failed to build.'
  } else {
    return undefined
  }
})

/**
 * Can the displayed game config can be archived?
 */
const canArchive = computed(() => {
  return !gameConfigData.value?.isActive
})

// Tabs ---------------------------------------------------------------------------------------------------------------

/**
 * By default set initial tab to 0 to show the detail tab.
 */
const currentTab = ref(0)

// If there are errors, switch to the build log tab.
// Note: This used to be `{ once: true }`, but in order for this work consistently we need to use `{ immediate: true }` instead.
watch(
  gameConfigData,
  (newValue) => {
    if (newValue && newValue.publishBlockingErrors.length > 0 && !route.query.tab) {
      for (const error of newValue.publishBlockingErrors) {
        if (error.errorType === 'BlockingMessages') {
          currentTab.value = 1 // Switch to the Build Log tab
          break
        }
      }
    }
  },
  { immediate: true }
)

const tabOptions = computed((): TabOption[] => [
  { label: 'Details' },
  {
    label: 'Build Log',
    highlighted: gameConfigData.value && gameConfigData.value.publishBlockingErrors.length > 0,
  },
  { label: 'Audit Log' },
])

const tabUiPlacements: UiPlacement[] = [
  'GameConfigs/Details/Tab0',
  'GameConfigs/Details/Tab1',
  'GameConfigs/Details/Tab2',
]
</script>

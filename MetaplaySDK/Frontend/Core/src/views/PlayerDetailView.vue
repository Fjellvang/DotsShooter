<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  full-width
  :variant="viewVariant"
  :alerts="alerts"
  :is-loading="fetchStatus === 'loading' || !dashboardOptionsData"
  )
  //- Errors when fetching data.
  template(
    v-if="fetchStatus.includes('error')"
    #errors
    )
    MErrorCallout(
      v-if="fetchStatus === 'error-bad-id' || fetchStatus === 'error-unknown'"
      :error="singlePlayerError"
      )
    MCallout(
      v-else-if="fetchStatus === 'error-not-found'"
      title="Player Not Found"
      variant="danger"
      ) This player does not exist. Are you looking in the correct deployment?
    MCallout(
      v-else-if="fetchStatus === 'error-entity-uninitialized'"
      title="Player Not Initialized"
      variant="danger"
      ) This player has not yet been initialized. This is likely a temporary state for a new player, but indicates a problem if the state persists.

    //- Give access to additional debugging tools when they are relevant.
    MCallout(
      v-if="fetchStatus === 'error-unknown' || fetchStatus === 'error-entity-uninitialized'"
      title="Debugging"
      variant="danger"
      class="tw-mt-3"
      )
      p The following tools may be able to give you more information about this error:
      ul
        li Use #[MTextButton(:to="`/players/${id}/raw`") raw query debugger] to inspect data retrieval phases.
        li Use #[MTextButton(:to="`/entities/${id}/dbinfo`" permission="api.database.inspect_entity" data-testid="model-size-link") save file inspector] to inspect entity data saved on the database.
      div(class="d-flex justify-content-end tw-mt-4")
        player-action-force-reset(:playerId="id")
        player-action-export(
          :playerId="id"
          allow-debug-export
          class="ml-2"
          )

  //- Scheduled deletion alert (complex body -> using a slot)
  template(#alerts)
    meta-alert(
      v-if="singlePlayerData?.model.deletionStatus.startsWith('ScheduledBy')"
      variant="danger"
      title="Scheduled for Deletion"
      data-testid="player-deletion-alert"
      )
      div
        MBadge(variant="danger") {{ playerName }}
        span is scheduled to be deleted
          |#[span(class="font-weight-bold") #[meta-time(:date="singlePlayerData.model.scheduledForDeletionAt" showAs="timeago")]]
          |
          | at #[meta-time(:date="singlePlayerData.model.scheduledForDeletionAt" showAs="time" disableTooltip)]
          |
          | on #[meta-time(:date="singlePlayerData.model.scheduledForDeletionAt" showAs="date" disableTooltip)].
        span Deletion was
        span(v-if="singlePlayerData.model.deletionStatus === 'ScheduledByAdmin'") scheduled by an admin.
        span(v-else-if="singlePlayerData.model.deletionStatus === 'ScheduledByUser'") requested in-game by the player.
        span(v-else) scheduled by an automated system.
      div(
        v-if="new Date(singlePlayerData.model.scheduledForDeletionAt) < new Date()"
        class="mt-2 small text-muted tw-italic"
        )
        | Note: Players are deleted with batch jobs that run every now and then. It is normal for players to get deleted up to a day after their due date!

  //- Overview card
  template(#overview)
    player-overview-card(:playerId="id")

  //- Admin actions card
  div(
    v-if="!singlePlayerData?.model.deletionStatus.startsWith('Deleted')"
    class="tw-mx-auto tw-mb-10 tw-max-w-3xl tw-@container"
    )
    player-admin-actions-card(:playerId="id")

  //- Tabs
  div(
    v-if="singlePlayerData && dashboardOptionsData && !singlePlayerData.model.deletionStatus.startsWith('Deleted')"
    class="mb-5"
    )
    MTabLayout(:tabs="tabOptions")
      template(#tab-0)
        core-ui-placement(
          :placementId="tabUiPlacements[0]"
          :playerId="id"
          )
      template(#tab-1)
        core-ui-placement(
          :placementId="tabUiPlacements[1]"
          :playerId="id"
          )
      template(#tab-2)
        core-ui-placement(
          :placementId="tabUiPlacements[2]"
          :playerId="id"
          )
      template(#tab-3)
        core-ui-placement(
          :placementId="tabUiPlacements[3]"
          :playerId="id"
          )

        //- TODO: this way of auto-discovering activables no longer feels kosher. Re-design?
        div(class="tw-my-3 tw-grid tw-grid-cols-1 tw-gap-3 lg:tw-grid-cols-2")
          PlayerLiveOpsEventCard(
            v-if="staticInfos.featureFlags.liveOpsEvents"
            :playerId="id"
            )

          div(
            v-for="(title, category) in activableCategories"
            :key="title"
            )
            offer-groups-card(
              v-if="category === 'OfferGroup'"
              hideDisabled
              hideConversion
              :playerId="id"
              :title="title"
              :emptyMessage="`${playerName} doesn't have any offers available.`"
              :defaultSortOption="3"
              hidePriority
              hideRevenue
              class="tw-h-full"
              )
            meta-generic-activables-card(
              v-else
              hideDisabled
              hideConversion
              :playerId="id"
              :category="String(category)"
              :title="title"
              :emptyMessage="`There are no ${title} defined.`"
              class="tw-h-full"
              )
      template(#tab-4)
        core-ui-placement(
          :placementId="tabUiPlacements[4]"
          :playerId="id"
          )

        //- Player pretty print
        MetaRawData(
          :kvPair="singlePlayerData"
          name="singlePlayerData"
          )
        MetaRawData(:kvPair="dashboardOptionsData")
</template>

<script lang="ts" setup>
import { AxiosError } from 'axios'
import { computed } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import { MetaRawData } from '@metaplay/meta-ui'
import {
  MBadge,
  MCallout,
  MErrorCallout,
  MTextButton,
  MViewContainer,
  useHeaderbar,
  type MViewContainerAlert,
  MTabLayout,
  type TabOption,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaGenericActivablesCard from '../components/activables/MetaGenericActivablesCard.vue'
import OfferGroupsCard from '../components/offers/OfferGroupsCard.vue'
import PlayerAdminActionsCard from '../components/playerdetails/PlayerAdminActionsCard.vue'
import PlayerLiveOpsEventCard from '../components/playerdetails/PlayerLiveOpsEventCard.vue'
import PlayerOverviewCard from '../components/playerdetails/PlayerOverviewCard.vue'
import PlayerActionExport from '../components/playerdetails/adminactions/PlayerActionExport.vue'
import PlayerActionForceReset from '../components/playerdetails/adminactions/PlayerActionForceReset.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import type { UiPlacement } from '../integration_api/uiPlacementApis'
import {
  getDashboardOptionsSubscriptionOptions,
  getStaticConfigSubscriptionOptions,
} from '../subscription_options/general'
import { getSinglePlayerSubscriptionOptions } from '../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player to show.
   */
  id: string
}>()

const { data: singlePlayerData, error: singlePlayerError } = useSubscription(
  getSinglePlayerSubscriptionOptions(props.id)
)
const playerName = computed((): string => singlePlayerData.value?.model?.playerName || 'n/a')

const staticInfos = useStaticInfos()

/**
 * A computed status of the data fetch based on the data and error values. This splits the status into three broad
 * categories: `loading`, `success`, and `error`. The error category is further split into subcategories by determining
 * the specific type of error.
 */
const fetchStatus = computed(
  (): 'loading' | 'success' | 'error-bad-id' | 'error-not-found' | 'error-unknown' | 'error-entity-uninitialized' => {
    if (singlePlayerError.value) {
      const axiosError = singlePlayerError.value as AxiosError
      if (axiosError.response?.status === 400) {
        // Server returned 400 Bad Request.
        return 'error-bad-id'
      } else if (axiosError.response?.status === 404) {
        // Server return 404 Not Found.
        return 'error-not-found'
      } else {
        // Server returned any other type of error, likely a 500 Internal Server Error.
        return 'error-unknown'
      }
    } else if (singlePlayerData.value?.isInitialized === false) {
      // The returned data explicitly indicates that the player has not been initialized.
      return 'error-entity-uninitialized'
    } else if (singlePlayerData.value) {
      // Data returned with no error.
      return 'success'
    } else {
      // No data or error.
      return 'loading'
    }
  }
)

/**
 * Which background stripe variant to use for the page.
 */
const viewVariant = computed(() => {
  if (fetchStatus.value === 'loading') {
    // Still loading - don't show any stripes.
    return undefined
  } else if (fetchStatus.value.startsWith('error')) {
    // If there were any fetching errors then use `danger`.
    return 'danger'
  } else if (singlePlayerData.value?.model && !singlePlayerData.value.model.deletionStatus.startsWith('None')) {
    // If deletion is pending/complete then use `danger`.
    return 'danger'
  } else {
    // Don't show any stripes. If there are any alerts on this page, they will set the stripe color.
    return undefined
  }
})

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(playerName, (playerName) => `Manage ${playerName.value || 'Player'}`)

// Alerts ----------------------

const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  if (singlePlayerData.value?.model) {
    if (singlePlayerData.value.model.deletionStatus.startsWith('DeletedBy')) {
      const source = singlePlayerData.value.model.deletionStatus.substring(9)
      allAlerts.push({
        title: `☠️ Player Deleted by ${source}`,
        message: 'This player is no more. They have ceased to be and the account had been scrubbed of personal data.',
        variant: 'danger',
        dataTest: 'player-deleted-alert',
      })
    } else if (
      singlePlayerData.value.model.attachedAuthMethods &&
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.keys(singlePlayerData.value.model.attachedAuthMethods).length === 0
    ) {
      allAlerts.push({
        title: 'Orphan Account',
        message: 'This account has no login methods attached. Nobody can currently connect to play as this player.',
        variant: 'warning',
        dataTest: 'player-orphaned-alert',
      })
    }

    if (singlePlayerData.value.model.isBanned === true) {
      allAlerts.push({
        title: 'Banned',
        message: "This player is currently banned. That'll teach them!",
        variant: 'warning',
        dataTest: 'player-banned-alert',
      })
    }

    if (singlePlayerData.value.logicVersionMismatched === true && singlePlayerData.value.model) {
      allAlerts.push({
        title: 'Logic Version Mismatched',
        message: `This player's logic version was expected to be ${singlePlayerData.value.model.logicVersion} but was ${singlePlayerData.value.logicVersion} instead. The player's logic version will automatically be updated next time the player is persisted. As a result this player might behave unexpectedly.`,
        variant: 'warning',
        dataTest: 'logic-version-mismatch-alert',
      })
    }
  }

  return allAlerts
})

// Tabs ------------------------

// Fetch display names for tabs from dashboard options
const dashboardOptionsData = useSubscription(getDashboardOptionsSubscriptionOptions()).data
function getDisplayNameForTabByIndex(tabIndex: number): string {
  if (!dashboardOptionsData.value) {
    throw new Error('Trying to find a display name for a tab before dashboard options have loaded!')
  }
  return dashboardOptionsData.value[`playerDetailsTab${tabIndex}DisplayName`]
}

/**
 * Tab options computed from `dashboardOptionsData` for the `MTabLayout` component.
 */
const tabOptions = computed((): TabOption[] => {
  return [
    { label: getDisplayNameForTabByIndex(0) },
    { label: getDisplayNameForTabByIndex(1) },
    { label: getDisplayNameForTabByIndex(2) },
    { label: getDisplayNameForTabByIndex(3) },
    { label: getDisplayNameForTabByIndex(4) }, //, highlighted: singlePlayerData.value.incidentHeaders.length > 0 },
  ]
})

/**
 * Throw an error if the component for the UI placement in a specific tab is missing.
 */
const tabUiPlacements: UiPlacement[] = [
  'Players/Details/Tab0',
  'Players/Details/Tab1',
  'Players/Details/Tab2',
  'Players/Details/Tab3',
  'Players/Details/Tab4',
]

// Activables etc. ---------------------
// TODO: migrate away from here

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

const activableCategories = computed(() => {
  const categories = Object.entries(staticConfigData.value?.activablesMetadata.categories ?? {})
  return Object.fromEntries(
    categories.map((x) => {
      return [x[0], x[1].displayName]
    })
  )
})
</script>

<style>
.tabs-container {
  position: sticky;
  top: -1px;
  z-index: 4;
}

.tabs-container button {
  background-color: transparent;
  border: 0;
  font-weight: 300;
  border-radius: 0.3rem;
  padding: 0.5rem 1rem 0.5rem 1rem;
  margin-left: 0.1rem;
  margin-right: 0.1rem;
  color: var(--metaplay-blue);
}

.tabs-container button:hover {
  background-color: var(--metaplay-grey);
  color: rgb(27, 103, 162);
}

.tabs-container .active {
  background-color: var(--metaplay-blue);
  color: white;
}

.tabs-container .active:hover {
  background-color: var(--metaplay-blue);
  color: white;
}
</style>

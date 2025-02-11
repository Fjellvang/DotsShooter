<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  style="position: sticky; top: 0; z-index: 12"
  class="tw-mx-4"
  )
  // Connection status
  div(
    v-if="!gameServerApiStore.isConnected"
    class="header-notification rounded-bottom shadow-sm alert-danger tw-mb-1 tw-py-4 tw-text-center"
    )
    div(class="font-weight-bolder")
      fa-icon(
        :icon="['far', 'window-close']"
        class="mr-2"
        )
      | Lost connection to the backend!
    div Please check that the instance is running and that you are connected to the internet.

  // Server restarted
  div(
    v-if="uiStore.hasServerRestarted"
    class="header-notification rounded-bottom shadow-sm alert-danger tw-mb-1 tw-py-4 tw-text-center"
    )
    div(class="font-weight-bolder")
      fa-icon(
        :icon="['far', 'window-close']"
        class="mr-2"
        )
      | Server restarted!
    div Please #[MTextButton(:to="route.fullPath" @click="refreshPage()") refresh the page] to get the latest changes.

  // Game config updates
  div(
    v-if="uiStore.isNewGameConfigAvailable"
    class="header-notification rounded-bottom shadow-sm alert-danger tw-mb-1 tw-py-4 tw-text-center"
    )
    div(class="font-weight-bolder")
      fa-icon(
        :icon="['far', 'window-close']"
        class="mr-2"
        )
      | Server game configs updated!
    //- We use `to` to make this a link, but the router will not actually navigate to the same page. Instead, we hook
    //- the click event and manipulate router directly.
    div Please #[MTextButton(:to="route.fullPath" @click="refreshPage()") refresh the page] to get the latest changes.

  // Maintenance mode
  div(
    v-if="backendStatus && (backendStatus.maintenanceStatus.isInMaintenance || backendStatus.maintenanceStatus.scheduledMaintenanceMode)"
    class="header-notification rounded-bottom shadow-sm alert-warning tw-mb-1 tw-py-4 tw-text-center"
    data-testid="maintenance-mode-header-notification"
    )
    div(v-if="backendStatus.maintenanceStatus.isInMaintenance")
      fa-icon(
        :icon="['far', 'window-close']"
        class="mr-2"
        )
      span(
        class="font-weight-bolder"
        data-testid="maintenance-on-label"
        ) Maintenance mode on
      div You can turn it off from the #[MTextButton(to="/system") system settings page].
    div(v-else-if="backendStatus.maintenanceStatus.scheduledMaintenanceMode")
      fa-icon(
        :icon="['far', 'window-close']"
        class="mr-2"
        )
      span(
        class="font-weight-bolder"
        data-testid="maintenance-scheduled-label"
        ) Maintenance scheduled
      div Maintenance mode will start #[meta-time(:date="backendStatus.maintenanceStatus.scheduledMaintenanceMode.startAt")].
</template>

<script lang="ts" setup>
import { useRoute, useRouter } from 'vue-router'

import { useGameServerApiStore } from '@metaplay/game-server-api'
import { useUiStore } from '@metaplay/meta-ui'
import { MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getBackendStatusSubscriptionOptions } from '../../subscription_options/general'

const route = useRoute()
const router = useRouter()
const uiStore = useUiStore()
const gameServerApiStore = useGameServerApiStore()

const { data: backendStatus } = useSubscription(getBackendStatusSubscriptionOptions())

/**
 * Tell the router to reload the current page.
 */
function refreshPage(): void {
  router.go(0)
}
</script>

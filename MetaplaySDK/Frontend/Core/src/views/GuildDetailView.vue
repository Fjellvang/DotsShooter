<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :alerts="alerts"
  :is-loading="fetchStatus === 'loading'"
  )
  //- Errors when fetching data.
  template(
    v-if="fetchStatus.includes('error')"
    #errors
    )
    MErrorCallout(
      v-if="fetchStatus === 'error-bad-id' || fetchStatus === 'error-unknown'"
      :error="singleGuildError"
      )
    MCallout(
      v-else-if="fetchStatus === 'error-not-found'"
      title="Guild Not Found"
      variant="danger"
      ) This guild does not exist. Are you looking in the correct deployment?
    MCallout(
      v-else-if="fetchStatus === 'error-entity-uninitialized'"
      title="Guild Not Initialized"
      variant="danger"
      ) This guild has not yet been initialized. This is likely a temporary state for a new guild, but indicates a problem if the state persists.

    //- Give access to additional debugging tools when they are relevant.
    MCallout(
      v-if="fetchStatus === 'error-unknown' || fetchStatus === 'error-entity-uninitialized'"
      title="Debugging"
      variant="danger"
      class="tw-mt-3"
      )
      p The following tool may be able to give you more information about this error:
      ul
        li Use #[MTextButton(:to="`/entities/${guildId}/dbinfo`" permission="api.database.inspect_entity" data-testid="model-size-link") save file inspector] to inspect entity data saved on the database.

  template(#overview)
    guild-overview-card(:guildId="guildId")

  template(#default)
    div(class="tw-mx-auto tw-mb-10 tw-max-w-2xl tw-@container")
      guild-admin-actions-card(:guildId="guildId")

    h3(class="tw-my-5") Game State

    core-ui-placement(
      placementId="Guilds/Details/GameState"
      :guildId="guildId"
      )

    h3(class="tw-my-5") Guild & Admin Logs

    core-ui-placement(
      placementId="Guilds/Details/GuildAdminLogs"
      :guildId="guildId"
      )

    //- Guild pretty print
    MetaRawData(
      :kvPair="singleGuildData"
      name="guild"
      )/
</template>

<script lang="ts" setup>
import { AxiosError } from 'axios'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaRawData } from '@metaplay/meta-ui'
import {
  MCallout,
  MErrorCallout,
  MTextButton,
  MViewContainer,
  type MViewContainerAlert,
  useHeaderbar,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import GuildAdminActionsCard from '../components/guilds/GuildAdminActionsCard.vue'
import GuildOverviewCard from '../components/guilds/GuildOverviewCard.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue } from '../coreUtils'
import { getSingleGuildSubscriptionOptions } from '../subscription_options/guilds'

const route = useRoute()
const guildId = computed(() => routeParamToSingleValue(route.params.id))

const { data: singleGuildData, error: singleGuildError } = useSubscription(
  getSingleGuildSubscriptionOptions(guildId.value)
)

/**
 * A computed status of the data fetch based on the data and error values. This splits the status into three broad
 * categories: `loading`, `success`, and `error`. The error category is further split into subcategories by determining
 * the specific type of error.
 */
const fetchStatus = computed(
  (): 'loading' | 'success' | 'error-bad-id' | 'error-not-found' | 'error-unknown' | 'error-entity-uninitialized' => {
    if (singleGuildError.value) {
      const axiosError = singleGuildError.value as AxiosError
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
    } else if (singleGuildData.value?.isInitialized === false) {
      // The returned data explicitly indicates that the player has not been initialized.
      return 'error-entity-uninitialized'
    } else if (singleGuildData.value) {
      // Data returned with no error.
      return 'success'
    } else {
      // No data or error.
      return 'loading'
    }
  }
)

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(
  singleGuildData,
  (singleGuildData) => `Manage ${singleGuildData.value?.model.displayName || 'Guild'}`
)

const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  if (singleGuildData.value?.model) {
    if (singleGuildData.value.model.lifecyclePhase === 'Closed') {
      allAlerts.push({
        title: '☠️ Guild Closed',
        message: 'This guild has no players and has been closed.',
        variant: 'danger',
        dataTest: 'guild-closed-alert',
      })
    } else if (
      singleGuildData.value.model.lifecyclePhase === 'WaitingForSetup' ||
      singleGuildData.value.model.lifecyclePhase === 'WaitingForLeader'
    ) {
      allAlerts.push({
        title: 'Guild-in-Progress',
        message: 'All our actors are busy creating this guild and will be with you momentarily, please hold.',
        variant: 'warning',
        dataTest: 'guild-in-progress-alert',
      })
    }
  }

  return allAlerts
})
</script>

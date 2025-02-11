<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Card.
MCard(
  title="Maintenance Mode"
  :is-loading="!backendStatusData"
  :error="backendStatusError"
  :badge="cardBadge.text"
  :badge-variant="cardBadge.variant"
  data-testid="system-maintenance-mode-card"
  )
  template(#subtitle)
    div(
      v-if="backendStatusData.maintenanceStatus.isInMaintenance"
      class="tw-mb-4"
      )
      div Maintenance mode started #[meta-time(:date="backendStatusData.maintenanceStatus.scheduledMaintenanceMode.startAt")].
      div(v-if="backendStatusData.maintenanceStatus.scheduledMaintenanceMode.estimationIsValid") Estimated duration: {{ backendStatusData.maintenanceStatus.scheduledMaintenanceMode.estimatedDurationInMinutes }} minutes
      div(v-else) Duration: #[MBadge None]
      div Affected platforms: #[MBadge(v-for="plat in maintenancePlatformsOnServer" :key="plat" class="tw-mr-1") {{ plat }}]

    div(v-else-if="backendStatusData.maintenanceStatus.scheduledMaintenanceMode")
      p
        | Maintenance mode has been scheduled to start #[meta-time(:date="backendStatusData.maintenanceStatus.scheduledMaintenanceMode.startAt")] on #[meta-time(:date="backendStatusData.maintenanceStatus.scheduledMaintenanceMode.startAt" showAs="datetime")]
      p(
        v-if="backendStatusData.maintenanceStatus.scheduledMaintenanceMode.estimationIsValid"
        class="m-0"
        ) Estimated duration: {{ backendStatusData.maintenanceStatus.scheduledMaintenanceMode.estimatedDurationInMinutes }} minutes
      p(
        v-else
        class="m-0"
        ) Duration: #[MBadge Off]
      p Affected platforms: #[MBadge(v-for="plat in maintenancePlatformsOnServer" :key="plat" class="tw-mr-1") {{ plat }}]

    div(v-else)
      p Maintenance mode will prevent players from logging into the game. Use it to make backend downtime more graceful for players.

  //- Action Modal.
  div(class="tw-flex tw-justify-end")
    MActionModalButton(
      modal-title="Update Maintenance Mode Settings"
      :action="setMaintenance"
      trigger-button-label="Edit Settings"
      :ok-button-label="okButtonDetails.text"
      :ok-button-disabled-tooltip="okButtonDisabledReason"
      permission="api.system.edit_maintenance"
      @show="resetModal"
      data-testid="maintenance-mode"
      )
      template(#ok-button-icon)
        fa-icon(
          v-if="okButtonDetails.icon"
          :icon="okButtonDetails.icon"
          class="tw-mb-[0.05rem] tw-h-3.5 tw-w-4"
          )
      div(class="tw-flex tw-justify-between tw-font-semibold") Maintenance Mode Enabled
        MInputSwitch(
          :model-value="maintenanceEnabled"
          class="tw-relative tw-top-1 tw-mr-1"
          name="maintenanceModeEnabled"
          size="small"
          @update:model-value="maintenanceEnabled = $event"
          data-testid="maintenance-enabled"
          )

      MInputDateTime(
        :model-value="maintenanceDateTime"
        :disabled="!maintenanceEnabled"
        min-date-time="utcNow"
        label="Start Time (UTC)"
        @update:model-value="onMaintenanceDateTimeChange"
        )
      p(class="tw-font-xs tw-mt-1 tw-text-neutral-400")
        span(v-if="!isMaintenanceDateTimeInFuture && maintenanceEnabled") Maintenance mode will start immediately.
        span(v-else-if="maintenanceEnabled") Maintenance mode will start #[meta-time(:date="maintenanceDateTime")].
        span(v-else) Maintenance mode off.

      MInputMultiSelectCheckbox(
        :model-value="maintenancePlatforms"
        :options="props.platforms.map((platform) => ({ label: platform, value: platform }))"
        label="Platforms"
        :disabled="!maintenanceEnabled"
        hint-message="Maintenance mode will only affect the selected platforms."
        class="tw-mb-4"
        @update:model-value="maintenancePlatforms = $event"
        )

      MInputNumber(
        label="Estimated Duration"
        :model-value="maintenanceDuration"
        :disabled="!maintenanceEnabled"
        :variant="maintenanceEnabled && maintenanceDuration && maintenanceDuration > 0 ? 'success' : 'default'"
        hint-message="This is just a number you can display on the client to the players. Maintenance mode will not turn off automatically based on duration."
        :min="0"
        @update:model-value="maintenanceDuration = $event"
        )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  MCard,
  MInputDateTime,
  MInputMultiSelectCheckbox,
  MInputNumber,
  MInputSwitch,
  useNotifications,
  type Variant,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getBackendStatusSubscriptionOptions } from '../../subscription_options/general'
import type { StatusResponse } from '../../subscription_options/generalTypes'

const props = withDefaults(
  defineProps<{
    platforms?: string[]
  }>(),
  {
    platforms: () => ['iOS', 'Android', 'WebGL', 'UnityEditor'],
  }
)

const gameServerApi = useGameServerApi()
const {
  data: backendStatusData,
  refresh: backendStatusTriggerRefresh,
  error: backendStatusError,
} = useSubscription(getBackendStatusSubscriptionOptions())

const maintenanceEnabled = ref<boolean>(false)
const maintenanceDateTime = ref<DateTime>(DateTime.now())
const maintenanceDuration = ref<number>()
const maintenancePlatforms = ref<any>(null)

const maintenancePlatformsOnServer = computed(() => {
  return props.platforms.filter(
    (platform) =>
      !backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode.platformExclusions.includes(platform)
  )
})

const okButtonDisabledReason = computed(() => {
  // If maintenance is not enabled and there is no maintenance status.
  if (
    !maintenanceEnabled.value &&
    !(
      backendStatusData.value.maintenanceStatus.isInMaintenance ||
      backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode
    )
  ) {
    return 'Toggle the switch to enable maintenance mode.'
  }
  // If maintenance is enabled but no platforms are selected.
  if (maintenancePlatforms.value?.length === 0) {
    return 'Select at least one platform to enable maintenance mode.'
  }
  // Otherwise, the form is valid.
  return undefined
})

function onMaintenanceDateTimeChange(value?: DateTime): void {
  if (!value) return
  maintenanceDateTime.value = value
}

const isMaintenanceDateTimeInFuture = computed(() => {
  return maintenanceDateTime.value.diff(DateTime.now()).toMillis() >= 0
})

const { showSuccessNotification } = useNotifications()

async function setMaintenance(): Promise<void> {
  if (maintenanceEnabled.value) {
    const payload = {
      StartAt: maintenanceDateTime.value.toISO(),
      EstimatedDurationInMinutes: maintenanceDuration.value ? maintenanceDuration.value : 0,
      EstimationIsValid: !!maintenanceDuration.value,
      PlatformExclusions: props.platforms.filter((platform) => !maintenancePlatforms.value.includes(platform)),
    }
    await gameServerApi.put('maintenanceMode', payload)

    if (isMaintenanceDateTimeInFuture.value) {
      const message = 'Maintenance mode enabled.'
      showSuccessNotification(message)
    } else {
      const message = 'Maintenance mode scheduled.'
      showSuccessNotification(message)
    }
  } else {
    await gameServerApi.delete('maintenanceMode')
    const message = 'Maintenance mode disabled.'
    showSuccessNotification(message)
  }
  backendStatusTriggerRefresh()
}

const okButtonDetails = computed(() => {
  if (maintenanceEnabled.value) {
    if (isMaintenanceDateTimeInFuture.value) {
      return {
        text: 'Schedule',
        icon: 'calendar-alt',
      }
    } else {
      return {
        text: 'Set Immediately',
        icon: ['far', 'window-close'],
      }
    }
  } else {
    return {
      text: 'Save Settings',
    }
  }
})

function resetModal(): void {
  maintenanceDuration.value = backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode?.estimationIsValid
    ? backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode?.estimatedDurationInMinutes
    : undefined
  maintenanceDateTime.value = backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode
    ? DateTime.fromISO((backendStatusData.value as StatusResponse).maintenanceStatus.scheduledMaintenanceMode?.startAt)
    : DateTime.now().plus({ minutes: 60 })
  maintenanceEnabled.value = !!(
    backendStatusData.value.maintenanceStatus.isInMaintenance ||
    backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode
  )
  maintenancePlatforms.value = props.platforms.filter(
    (platform) =>
      !backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode?.platformExclusions.includes(platform)
  )
}

const cardBadge = computed((): { text: string; variant: Variant } => {
  if (!backendStatusData.value) {
    return {
      text: 'Loading',
      variant: 'neutral',
    }
  } else if (backendStatusData.value.maintenanceStatus.isInMaintenance) {
    return {
      text: 'On',
      variant: 'success',
    }
  } else if (backendStatusData.value.maintenanceStatus.scheduledMaintenanceMode) {
    return {
      text: 'Scheduled',
      variant: 'primary',
    }
  } else {
    return {
      text: 'Off',
      variant: 'neutral',
    }
  }
})
</script>

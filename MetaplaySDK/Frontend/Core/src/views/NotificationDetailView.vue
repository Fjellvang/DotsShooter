<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.notifications.view"
  :is-loading="!singleNotificationCampaignsData"
  :meta-api-error="singleNotificationCampaignsError"
  :alerts="pageAlerts"
  )
  template(#overview)
    MPageOverviewCard(
      :id="`${campaignInfo.id}`"
      :title="campaignInfo.campaignParams.name"
      )
      template(#badge)
        //- Running
        MBadge(
          v-if="campaignInfo.campaignPhase == 'Running'"
          :variant="phaseVariant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="broadcast-tower")
          | {{ campaignInfo.campaignPhase }}
        //- Cancelling
        MBadge(
          v-else-if="campaignInfo.campaignPhase == 'Cancelling'"
          :variant="phaseVariant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="broadcast-tower")
          | {{ campaignInfo.campaignPhase }}
        //- Scheduled
        MBadge(
          v-else-if="campaignInfo.campaignPhase == 'Scheduled'"
          :variant="phaseVariant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="calendar-alt")
          | {{ campaignInfo.campaignPhase }}
        //- Cancelled
        MBadge(
          v-else-if="campaignInfo.campaignPhase == 'Cancelled'"
          :variant="phaseVariant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="times")
          | {{ campaignInfo.campaignPhase }}
        //- Did Not Run
        MBadge(
          v-else-if="campaignInfo.campaignPhase == 'DidNotRun'"
          :variant="phaseVariant"
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="bell-slash")
          | {{ notificationPhaseDisplayString(campaignInfo.campaignPhase) }}
        //- Sent
        MBadge(
          v-else
          class="ml-2"
          )
          template(#icon)
            fa-icon(icon="paper-plane")
          | {{ campaignInfo.campaignPhase }}
      template(#buttons)
        div(class="tw-inline-flex tw-gap-x-2")
          notification-form-button(
            modal-title="Duplicate Campaign"
            button-text="Duplicate"
            :oldNotification="campaignFormInfo"
            :update="false"
            @refresh="singleNotificationCampaignsRefresh"
            )

          notification-form-button(
            v-if="campaignInfo.campaignPhase === 'Scheduled'"
            modal-title="Edit Campaign"
            button-text="Edit"
            :oldNotification="campaignFormInfo"
            :update="true"
            @refresh="singleNotificationCampaignsRefresh"
            )

          MButton(
            v-if="campaignInfo.campaignPhase == 'Running'"
            variant="danger"
            permission="api.notifications.edit"
            @click="cancelCampaign"
            ) Cancel

          MActionModalButton(
            modal-title="Delete Campaign"
            :action="deleteCampaign"
            trigger-button-label="Delete"
            :trigger-button-disabled-tooltip="canDeleteCampaign() ? undefined : 'You cannot delete this campaign in its current phase.'"
            ok-button-label="Delete campaign"
            variant="danger"
            permission="api.notifications.edit"
            )
            div Deleting this Campaign will prevent any more players from receiving it. However, it will not be removed from the devices of those who have already received it.
            meta-no-seatbelts(class="tw-mt-2")

      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        class="tw-mt-1"
        )
        b-tbody
          b-tr
            b-td Audience Size Estimate
            b-td(class="text-right") #[meta-audience-size-estimate(:sizeEstimate="isTargeted() ? singleNotificationCampaignsData.audienceSizeEstimate : undefined")]
          b-tr
            b-td Start Time
            b-td(class="text-right") #[meta-time(:date="campaignInfo.campaignParams.targetTime" showAs="datetime")]

      //- Campaign statistics
      div(v-if="campaignInfo.campaignPhase !== 'DidNotRun'")
        span(class="font-weight-bold") #[fa-icon(icon="chart-line")] Statistics
        b-row(
          align-h="center"
          class="tw-mt-1"
          )
          b-col(
            v-if="!campaignInfo.stats"
            xl="9"
            class="tw-text-center"
            )
            b-alert(
              show
              variant="secondary"
              class="tw-mt-1"
              ) Stats will be collected after the campaign sending starts.
          b-col(v-else)
            b-table-simple(
              small
              responsive
              )
              b-tbody
                b-tr
                  b-td Duration
                  b-td(class="text-right") #[meta-duration(:duration="jobDuration" showAs="humanizedSentenceCase")]
                b-tr
                  b-td Players Notified
                  b-td(class="text-right") {{ abbreviateNumber(campaignInfo.stats.notificationStats.numPlayersNotified) }}
                b-tr
                  b-td Players Notifications Failed
                  b-td(class="text-right") {{ abbreviateNumber(campaignInfo.stats.notificationStats.notificationFailedPlayers.count) }}
                b-tr
                  b-td Firebase Analytics Label
                  b-td(class="text-right") {{ campaignInfo.campaignParams.firebaseAnalyticsLabel }}
      template(#alerts)
        MCallout(
          title="Progress"
          variant="neutral"
          class="tw-pb-4"
          )
          b-progress(
            v-if="campaignInfo.stats"
            :value="campaignInfo.stats.scanStats.scannedRatioEstimate"
            :max="1"
            show-progress
            :animated="campaignInfo.campaignPhase == 'Running'"
            :variant="phaseVariant"
            )
          b-progress(
            v-else
            :value="0"
            :max="1"
            show-progress
            :variant="phaseVariant"
            )

  meta-generated-section(
    title="Content"
    :value="campaignInfo.campaignParams.content"
    is-targeting-multiple-players
    )

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Targeting
    h4: MBadge(
      :variant="isTargeted() ? 'success' : 'neutral'"
      class="ml-2"
      ) {{ isTargeted() ? 'On' : 'Off' }}

  b-row(align-h="center")
    b-col(
      md="6"
      class="tw-mb-4"
      )
      targeting-card(
        ownerTitle="This notification"
        :targetCondition="campaignInfo.campaignParams.targetCondition"
        )

    b-col(
      md="6"
      class="tw-mb-4"
      )
      player-list-card(
        :playerIds="campaignInfo.campaignParams.targetPlayers ?? []"
        title="Individual Players"
        emptyMessage="No individual players targeted."
        )

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Admin

  b-row(
    align-h="center"
    class="tw-mb-4"
    )
    b-col(md="6")
      audit-log-card(
        targetType="$Notification"
        :targetId="String(campaignInfo.id)"
        )

  meta-raw-data(
    :kvPair="campaignInfo"
    name="campaign"
    )/
</template>

<script lang="ts" setup>
import { BProgress } from 'bootstrap-vue'
import { DateTime } from 'luxon'
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  MButton,
  MCallout,
  MPageOverviewCard,
  MViewContainer,
  type Variant,
  type MViewContainerAlert,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { abbreviateNumber } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import MetaAudienceSizeEstimate from '../components/MetaAudienceSizeEstimate.vue'
import AuditLogCard from '../components/auditlogs/AuditLogCard.vue'
import MetaGeneratedSection from '../components/generatedui/components/MetaGeneratedSection.vue'
import PlayerListCard from '../components/global/PlayerListCard.vue'
import TargetingCard from '../components/mails/TargetingCard.vue'
import NotificationFormButton from '../components/notifications/NotificationFormButton.vue'
import { routeParamToSingleValue, notificationPhaseDisplayString } from '../coreUtils'
import { getSingleNotificationSubscriptionOptions } from '../subscription_options/notifications'
import { getAllScanJobsSubscriptionOptions } from '../subscription_options/scanJobs'

const gameServerApi = useGameServerApi()
const route = useRoute()
const router = useRouter()

/**
 * Subscribe to notification campaign data.
 */
const {
  data: singleNotificationCampaignsData,
  refresh: singleNotificationCampaignsRefresh,
  error: singleNotificationCampaignsError,
} = useSubscription(getSingleNotificationSubscriptionOptions(routeParamToSingleValue(route.params.id)))

/**
 * Subscribe to database scan job data.
 */
const { data: databaseScanJobsData } = useSubscription(getAllScanJobsSubscriptionOptions())

/**
 * Notification campaign details that are to be displayed in this component.
 */
const campaignInfo = computed(() => {
  return singleNotificationCampaignsData.value?.campaignInfo
})

/**
 * Color variant to be used in the progress bar.
 */
const phaseVariant = computed((): Variant => {
  if (campaignInfo.value) {
    if (campaignInfo.value?.campaignPhase === 'Running') {
      return 'success'
    }
    if (campaignInfo.value?.campaignPhase === 'Cancelling') {
      return 'warning'
    }
    if (campaignInfo.value?.campaignPhase === 'Scheduled') {
      return 'primary'
    }
  }
  // Cancelled, DidNotRun, and sent.
  return 'neutral'
})

/**
 * Amount of time taken to run the notification campaign.
 */
const jobDuration = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const start = DateTime.fromISO(campaignInfo.value?.stats.startTime)
  const stop = campaignInfo.value?.stats.stopTime
    ? // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      DateTime.fromISO(campaignInfo.value?.stats.stopTime)
    : DateTime.now()
  return stop.diff(start).toISO()
})

/**
 * When true indicates that the notification campaign is targeted to a specific audience.
 */
function isTargeted(): boolean {
  return (
    campaignInfo.value?.campaignParams?.targetPlayers?.length || campaignInfo.value?.campaignParams?.targetCondition
  )
}

/**
 * Notification campaign details to be edited or duplicated.
 */
const campaignFormInfo = computed(() => {
  if (campaignInfo.value) {
    return {
      id: campaignInfo.value?.id,
      name: campaignInfo.value?.campaignParams.name,
      targetTime: campaignInfo.value?.campaignParams.targetTime,
      content: campaignInfo.value?.campaignParams.content,
      debugEntityIdValueUpperBound: campaignInfo.value?.campaignParams.debugEntityIdValueUpperBound,
      debugFakeNotificationMode: campaignInfo.value?.campaignParams.debugFakeNotificationMode,
      firebaseAnalyticsLabel: campaignInfo.value?.campaignParams.firebaseAnalyticsLabel,
      targetPlayers: campaignInfo.value?.campaignParams.targetPlayers,
      targetCondition: campaignInfo.value?.campaignParams.targetCondition,
    }
  } else {
    return undefined
  }
})

/**
 * Cannot delete a campaign when it is in certain phases.
 */
function canDeleteCampaign(): boolean {
  const allowablePhases = [
    'Scheduled',
    // Running,
    'Sent',
    'Cancelled',
    // Cancelling,
    'DidNotRun',
  ]
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return allowablePhases.includes(campaignInfo.value.campaignPhase)
}

const { showSuccessNotification } = useNotifications()

/**
 * Delete the displayed notification campaign and navigates back to the notification list view.
 */
async function deleteCampaign(): Promise<void> {
  await gameServerApi.delete(`/notifications/${route.params.id.toString()}`)
  const message = 'Notification campaign deleted.'
  showSuccessNotification(message)
  await router.push('/notifications')
}

/**
 * Cancel a running notification campaign.
 */
async function cancelCampaign(): Promise<void> {
  await gameServerApi.put(`/notifications/${route.params.id.toString()}/cancel`)
  showSuccessNotification('Notification campaign cancellation started.')
  singleNotificationCampaignsRefresh()
}

/**
 * Alert messages to be displayed at the top of the page.
 */
const pageAlerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []

  // Warning: Push notification jobs are globally paused.
  if (databaseScanJobsData.value?.globalPauseIsEnabled) {
    allAlerts.push({
      title: 'Push Notifications Paused',
      message:
        'All database scan jobs are currently paused - this includes the job that sends push notifications. You can resume them from the scan jobs page.',
      variant: 'warning',
    })
  }
  if (campaignInfo.value?.campaignPhase === 'DidNotRun') {
    allAlerts.push({
      title: 'Campaign did not run',
      message: `${campaignInfo.value.campaignParams?.name} did not run because push notifications
      are disabled for this deployment. Contact your game team to set them up.`,
      variant: 'warning',
    })
  }

  return allAlerts
})
</script>

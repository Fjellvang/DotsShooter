<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.notifications.view"
  :is-loading="!allNotificationCampaignsData"
  :error="allNotificationCampaignsError"
  :alerts="pageAlerts"
  )
  template(#overview)
    MPageOverviewCard(
      title="Create a New Notification Campaign"
      :alerts="overviewAlerts"
      data-testid="notification-list-overview-card"
      )
      template(#buttons)
        notification-form-button(
          v-if="staticInfos.featureFlags.pushNotifications"
          modal-title="New Notification Campaign"
          button-text="New Campaign"
          button-icon="plus-square"
          )

      p(class="tw-mb-1") Notification campaigns are scheduled and localised push notifications that you can send to the players.
      div(class="tw-text-xs+ tw-text-neutral-500") Players who match the targeting criteria of a campaign will be sent a push notification in their selected language if they have enabled notifications on their device. Notifications are processed in batches by Firebase, so sending large campaigns may take up to a few hours to complete.
  MSingleColumnLayout
    meta-list-card#broadcasts-list(
      title="All Notification Campaigns"
      :itemList="allNotificationCampaignsData"
      :searchFields="['id', 'params.name']"
      :filterSets="filterSets"
      :sortOptions="sortOptions"
      :defaultSortOption="1"
      :pageSize="20"
      icon="comment-alt"
      data-testid="all-notifications-list-card"
      )
      template(#item-card="{ item: notification }")
        MListItem
          | {{ notification.params.name }} #[span(class="tw-text-xs+ tw-font-normal tw-text-neutral-500") for {{ audienceSummary(notification.params) }}]
          template(#top-right)
            MBadge(:variant="getPhaseVariant(notification.phase)")
              template(#icon)
                fa-icon(
                  :icon="getPhaseIcon(notification.phase)"
                  class="tw-mr-1"
                  )
              | {{ notificationPhaseDisplayString(notification.phase) }}

          template(#bottom-left)
            div(v-if="notification.phase === 'Scheduled'") Start time: #[meta-time(:date="notification.params.targetTime")]
            div(v-else-if="notification.phase === 'Sent'") Sent at: #[meta-time(:date="notification.params.targetTime")]
            div(v-else-if="notification.phase === 'Cancelled'") Cancelled
            div(v-else-if="notification.phase === 'DidNotRun'") {{ notificationPhaseDisplayString(notification.phase) }}
            div(v-else-if="notification.phase === 'Cancelling'")
              b-progress(
                :value="1"
                :max="1"
                animated
                variant="secondary"
                class="font-weight-bold"
                )
            div(v-else)
              b-progress(
                :value="notification.scannedRatioEstimate"
                :max="1"
                animated
                variant="success"
                class="font-weight-bold"
                )

          template(#bottom-right)
            MTextButton(:to="`/notifications/${notification.id}`") View campaign

  meta-raw-data(
    :kvPair="allNotificationCampaignsData"
    name="campaigns"
    )
</template>

<script lang="ts" setup>
import { BProgress } from 'bootstrap-vue'
import { computed, ref } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import { MetaListFilterOption, MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import {
  MBadge,
  type Variant,
  MListItem,
  MTextButton,
  MViewContainer,
  MPageOverviewCard,
  type MViewContainerAlert,
  type MPageOverviewCardAlert,
  MSingleColumnLayout,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import NotificationFormButton from '../components/notifications/NotificationFormButton.vue'
import { notificationPhaseDisplayString } from '../coreUtils'
import { getAllNotificationsSubscriptionOptions } from '../subscription_options/notifications'
import { getAllScanJobsSubscriptionOptions } from '../subscription_options/scanJobs'

const staticInfos = useStaticInfos()

/**
 * Subscribe to notification campaigns data.
 */
const { data: allNotificationCampaignsData, error: allNotificationCampaignsError } = useSubscription<any[] | undefined>(
  getAllNotificationsSubscriptionOptions()
)

/**
 * Subscribe to database scan job data.
 */
const { data: databaseScanJobsData } = useSubscription(getAllScanJobsSubscriptionOptions())

/**
 * Get the color variant to be used in phase badge.
 * @param status current campaign phase.
 */
function getPhaseVariant(status: string): Variant {
  if (['Sent', 'Cancelled', 'Cancelling', 'DidNotRun'].includes(status)) {
    return 'neutral'
  } else if (status === 'Scheduled') return 'primary'
  else return 'success'
}

/**
 * Get the custom icon based on the campaign phase.
 * @param status current campaign phase.
 */
function getPhaseIcon(status: string): string {
  if (status === 'Sent') return 'paper-plane'
  else if (status === 'Scheduled') return 'calendar-alt'
  else if (status === 'Cancelled') return 'times'
  else if (status === 'DidNotRun') return 'bell-slash'
  else return 'paper-plane'
}

/**
 * Summary estimation of number of players targeted in the campaign.
 */
function audienceSummary(params: any): string | undefined {
  if (!params.targetPlayers?.length && !params.targetCondition) {
    return 'everyone'
  } else if (params.targetPlayers?.length && params.targetCondition) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return `Segment based and ${maybePluralString(params.targetPlayers.length, 'player')}`
  } else if (params.targetPlayers?.length) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    return maybePluralString(params.targetPlayers.length, 'player')
  } else if (params.targetCondition) {
    return 'Segment based'
  }
}

/**
 * Sorting options for the list of notification campaigns.
 */
const sortOptions = ref([
  new MetaListSortOption('Time', 'params.targetTime', MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', 'params.targetTime', MetaListSortDirection.Descending),
  new MetaListSortOption('Name', 'params.name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'params.name', MetaListSortDirection.Descending),
])

/**
 * Filtering options for the list of notification campaigns.
 */
const filterSets = ref([
  new MetaListFilterSet('status', [
    new MetaListFilterOption('Scheduled', (x: any) => x.phase === 'Scheduled'),
    new MetaListFilterOption('Running', (x: any) => x.phase === 'Running'),
    new MetaListFilterOption('Sent', (x: any) => x.phase === 'Sent'),
    new MetaListFilterOption('Cancelled', (x: any) => x.phase === 'Cancelled' || x.phase === 'Cancelling'),
    new MetaListFilterOption('Did Not Run', (x: any) => x.phase === 'DidNotRun'),
  ]),
])

/**
 * Alert messages to be displayed in at the top of the page.
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
  return allAlerts
})

const overviewAlerts = computed(() => {
  const allAlerts: MPageOverviewCardAlert[] = []
  if (!staticInfos.featureFlags.pushNotifications) {
    allAlerts.push({
      title: 'Push Notifications Not Enabled',
      message: 'Push notifications have not been configured for this deployment. Set them up in your',
      variant: 'neutral',
      linkText: 'runtime options',
      link: '/environment?tab=2',
    })
  }
  return allAlerts
})
</script>

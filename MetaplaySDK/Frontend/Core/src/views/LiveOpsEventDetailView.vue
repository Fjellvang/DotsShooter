<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!singleLiveOpsEventData"
  :meta-api-error="singleLiveOpsEventError"
  :alerts="alerts"
  permission="api.liveops_events.view"
  )
  template(#overview)
    MPageOverviewCard(
      v-if="singleLiveOpsEventData"
      :title="singleLiveOpsEventData.eventParams.displayName"
      :subtitle="singleLiveOpsEventData.eventParams.description"
      :id="eventId"
      data-testid="live-ops-event-detail-overview-card"
      )
      //- Overview.
      ul(
        role="list"
        class="tw-m-0 tw-mb-4 tw-divide-y tw-divide-neutral-200"
        )
        div(class="tw-mb-1 tw-font-semibold") #[fa-icon(icon="bar-chart")] Overview
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Event Type
            MBadge {{ eventTypeDisplayName }}
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Total Participants
            span {{ singleLiveOpsEventData.participantCount }}

      //- Schedule.
      ul(
        role="list"
        class="tw-m-0 tw-divide-y tw-divide-neutral-200"
        )
        div(class="tw-mb-1 tw-font-semibold") #[fa-icon(icon="calendar-alt")] Scheduling
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Time Mode
            span(
              v-if="!schedule"
              class="tw-italic tw-text-neutral-500"
              ) None
            MBadge(v-else) {{ schedule.isPlayerLocalTime ? 'Player Local' : 'UTC' }}
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Start Time
            span(
              v-if="!schedule"
              class="tw-italic tw-text-neutral-500"
              ) None
            MDateTime(
              v-else
              :instant="DateTime.fromISO(schedule.enabledStartTime)"
              :disable-tooltip="schedule?.isPlayerLocalTime"
              )
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span End Time
            span(
              v-if="!schedule"
              class="tw-italic tw-text-neutral-500"
              ) None
            MDateTime(
              v-else
              :instant="DateTime.fromISO(schedule.enabledEndTime)"
              :disable-tooltip="schedule?.isPlayerLocalTime"
              )
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Preview Duration
            span(
              v-if="!schedule || Duration.fromISO(schedule.previewDuration).toMillis() === 0"
              class="tw-italic tw-text-neutral-500"
              ) None
            meta-duration(
              v-else
              :duration="schedule.previewDuration"
              showAs="exactDuration"
              hideMilliseconds
              )
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Ending Soon Duration
            span(
              v-if="!schedule || Duration.fromISO(schedule.endingSoonDuration).toMillis() === 0"
              class="tw-italic tw-text-neutral-500"
              ) None
            meta-duration(
              v-else
              :duration="schedule.endingSoonDuration"
              showAs="exactDuration"
              hideMilliseconds
              )
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Review Duration
            span(
              v-if="!schedule || Duration.fromISO(schedule.reviewDuration).toMillis() === 0"
              class="tw-italic tw-text-neutral-500"
              ) None
            meta-duration(
              v-else
              :duration="schedule?.reviewDuration"
              showAs="exactDuration"
              hideMilliseconds
              )
        li(class="tw-block tw-py-1")
          span(class="tw-flex tw-justify-between")
            span Total Duration
            span(
              v-if="!schedule"
              class="tw-italic tw-text-neutral-500"
              ) None
            meta-duration(
              v-else
              :duration="totalDurationInSeconds"
              showAs="exactDuration"
              hideMilliseconds
              )

      //- Only show timeline if event has UTC schedule and is not already ended.
      span(v-if="schedule?.isPlayerLocalTime === false && singleLiveOpsEventData?.currentPhase !== 'Ended'")
        //- Time mode.
        div(class="tw-my-5")
          ul(
            v-if="schedule"
            role="list"
            class="tw-m-0 tw-divide-y tw-divide-neutral-200"
            )
            div(class="tw-flex tw-justify-between")
              span Current Phase:&nbsp;
                MBadge(
                  v-if="singleLiveOpsEventData?.currentPhase"
                  :variant="liveOpsEventPhaseInfos[singleLiveOpsEventData.currentPhase].badgeVariant"
                  ) {{ liveOpsEventPhaseInfos[singleLiveOpsEventData.currentPhase].displayString }}
              span Next Phase:&nbsp;
                MBadge(
                  v-if="singleLiveOpsEventData?.nextPhase"
                  :variant="liveOpsEventPhaseInfos[singleLiveOpsEventData.nextPhase].badgeVariant"
                  ) {{ liveOpsEventPhaseInfos[singleLiveOpsEventData.nextPhase].displayString }}
                span &nbsp;#[meta-time(v-if="singleLiveOpsEventData?.nextPhaseTime" :date="singleLiveOpsEventData.nextPhaseTime")]

        //- Progress bar.
        div(
          :style="`margin-left: ${(durationToMilliseconds(schedule?.previewDuration ?? '0') / totalDurationInSeconds) * 100}%; position: relative; left: -1px`"
          class="pb-3 pl-2 border-left border-dark"
          )
          div(class="small font-weight-bold") Start
          div(class="small")
            span(v-if="schedule === null") No schedule
            meta-time(
              v-else
              :date="schedule?.enabledStartTime ?? ''"
              )
        // Schedule timeline
        b-progress(
          v-if="schedule"
          :max="totalDurationInSeconds"
          height="3rem"
          class="font-weight-bold"
          )
          b-progress-bar(
            :value="durationToMilliseconds(schedule?.previewDuration ?? '0')"
            variant="info"
            ) Preview
          b-progress-bar(
            :value="schedule.endingSoonDuration ? enabledDurationInMilliseconds - durationToMilliseconds(schedule.endingSoonDuration) : enabledDurationInMilliseconds"
            variant="success"
            ) Active
          b-progress-bar(
            :value="durationToMilliseconds(schedule.endingSoonDuration ?? '0')"
            variant="warning"
            ) Ending soon
          b-progress-bar(
            :value="durationToMilliseconds(schedule.reviewDuration ?? '0')"
            variant="info"
            ) Review
        b-progress(
          v-else
          :max="1"
          height="3rem"
          class="font-weight-bold"
          )
          b-progress-bar(
            :value="1"
            variant="success"
            ) Active
        div(
          :style="`margin-right: ${(durationToMilliseconds(schedule?.reviewDuration ?? '0') / totalDurationInSeconds) * 100}%; position: relative; right: -1px`"
          class="pt-3 pr-2 pb-1 border-right border-dark text-right"
          )
          div(class="small font-weight-bold") End
          div(class="small")
            span(v-if="schedule === null") No schedule
            meta-time(
              v-else
              :date="schedule?.enabledEndTime ?? ''"
              )

      template(#buttons)
        live-ops-event-action-conclude(:eventId="eventId")
        //- TODO: What's with these props? Wouldn't it be better to just pass in the ID have the modal subscribe to data itself and avoid all these separate inputs/outputs? Refresh event is not wired up atm.
        live-ops-event-form-modal-button(
          form-mode="edit"
          :current-phase="singleLiveOpsEventData?.currentPhase"
          :prefill-data="singleLiveOpsEventData?.eventParams"
          :event-id="singleLiveOpsEventData?.eventId"
          @refresh="singleLiveOpsEventRefresh"
          )
        live-ops-event-form-modal-button(
          form-mode="duplicate"
          :prefill-data="singleLiveOpsEventData?.eventParams"
          )
        live-ops-event-action-export(:prefill-text="singleLiveOpsEventData?.eventId")

  MTwoColumnLayout
    // live-ops-event-related-events-card(:eventId="eventId")

    MCard(
      title="Event Configuration"
      data-testid="live-ops-event-detail-configuration-card"
      )
      meta-generated-content(
        :value="singleLiveOpsEventData?.eventParams.content"
        is-targeting-multiple-players
        )

    targeting-card(
      :targetCondition="singleLiveOpsEventData?.eventParams.targetCondition ?? null"
      ownerTitle="This event"
      )

    player-list-card(
      :playerIds="singleLiveOpsEventData?.eventParams.targetPlayers ?? []"
      title="Individual Players"
      emptyMessage="No individual players targeted."
      )

  core-ui-placement(
    placementId="LiveOpsEvents/Details"
    :liveOpsEventId="eventId"
    )

  MetaRawData(
    :kvPair="singleLiveOpsEventData"
    name="singleLiveOpsEventData"
    )
</template>

<script lang="ts" setup>
import { BProgress, BProgressBar } from 'bootstrap-vue'
import { DateTime, Duration } from 'luxon'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MetaRawData } from '@metaplay/meta-ui'
import {
  MBadge,
  MCard,
  MDateTime,
  MPageOverviewCard,
  MViewContainer,
  type MViewContainerAlert,
  MTwoColumnLayout,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaGeneratedContent from '../components/generatedui/components/MetaGeneratedContent.vue'
// import LiveOpsEventRelatedEventsCard from '../components/liveopsevents/LiveOpsEventRelatedEventsCard.vue'
import PlayerListCard from '../components/global/PlayerListCard.vue'
import LiveOpsEventActionConclude from '../components/liveopsevents/LiveOpsEventActionConclude.vue'
import LiveOpsEventActionExport from '../components/liveopsevents/LiveOpsEventActionExport.vue'
import LiveOpsEventFormModalButton from '../components/liveopsevents/LiveOpsEventFormModalButton.vue'
import TargetingCard from '../components/mails/TargetingCard.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue, durationToMilliseconds } from '../coreUtils'
import { liveOpsEventPhaseInfos } from '../liveOpsEventUtils'
import {
  getLiveOpsEventTypesSubscriptionOptions,
  getSingleLiveOpsEventsSubscriptionOptions,
} from '../subscription_options/liveOpsEvents'

const route = useRoute()
const eventId = routeParamToSingleValue(route.params.id)

/**
 * Information about the event.
 */
const {
  data: singleLiveOpsEventData,
  error: singleLiveOpsEventError,
  refresh: singleLiveOpsEventRefresh,
} = useSubscription(getSingleLiveOpsEventsSubscriptionOptions(eventId))

/**
 * All template details.
 */
const { data: liveOpsEventTypesData } = useSubscription(getLiveOpsEventTypesSubscriptionOptions())

/**
 * Shortcut access to the schedule.
 */
const schedule = computed(() => {
  return singleLiveOpsEventData.value?.eventParams.schedule ?? null
})

/**
 * Enabled duration for the event, ie: between scheduled start and end time, in seconds.
 */
const enabledDurationInMilliseconds = computed(() => {
  if (singleLiveOpsEventData.value?.eventParams.schedule) {
    const duration =
      DateTime.fromISO(singleLiveOpsEventData.value.eventParams.schedule.enabledEndTime ?? '')
        .diff(DateTime.fromISO(singleLiveOpsEventData.value.eventParams.schedule.enabledStartTime ?? ''))
        .toISO() ?? 'PT0S'
    return durationToMilliseconds(duration)
  } else {
    return 0
  }
})

/**
 * Total duration of the event, including preview and review, in seconds.
 */
const totalDurationInSeconds = computed(() => {
  if (singleLiveOpsEventData.value?.eventParams.schedule) {
    return (
      durationToMilliseconds(singleLiveOpsEventData.value.eventParams.schedule.previewDuration ?? 'PT0S') +
      enabledDurationInMilliseconds.value +
      durationToMilliseconds(singleLiveOpsEventData.value.eventParams.schedule.reviewDuration ?? 'PT0S')
    )
  } else {
    return 0
  }
})

/**
 * Return the display name of the event type. Fallback to the full type name if it cannot be found.
 */
const eventTypeDisplayName = computed(() => {
  const eventContentClass = singleLiveOpsEventData.value?.eventParams.content.$type
  const eventType = (liveOpsEventTypesData.value ?? []).find((type) => {
    const typeContentClass = type.contentClass.split(',')[0]
    return eventContentClass === typeContentClass
  })
  return eventType?.eventTypeName ?? eventContentClass
})

/**
 * Array of messages to be displayed at the top of the page.
 */
const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []
  if (singleLiveOpsEventData.value?.currentPhase === 'Ended') {
    allAlerts.push({
      title: 'Past Event',
      message: 'You are currently viewing an event that has already ended.',
      variant: 'neutral',
    })
  }
  return allAlerts
})
</script>

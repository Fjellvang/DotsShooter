<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.activables.view"
  :is-loading="!singleActivableData"
  :error="singleActivableError"
  )
  template(#overview)
    MPageOverviewCard(
      :id="singleActivableData.config.activableId"
      :title="singleActivableData.config.displayName"
      :subtitle="singleActivableData.config.description"
      data-testid="activable-detail-overview-card"
      )
      span(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(small)
        b-tbody
          b-tr
            b-td Status
            b-td(class="tw-text-right")
              MBadge(
                v-if="isEnabled"
                variant="success"
                ) Enabled
              MBadge(
                v-else
                variant="danger"
                ) Disabled
          b-tr
            b-td Audience Size Estimate
            b-td(class="tw-text-right") #[meta-audience-size-estimate(:sizeEstimate="isTargeted ? singleActivableData.audienceSizeEstimate : undefined")]
          b-tr
            b-td Activated By
            b-td(class="tw-text-right") #[meta-abbreviate-number(:value="singleActivableData.statistics.numActivatedForFirstTime" unit="player")]
          b-tr
            b-td Consumed By
            b-td(class="tw-text-right") #[meta-abbreviate-number(:value="singleActivableData.statistics.numConsumedForFirstTime" unit="player")]
          b-tr
            b-td Conversion
            b-td(
              v-if="singleActivableData.statistics.numActivatedForFirstTime > 0"
              class="tw-text-right"
              ) {{ conversion }}%
            b-td(
              v-else
              class="text-right text-muted tw-italic"
              ) None
          b-tr
            b-td Total Consumes
            b-td(class="tw-text-right") #[meta-abbreviate-number(:value="singleActivableData.statistics.numConsumed" unit="time")]
          b-tr
            b-td Activation Mode
            b-td(class="tw-text-right")
              MBadge(:variant="singleActivableParams.schedule ? 'neutral' : 'primary'") {{ singleActivableParams.schedule ? 'Scheduled' : 'Dynamic' }}
          // Lifetime overview
          b-tr(v-if="!singleActivableParams.schedule")
            b-td Lifetime After Activation
            b-td(class="tw-text-right")
              span(v-if="lifetimeType === 'Forever'") Forever
              span(v-else-if="lifetimeType === 'Fixed'")
                meta-duration(
                  :duration="singleActivableParams.lifetime.duration"
                  showAs="humanizedSentenceCase"
                  )
              span(
                v-else
                class="text-danger"
                ) Unknown!
          b-tr(v-if="!singleActivableParams.schedule")
            b-td Cooldown After Deactivation
            b-td(class="tw-text-right")
              span(v-if="cooldownType === 'Fixed'")
                span(
                  v-if="durationToMilliseconds(singleActivableParams.cooldown.duration) === 0"
                  class="text-muted tw-italic"
                  ) None
                meta-duration(
                  v-else
                  :duration="singleActivableParams.cooldown.duration"
                  showAs="humanizedSentenceCase"
                  )
              span(
                v-else
                class="text-danger"
                ) Unknown!
          // Schedule overview
          b-tr(v-if="singleActivableParams.schedule")
            b-td(colspan="2")
              b-row(class="tw-mb-4 tw-mt-4")
                b-col(md)
                  span Time Mode:
                    MBadge(
                      :tooltip="singleActivableParams.schedule.timeMode !== 'Utc' ? 'Using UTC time to preview the schedule.' : undefined"
                      class="tw-ml-1"
                      ) {{ metaScheduleTimeModeDisplayString(singleActivableParams.schedule.timeMode) }}
                b-col(md="auto")
                  span Current Phase: #[meta-activable-phase-badge(:activable="singleActivableData")]
                b-col(md="auto")
                  span(v-if="!isEnabled") Next Phase: #[meta-activable-phase-badge(:activable="singleActivableData" phase="Disabled")]
                  span(v-else-if="nextPhase") Next Phase: #[meta-activable-phase-badge(:activable="singleActivableData" :phase="nextPhase")] #[meta-time(:date="nextPhaseStartTime")]
                  span(v-else) No longer occurring
              // Start time label
              div(class="mt-4 tw-w-full")
                div(
                  :style="`margin-left: ${(durationToMilliseconds(singleActivableParams.schedule.preview) / totalDuration) * 100}%; position: relative; left: -1px`"
                  class="pb-3 pl-2 border-left border-dark"
                  )
                  div(class="small font-weight-bold") Start
                  div(
                    v-if="displayedEnabledRange"
                    class="small"
                    )
                    meta-time(:date="displayedEnabledRange.start")
              // Schedule timeline
              b-progress(
                :max="totalDuration"
                height="3rem"
                :style="phase === 'Inactive' ? 'filter: contrast(50%) brightness(130%)' : ''"
                class="font-weight-bold"
                )
                b-progress-bar(
                  :value="durationToMilliseconds(singleActivableParams.schedule.preview)"
                  variant="info"
                  :animated="phase === 'Preview'"
                  ) Preview
                b-progress-bar(
                  :value="durationToMilliseconds(singleActivableParams.schedule.duration) - durationToMilliseconds(singleActivableParams.schedule.endingSoon)"
                  variant="success"
                  :animated="phase === 'Active'"
                  ) Active
                b-progress-bar(
                  :value="durationToMilliseconds(singleActivableParams.schedule.endingSoon)"
                  variant="warning"
                  :animated="phase === 'EndingSoon'"
                  ) Ending soon
                b-progress-bar(
                  :value="durationToMilliseconds(singleActivableParams.schedule.review)"
                  variant="info"
                  :animated="phase === 'Review'"
                  ) Review
              // End time label
              div(class="tw-w-full")
                div(
                  :style="`margin-right: ${(durationToMilliseconds(singleActivableParams.schedule.review) / totalDuration) * 100}%; position: relative; right: -1px`"
                  class="pt-3 pr-2 pb-1 border-right border-dark text-right"
                  )
                  div(class="small font-weight-bold") End
                  div(
                    v-if="displayedEnabledRange"
                    class="small"
                    )
                    meta-time(:date="displayedEnabledRange.end")

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Configuration

  b-row(align-h="center")
    b-col(
      md="9"
      class="tw-mb-4"
      )
      meta-list-card(
        title="Game Specific Configuration"
        icon="sliders-h"
        :tooltip="`How your game deals with this ${kindDisplayName}.`"
        :itemList="gameSpecificInfo"
        data-testid="activable-detail-game-specific-config-card"
        )
        template(#item-card="{ item: info }")
          MListItem {{ info.key }}
            template(#top-right)
              MBadge(v-if="info.value === null") null
              MBadge(
                v-else-if="info.value === true"
                variant="success"
                ) true
              MBadge(
                v-else-if="info.value === false"
                variant="danger"
                ) false
              span(v-else) {{ info.value }}

  b-row(
    v-if="singleActivableParams"
    align-h="center"
    )
    b-col
      b-card(data-testid="activable-detail-configuration-card")
        b-card-title
          b-row(
            align-v="center"
            no-gutters
            )
            fa-icon(
              icon="sliders-h"
              class="mr-2"
              )
            span Activable Configuration
        b-row
          b-col(
            md="6"
            class="tw-mb-4"
            )
            div(
              :class="{ 'bg-light': !singleActivableParams.lifetime || lifetimeType === 'ScheduleBased' }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Lifetime
                MBadge(
                  v-if="!singleActivableParams.lifetime || lifetimeType === 'ScheduleBased'"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(
                v-if="singleActivableParams.lifetime"
                class="tw-my-4"
                )
                div(
                  v-if="lifetimeType === 'ScheduleBased'"
                  class="text-muted tw-text-center"
                  ) This activable's lifetime follows the schedule.
                div(
                  v-else-if="lifetimeType === 'Forever'"
                  class="text-muted tw-text-center"
                  ) This activable exists forever.
                div(
                  v-else-if="lifetimeType === 'Fixed'"
                  class="tw-text-center"
                  ) This activable's lifetime is #[meta-duration(:duration="singleActivableParams.lifetime.duration")].
                div(
                  v-else
                  class="text-muted"
                  ) Unknown lifetime type: {{ lifetimeType }}.
              div(
                v-else
                class="text-muted tw-text-center"
                ) No lifetime defined.

          b-col(
            md="6"
            class="tw-mb-4"
            )
            div(
              :class="{ 'bg-light': !singleActivableParams.cooldown || cooldownType === 'ScheduleBased' || durationToMilliseconds(singleActivableParams.cooldown.duration) === 0 }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Cooldown
                MBadge(
                  v-if="!singleActivableParams.cooldown || cooldownType === 'ScheduleBased' || durationToMilliseconds(singleActivableParams.cooldown.duration) === 0"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(
                v-if="singleActivableParams.cooldown"
                class="tw-my-4"
                )
                div(
                  v-if="cooldownType === 'ScheduleBased'"
                  class="text-muted tw-text-center"
                  ) This activable's cooldown period follows the schedule.
                div(
                  v-else-if="cooldownType === 'Fixed'"
                  :class="{ 'text-muted': durationToMilliseconds(singleActivableParams.cooldown.duration) === 0 }"
                  class="tw-text-center"
                  ) This activable's cooldown period is #[meta-duration(:duration="singleActivableParams.cooldown.duration")].
                div(v-else)
                  div(class="text-muted") Unknown cooldown type: {{ cooldownType }}.
                  pre(style="font-size: 0.7rem") {{ singleActivableParams.cooldown }}
              div(
                v-else
                class="text-muted tw-text-center"
                ) No cooldown defined.

          b-col(
            md="6"
            class="tw-mb-4"
            )
            div(
              :class="{ 'bg-light': !singleActivableParams.schedule }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Schedule
                MBadge(
                  v-if="!singleActivableParams.schedule"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(v-if="singleActivableParams.schedule")
                b-table-simple(
                  v-if="scheduleType === 'MetaRecurringCalendarSchedule'"
                  small
                  style="font-size: 0.85rem"
                  )
                  b-tbody
                    b-tr
                      b-td Time mode
                      b-td(
                        v-if="singleActivableParams.schedule.timeMode !== 'Utc'"
                        class="tw-text-right"
                        ) #[MBadge {{ singleActivableParams.schedule.timeMode }}]
                      b-td(
                        v-else
                        class="tw-text-right"
                        ) #[MBadge {{ metaScheduleTimeModeDisplayString(singleActivableParams.schedule.timeMode) }}]
                    b-tr
                      b-td Start
                      b-td(class="tw-text-right") #[meta-time(:date="singleActivableParams.schedule.start" showAs="datetime")]
                    b-tr
                      b-td Preview
                      b-td(class="tw-text-right")
                        meta-duration(
                          :duration="singleActivableParams.schedule.preview"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Duration
                      b-td(class="tw-text-right")
                        meta-duration(
                          :duration="singleActivableParams.schedule.duration"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Ending Soon
                      b-td(class="tw-text-right")
                        meta-duration(
                          :duration="singleActivableParams.schedule.endingSoon"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Review
                      b-td(class="tw-text-right")
                        meta-duration(
                          :duration="singleActivableParams.schedule.review"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Repeats
                      b-td(
                        v-if="singleActivableParams.schedule.numRepeats === null"
                        class="text-right text-muted tw-italic"
                        ) Unlimited
                      b-td(
                        v-else
                        class="tw-text-right"
                        ) {{ singleActivableParams.maxTotalConsumes }}
                    b-tr
                      b-td Recurrence
                      b-td(class="tw-text-right")
                        div(v-if="singleActivableParams.schedule.recurrence === null") Never
                        meta-duration(
                          v-else
                          :duration="singleActivableParams.schedule.recurrence"
                          showAs="exactDuration"
                          )
                div(v-else)
                  div(class="text-muted") Unknown schedule type: {{ scheduleType }}.
                  pre(style="font-size: 0.7rem") {{ singleActivableParams.schedule }}
              div(
                v-else
                class="text-muted my-auto tw-text-center"
                ) No schedule defined.

          b-col(
            md="6"
            class="tw-mb-4"
            )
            div(class="rounded border py-2 h-100 tw-px-4")
              div(class="mb-2 font-weight-bold") Activations
              b-table-simple(
                small
                style="font-size: 0.85rem"
                class="m-0"
                )
                b-tbody
                  b-tr
                    b-td Max Activations
                    b-td(
                      v-if="singleActivableParams.maxActivations === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="tw-text-right"
                      ) {{ singleActivableParams.maxActivations }}
                  b-tr
                    b-td Max Total Consumes
                    b-td(
                      v-if="singleActivableParams.maxTotalConsumes === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="tw-text-right"
                      ) {{ singleActivableParams.maxTotalConsumes }}
                  b-tr
                    b-td Max Consumed Per Activation
                    b-td(
                      v-if="singleActivableParams.maxConsumesPerActivation === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="tw-text-right"
                      ) {{ singleActivableParams.maxConsumesPerActivation }}

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Targeting
  b-row(align-h="center")
    b-col(
      md="6"
      class="tw-mb-4"
      )
      segments-card(
        :segments="singleActivableParams.segments"
        ownerTitle="This event"
        )
    b-col(
      md="6"
      class="tw-mb-4"
      )
      player-conditions-card(:playerConditions="singleActivableParams.additionalConditions")

  meta-raw-data(
    :kvPair="singleActivableData"
    name="singleActivableData"
    )
  meta-raw-data(
    :kvPair="gameSpecificInfo"
    name="gameSpecificInfo"
    )
</template>

<script lang="ts" setup>
import { BProgress, BProgressBar } from 'bootstrap-vue'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { MBadge, MListItem, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaAudienceSizeEstimate from '../components/MetaAudienceSizeEstimate.vue'
import MetaActivablePhaseBadge from '../components/activables/MetaActivablePhaseBadge.vue'
import PlayerConditionsCard from '../components/global/PlayerConditionsCard.vue'
import SegmentsCard from '../components/global/SegmentsCard.vue'
import {
  durationToMilliseconds,
  metaScheduleTimeModeDisplayString,
  roundTo,
  routeParamToSingleValue,
} from '../coreUtils'
import { getSingleActivableSubscriptionOptions } from '../subscription_options/activables'
import { getStaticConfigSubscriptionOptions } from '../subscription_options/general'

const props = defineProps<{
  kindId: string
}>()

/**
 * Subscribe to data needed on this page.
 * NB: Root point of difference plain activable and offerGroup details page
 */

const route = useRoute()
const activableId = routeParamToSingleValue(route.params.id)

// Activable data ----------------------------------------------------------------------------------------------------

const { data: singleActivableData, error: singleActivableError } = useSubscription(
  getSingleActivableSubscriptionOptions(props.kindId, activableId)
)

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

/**
 * Parameters that define the behavior of the event.
 * @example Lifetime or cooldown durations.
 */
const singleActivableParams = computed(() => {
  return singleActivableData.value?.config.activableParams
})

/**
 * How the game specific info is returned.
 */
interface GameSpecificInfo {
  key: string
  value: any
}

/**
 * Array of additional game-specific details about the event.
 */
const gameSpecificInfo = computed((): GameSpecificInfo[] | undefined => {
  if (!activablesMetadata.value) return undefined

  const gameSpecificMemberNames = activablesMetadata.value.kinds[props.kindId].gameSpecificConfigDataMembers
  return gameSpecificMemberNames.map((memberName) => {
    return {
      key: memberName,
      value: singleActivableData.value?.config[memberName],
    }
  })
})

/**
 *  Additional data about the event.
 */
const activablesMetadata = computed(() => {
  return staticConfigData.value?.activablesMetadata
})

// Activable schedule ----------------------------------------------------------------------------------------------------

/**
 * Specifies whether an event has a fixed or non-fixed lifetime.
 * i.e The duration an event is shown as 'active'.
 */
const lifetimeType = computed(() => {
  return singleActivableParams.value.lifetime.$type.match(
    /Metaplay\.Core\.Activables\.MetaActivableLifetimeSpec\+(.*)/
  )[1]
})

/**
 * Specifies the schedule based activation type that the event follows.
 * An event can be scheduled as a one-time offer or recurres after a set period of time.
 */
const scheduleType = computed(() => {
  return singleActivableParams.value.schedule.$type.match(/Metaplay\.Core\.Schedule\.(.*)/)[1]
})

/**
 * Specifies whether an event has a fixed or non-fixed cooldown duration.
 * i.e The duration that must elapse after an event has expired before it can be shown again.
 */
const cooldownType = computed(() => {
  return singleActivableParams.value.cooldown.$type.match(
    /Metaplay\.Core\.Activables\.MetaActivableCooldownSpec\+(.*)/
  )[1]
})

/**
 * Estimated time it takes to complete all phases.
 */
const totalDuration = computed(() => {
  if (singleActivableParams.value.schedule) {
    return (
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleActivableParams.value.schedule.preview) +
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleActivableParams.value.schedule.duration) +
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleActivableParams.value.schedule.review)
    )
  } else return 1
})

// Activable phases ----------------------------------------------------------------------------------------------------

/**
 * Display name for the current phase.
 */
const phase = computed(() => {
  const scheduleStatus = singleActivableData.value.scheduleStatus
  return scheduleStatus ? scheduleStatus.currentPhase.phase : null
})

/**
 * Display name for the next phase.
 */
const nextPhase = computed(() => {
  const scheduleStatus = singleActivableData.value?.scheduleStatus
  return scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.phase : null
})

/**
 * Indicates when the next phase starts.
 */
const nextPhaseStartTime = computed(() => {
  const scheduleStatus = singleActivableData.value?.scheduleStatus
  return scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.startTime : null
})

/**
 * Indicates if an event is actively running or not.
 */
const isEnabled = computed(() => {
  return singleActivableParams.value.isEnabled
})

/**
 * Start and end time of the current phase.
 */
const displayedEnabledRange = computed(() => {
  const scheduleStatus = singleActivableData.value?.scheduleStatus
  return scheduleStatus ? scheduleStatus.relevantEnabledRange : null
})

// Misc ---------------------------------------------------------------------------------------------------------------

/**
 * Conversion rate for the event.
 */
const conversion = computed(() => {
  return roundTo(
    (singleActivableData.value?.statistics.numConsumedForFirstTime /
      singleActivableData.value?.statistics.numActivatedForFirstTime) *
      100,
    2
  )
})

/**
 * Name of the activable kind displayed.
 */
const kindDisplayName = computed(() => {
  return activablesMetadata.value?.kinds[props.kindId].displayName
})

/**
 * Indicates whether an event is targeted to a specific audience or to the whole player base.
 */
const isTargeted = computed(() => {
  return singleActivableParams.value.segments !== null && singleActivableParams.value.segments.length !== 0
})
</script>

<style scoped>
pre {
  font-size: 0.7rem;
  margin-bottom: 0px;
}
</style>

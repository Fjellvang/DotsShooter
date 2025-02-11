<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.activables.view"
  :is-loading="!singleOfferGroupData"
  :error="singleOfferGroupError"
  :variant="containsOffers ? undefined : 'warning'"
  )
  template(#overview)
    MPageOverviewCard(
      :title="singleOfferGroupData.config.displayName"
      :subtitle="singleOfferGroupData.config.description"
      :id="singleOfferGroupData.config.activableId"
      data-testid="offer-group-detail-overview-card"
      )
      MCallout(
        v-if="!containsOffers"
        title="No Offers Found"
        variant="warning"
        class="tw-mt-3"
        )
        div This offer group contains no offers and thus will not be visible in the game! Did you forget to configure the offers in the game configs?

      div(v-else)
        div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-bar")] Overview
        b-table-simple(
          small
          responsive
          )
          b-tbody
            b-tr
              b-td Status
              b-td(class="text-right")
                MBadge(
                  v-if="isEnabled"
                  variant="success"
                  ) Enabled
                MBadge(
                  v-else
                  variant="danger"
                  ) Disabled
            b-tr
              b-td Placement
              b-td(class="text-right") {{ singleOfferGroupData.config.placement }}
            b-tr
              b-td Priority
              b-td(class="tw-text-right")
                meta-ordinal-number(
                  v-if="singleOfferGroupData.config.priority > 0"
                  :number="singleOfferGroupData.config.priority"
                  )
                span(v-else) {{ singleOfferGroupData.config.priority }}
            b-tr
              b-td Audience Size Estimate
              b-td(class="text-right") #[meta-audience-size-estimate(:sizeEstimate="isTargeted ? singleOfferGroupData.audienceSizeEstimate : undefined")]

        div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-line")] Statistics
        b-table-simple(
          small
          responsive
          )
          b-tbody
            b-tr
              b-td Activated By
                MBadge(
                  tooltip="Number of players who have seen this group."
                  shape="pill"
                  class="tw-ml-1"
                  ) ?
              b-td(class="text-right") #[meta-abbreviate-number(:value="singleOfferGroupData.statistics.numActivatedForFirstTime" unit="player")]
            b-tr
              b-td Consumed By
                MBadge(
                  tooltip="Number of players who have bought something from this group."
                  shape="pill"
                  class="tw-ml-1"
                  ) ?
              b-td(class="text-right") #[meta-abbreviate-number(:value="singleOfferGroupData.statistics.numConsumedForFirstTime" unit="player")]
            b-tr
              b-td Conversion
                MBadge(
                  tooltip="The percentage of players who have bought something from this group after seeing it."
                  shape="pill"
                  class="tw-ml-1"
                  ) ?
              b-td(
                v-if="singleOfferGroupData.statistics.numActivatedForFirstTime > 0"
                class="text-right"
                ) {{ conversion }}%
              b-td(
                v-else
                class="text-right text-muted tw-italic"
                ) None
            b-tr
              b-td Total Consumes
                MBadge(
                  tooltip="Total number of purchases from this group."
                  shape="pill"
                  class="tw-ml-1"
                  ) ?
              b-td(class="text-right") #[meta-abbreviate-number(:value="singleOfferGroupData.statistics.numConsumed" unit="time")]
            b-tr
              b-td Total Revenue
              b-td(class="text-right") ${{ singleOfferGroupData.revenue.toFixed(2) }}

        div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="calendar-alt")] Scheduling
        b-table-simple(
          small
          responsive
          )
          b-tbody
            b-tr
              b-td Activation Mode
              b-td(class="text-right")
                MBadge(:variant="singleOfferGroupParams.schedule ? 'neutral' : 'primary'") {{ singleOfferGroupParams.schedule ? 'Scheduled' : 'Dynamic' }}
            // Lifetime overview
            b-tr(v-if="!singleOfferGroupParams.schedule")
              b-td Lifetime After Activation
              b-td(class="text-right")
                span(v-if="lifetimeType === 'Forever'") Forever
                span(v-else-if="lifetimeType === 'Fixed'")
                  meta-duration(
                    :duration="singleOfferGroupParams.lifetime.duration"
                    showAs="humanizedSentenceCase"
                    )
                span(
                  v-else
                  class="text-danger"
                  ) Unknown!
            b-tr(v-if="!singleOfferGroupParams.schedule")
              b-td Cooldown After Deactivation
              b-td(class="text-right")
                span(v-if="cooldownType === 'Fixed'")
                  span(
                    v-if="durationToMilliseconds(singleOfferGroupParams.cooldown.duration) === 0"
                    class="text-muted tw-italic"
                    ) None
                  meta-duration(
                    v-else
                    :duration="singleOfferGroupParams.cooldown.duration"
                    showAs="humanizedSentenceCase"
                    )
                span(
                  v-else
                  class="text-danger"
                  ) Unknown!
            // Schedule overview
            b-tr(v-if="singleOfferGroupParams.schedule")
              b-td(colspan="2")
                b-row(class="mx-0 tw-my-4")
                  b-col(
                    md
                    class="pl-0"
                    )
                    span Time Mode:
                      MBadge(
                        :tooltip="singleOfferGroupParams.schedule.timeMode !== 'Utc' ? 'Using UTC time to preview the schedule.' : undefined"
                        class="tw-ml-1"
                        ) {{ metaScheduleTimeModeDisplayString(singleOfferGroupParams.schedule.timeMode) }}
                  b-col(md)
                    span Current Phase: #[meta-activable-phase-badge(:activable="singleOfferGroupData")]
                  b-col(md="auto")
                    span(v-if="!isEnabled") Next Phase: #[meta-activable-phase-badge(:activable="singleOfferGroupData" phase="Disabled")]
                    span(v-else-if="nextPhase") Next Phase: #[meta-activable-phase-badge(:activable="singleOfferGroupData" :phase="nextPhase")]
                      div #[meta-time(:date="nextPhaseStartTime")]
                    span(v-else) No longer occurring
                // Start time label
                div(class="tw-w-full")
                  div(
                    :style="`margin-left: ${(durationToMilliseconds(singleOfferGroupParams.schedule.preview) / totalDuration) * 100}%; position: relative; left: -1px`"
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
                    :value="durationToMilliseconds(singleOfferGroupParams.schedule.preview)"
                    variant="info"
                    :animated="phase === 'Preview'"
                    ) Preview
                  b-progress-bar(
                    :value="durationToMilliseconds(singleOfferGroupParams.schedule.duration) - durationToMilliseconds(singleOfferGroupParams.schedule.endingSoon)"
                    variant="success"
                    :animated="phase === 'Active'"
                    ) Active
                  b-progress-bar(
                    :value="durationToMilliseconds(singleOfferGroupParams.schedule.endingSoon)"
                    variant="warning"
                    :animated="phase === 'EndingSoon'"
                    ) Ending soon
                  b-progress-bar(
                    :value="durationToMilliseconds(singleOfferGroupParams.schedule.review)"
                    variant="info"
                    :animated="phase === 'Review'"
                    ) Review
                // End time label
                div(class="tw-w-full")
                  div(
                    :style="`margin-right: ${(durationToMilliseconds(singleOfferGroupParams.schedule.review) / totalDuration) * 100}%; position: relative; right: -1px`"
                    class="pt-3 pr-2 pb-1 border-right border-dark text-right"
                    )
                    div(class="smallfont-weight-bold") End
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
    h3 Contents

  b-row(align-h="center")
    b-col(
      lg="8"
      class="tw-mb-4"
      )
      offer-groups-offers-card(
        :offerGroupId="routeParamToSingleValue(route.params.id)"
        emptyMessage="This offer group contains no offers and thus will not be visible in the game! Did you forget to configure the offers in the game configs?"
        data-testid="offer-group-detail-offers-list-card"
        )

  b-row(
    no-gutters
    align-v="center"
    class="mb-2 tw-mt-4"
    )
    h3 Scheduling

  b-row(
    v-if="singleOfferGroupParams"
    align-h="center"
    class="tw-mb-4"
    )
    b-col(class="tw-mb-4")
      b-card(data-testid="offer-group-detail-configuration-card")
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
              :class="{ 'bg-light': !singleOfferGroupParams.lifetime || lifetimeType === 'ScheduleBased' }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Lifetime
                MBadge(
                  v-if="!singleOfferGroupParams.lifetime || lifetimeType === 'ScheduleBased'"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(
                v-if="singleOfferGroupParams.lifetime"
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
                  ) This activable's lifetime is #[meta-duration(:duration="singleOfferGroupParams.lifetime.duration")].
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
              :class="{ 'bg-light': !singleOfferGroupParams.cooldown || cooldownType === 'ScheduleBased' || durationToMilliseconds(singleOfferGroupParams.cooldown.duration) === 0 }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Cooldown
                MBadge(
                  v-if="!singleOfferGroupParams.cooldown || cooldownType === 'ScheduleBased' || durationToMilliseconds(singleOfferGroupParams.cooldown.duration) === 0"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(
                v-if="singleOfferGroupParams.cooldown"
                class="tw-my-4"
                )
                div(
                  v-if="cooldownType === 'ScheduleBased'"
                  class="text-muted tw-text-center"
                  ) This activable's cooldown period follows the schedule.
                div(
                  v-else-if="cooldownType === 'Fixed'"
                  :class="{ 'text-muted': durationToMilliseconds(singleOfferGroupParams.cooldown.duration) === 0 }"
                  class="tw-text-center"
                  ) This activable's cooldown period is #[meta-duration(:duration="singleOfferGroupParams.cooldown.duration")].
                div(v-else)
                  div(class="text-muted") Unknown cooldown type: {{ cooldownType }}.
                  pre(style="font-size: 0.7rem") {{ singleOfferGroupParams.cooldown }}
              div(
                v-else
                class="text-muted tw-text-center"
                ) No cooldown defined.

          b-col(
            md="6"
            class="tw-mb-4"
            )
            div(
              :class="{ 'bg-light': !singleOfferGroupParams.schedule }"
              class="rounded border py-2 h-100 tw-px-4"
              )
              b-row(
                align-v="center"
                no-gutters
                class="mb-2"
                )
                span(class="font-weight-bold") Schedule
                MBadge(
                  v-if="!singleOfferGroupParams.schedule"
                  class="tw-ml-1"
                  ) Off
                MBadge(
                  v-else
                  variant="success"
                  class="tw-ml-1"
                  ) On
              div(v-if="singleOfferGroupParams.schedule")
                b-table-simple(
                  v-if="scheduleType === 'MetaRecurringCalendarSchedule'"
                  small
                  style="font-size: 0.85rem"
                  )
                  b-tbody
                    b-tr
                      b-td Time mode
                      b-td(
                        v-if="singleOfferGroupParams.schedule.timeMode !== 'Utc'"
                        class="text-right"
                        ) #[MBadge {{ singleOfferGroupParams.schedule.timeMode }}]
                      b-td(
                        v-else
                        class="text-right"
                        ) #[MBadge {{ metaScheduleTimeModeDisplayString(singleOfferGroupParams.schedule.timeMode) }}]
                    b-tr
                      b-td Start
                      b-td(class="text-right") #[meta-time(:date="singleOfferGroupParams.schedule.start" showAs="timeagoSentenceCase")]
                    b-tr
                      b-td Preview
                      b-td(class="text-right")
                        meta-duration(
                          :duration="singleOfferGroupParams.schedule.preview"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Duration
                      b-td(class="text-right")
                        meta-duration(
                          :duration="singleOfferGroupParams.schedule.duration"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Ending Soon
                      b-td(class="text-right")
                        meta-duration(
                          :duration="singleOfferGroupParams.schedule.endingSoon"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Review
                      b-td(class="text-right")
                        meta-duration(
                          :duration="singleOfferGroupParams.schedule.review"
                          showAs="exactDuration"
                          )
                    b-tr
                      b-td Repeats
                      b-td(
                        v-if="singleOfferGroupParams.schedule.numRepeats === null"
                        class="text-right text-muted tw-italic"
                        ) Unlimited
                      b-td(
                        v-else
                        class="text-right"
                        ) {{ singleOfferGroupParams.maxTotalConsumes }}
                    b-tr
                      b-td Recurrence
                      b-td(class="text-right")
                        div(v-if="singleOfferGroupParams.schedule.recurrence === null") Never
                        meta-duration(
                          v-else
                          :duration="singleOfferGroupParams.schedule.recurrence"
                          showAs="exactDuration"
                          )
                div(v-else)
                  div(class="text-muted") Unknown schedule type: {{ scheduleType }}.
                  pre(style="font-size: 0.7rem") {{ singleOfferGroupParams.schedule }}
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
                      v-if="singleOfferGroupParams.maxActivations === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="text-right"
                      ) {{ singleOfferGroupParams.maxActivations }}
                  b-tr
                    b-td Max Total Consumes
                    b-td(
                      v-if="singleOfferGroupParams.maxTotalConsumes === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="text-right"
                      ) {{ singleOfferGroupParams.maxTotalConsumes }}
                  b-tr
                    b-td Max Consumed Per Activation
                    b-td(
                      v-if="singleOfferGroupParams.maxConsumesPerActivation === null"
                      class="text-right text-muted tw-italic"
                      ) Unlimited
                    b-td(
                      v-else
                      class="text-right"
                      ) {{ singleOfferGroupParams.maxConsumesPerActivation }}

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
        :segments="singleOfferGroupParams.segments"
        ownerTitle="This event"
        )
    b-col(
      md="6"
      class="tw-mb-4"
      )
      player-conditions-card(:playerConditions="singleOfferGroupParams.additionalConditions")

  meta-raw-data(
    :kvPair="singleOfferGroupData"
    name="singleOfferGroupData"
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

import { MBadge, MCallout, MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetaAudienceSizeEstimate from '../components/MetaAudienceSizeEstimate.vue'
import MetaActivablePhaseBadge from '../components/activables/MetaActivablePhaseBadge.vue'
import PlayerConditionsCard from '../components/global/PlayerConditionsCard.vue'
import SegmentsCard from '../components/global/SegmentsCard.vue'
import OfferGroupsOffersCard from '../components/offers/OfferGroupsOffersCard.vue'
import {
  durationToMilliseconds,
  metaScheduleTimeModeDisplayString,
  roundTo,
  routeParamToSingleValue,
} from '../coreUtils'
import { getStaticConfigSubscriptionOptions } from '../subscription_options/general'
import { getSingleOfferGroupSubscriptionOptions } from '../subscription_options/offers'

const props = defineProps<{
  kindId: string
}>()

/**
 * Subscribe to the data.
 * NB: Root point of difference plain activable and offerGroup details page
 */

const route = useRoute()
const offerGroupId = routeParamToSingleValue(route.params.id)

// Offer group data ----------------------------------------------------------------------------------------------------

const { data: singleOfferGroupData, error: singleOfferGroupError } = useSubscription(
  getSingleOfferGroupSubscriptionOptions(offerGroupId)
)

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

/**
 * Parameters that define the behavior of the offerGroup.
 * @example Lifetime or cooldown durations.
 */
const singleOfferGroupParams = computed(() => {
  return singleOfferGroupData.value.config.activableParams
})

/**
 * Additional game-specific details about the offer groups
 */
const gameSpecificInfo = computed(() => {
  if (!activablesMetadata.value) return undefined

  const gameSpecificMemberNames = activablesMetadata.value.kinds[props.kindId].gameSpecificConfigDataMembers
  return gameSpecificMemberNames.map((memberName: string) => {
    return {
      key: memberName,
      value: singleOfferGroupData.value.config[memberName],
    }
  })
})

/**
 *  Additional data about the offer groups.
 */
const activablesMetadata = computed(() => {
  return staticConfigData.value?.activablesMetadata
})

// Offer group schedule ----------------------------------------------------------------------------------------------------

/**
 * Specifies whether an offer group has a fixed or non-fixed lifetime.
 * i.e The duration an offer group is shown as 'active'.
 */
const lifetimeType = computed(() => {
  return singleOfferGroupParams.value.lifetime.$type.match(
    /Metaplay\.Core.Activables\.MetaActivableLifetimeSpec\+(.*)/
  )[1]
})

/**
 * Specifies the schedule based activation type that the offer group follows.
 * An offer group can be scheduled as a one-time offer or recurres after a set period of time.
 */
const scheduleType = computed(() => {
  return singleOfferGroupParams.value.schedule.$type.match(/Metaplay\.Core\.Schedule\.(.*)/)[1]
})

/**
 * Specifies whether an offer group has a fixed or non-fixed cooldown duration.
 * i.e The duration that must elapse after an offer Group has expired before it can be shown again.
 */
const cooldownType = computed(() => {
  return singleOfferGroupParams.value.cooldown.$type.match(
    /Metaplay\.Core\.Activables\.MetaActivableCooldownSpec\+(.*)/
  )[1]
})

/**
 * Estimated time it takes to complete all phases.
 */
const totalDuration = computed(() => {
  if (singleOfferGroupParams.value.schedule) {
    return (
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleOfferGroupParams.value.schedule.preview) +
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleOfferGroupParams.value.schedule.duration) +
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      durationToMilliseconds(singleOfferGroupParams.value.schedule.review)
    )
  } else return 1
})

// Offer group phases ----------------------------------------------------------------------------------------------------

/**
 * Display name for the current phase.
 */
const phase = computed(() => {
  const scheduleStatus = singleOfferGroupData.value?.scheduleStatus
  return scheduleStatus ? scheduleStatus.currentPhase.phase : null
})

/**
 * Display name for the next phase.
 */
const nextPhase = computed(() => {
  const scheduleStatus = singleOfferGroupData.value?.scheduleStatus
  return scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.phase : null
})

/**
 * Indicates when the next phase starts.
 */
const nextPhaseStartTime = computed(() => {
  const scheduleStatus = singleOfferGroupData.value.scheduleStatus
  return scheduleStatus?.nextPhase ? scheduleStatus.nextPhase.startTime : null
})

/**
 * Start and end time for the current offer group phase.
 */
const displayedEnabledRange = computed(() => {
  const scheduleStatus = singleOfferGroupData.value.scheduleStatus
  return scheduleStatus ? scheduleStatus.relevantEnabledRange : null
})

// Misc ---------------------------------------------------------------------------------------------------------------

/**
 * Number of players who have consumed the selected offer.
 */
const conversion = computed(() => {
  return roundTo(
    (singleOfferGroupData.value.statistics.numConsumedForFirstTime /
      singleOfferGroupData.value.statistics.numActivatedForFirstTime) *
      100,
    2
  )
})

/**
 * Check if the offer group contains multiple sub-offers.
 */
const containsOffers = computed(() => {
  return (singleOfferGroupData.value?.config.offers || []).length > 0
})

/**
 * Indicates whether an offer group is targeted to a specific audience or to the whole player base.
 */
const isTargeted = computed(() => {
  return singleOfferGroupParams.value.segments !== null && singleOfferGroupParams.value.segments.length !== 0
})

/**
 * Indicates if an offer group is actively running or not.
 */
const isEnabled = computed(() => {
  return singleOfferGroupParams.value.isEnabled
})
</script>

<style scoped>
pre {
  font-size: 0.7rem;
  margin-bottom: 0px;
}
</style>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Normal schedule
div(v-if="singleLeagueData && singleLeagueData.state.currentSeason && singleLeagueData.currentOrNextSeasonSchedule")
  b-row(class="tw-my-4")
    b-col(md)
      span Time mode: #[MBadge(class="tw-ml-1") {{ metaScheduleTimeModeDisplayString(singleLeagueData.schedule.timeMode) }}]
    b-col(md="auto")
      span Next phase:
        span(
          v-if="singleLeagueData.enabled"
          class="tw-ml-1"
          )
          MBadge(
            :variant="getPhaseVariant(singleLeagueData.currentOrNextSeasonSchedule.nextPhase.phase)"
            class="tw-mr-1"
            ) {{ schedulePhaseDisplayString(singleLeagueData.currentOrNextSeasonSchedule.nextPhase.phase) }}
          meta-time(:date="singleLeagueData.currentOrNextSeasonSchedule.nextPhase.startTime")
        span(
          v-else
          class="tw-ml-1"
          )
          MBadge(variant="danger") Disabled

  //- Start time label
  div(class="tw-w-full")
    div(
      :style="`margin-left: ${(Duration.fromISO(singleLeagueData.schedule.preview).toMillis() / totalDurationInMilliseconds) * 100}%; position: relative; left: -1px`"
      class="pb-3 pl-2 border-left border-dark"
      )
      div(class="small font-weight-bold") Start
      div(class="small")
        meta-time(:date="singleLeagueData.currentOrNextSeasonSchedule.start")
  //- Schedule timeline
  b-progress(
    :max="totalDurationInMilliseconds"
    height="3rem"
    :style="singleLeagueData.currentOrNextSeasonSchedule.currentPhase.phase === 'Inactive' ? 'filter: contrast(50%) brightness(130%)' : ''"
    class="font-weight-bold"
    )
    b-progress-bar(
      :value="Duration.fromISO(singleLeagueData.schedule.preview).toMillis()"
      variant="primary"
      :animated="singleLeagueData.enabled && singleLeagueData.currentOrNextSeasonSchedule.currentPhase.phase === 'Preview'"
      ) Preview
    b-progress-bar(
      :value="Duration.fromISO(singleLeagueData.schedule.duration).toMillis() - Duration.fromISO(singleLeagueData.schedule.endingSoon).toMillis()"
      variant="success"
      :animated="singleLeagueData.enabled && singleLeagueData.currentOrNextSeasonSchedule.currentPhase.phase === 'Active'"
      ) Active
    b-progress-bar(
      :value="Duration.fromISO(singleLeagueData.schedule.endingSoon).toMillis()"
      variant="warning"
      :animated="singleLeagueData.enabled && singleLeagueData.currentOrNextSeasonSchedule.currentPhase.phase === 'EndingSoon'"
      ) Ending Soon
  //- End time label
  div(class="tw-w-full")
    div(
      :style="`position: relative; right: -1px`"
      class="pt-3 pr-2 pb-1 border-right border-dark text-right"
      )
      div(class="small font-weight-bold") End
      div(class="small")
        meta-time(:date="singleLeagueData.currentOrNextSeasonSchedule.end")
</template>

<script lang="ts" setup>
import { BProgress, BProgressBar } from 'bootstrap-vue'
import { Duration } from 'luxon'
import { computed } from 'vue'

import { MBadge } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { metaScheduleTimeModeDisplayString, schedulePhaseDisplayString } from '../../../coreUtils'
import { getPhaseVariant } from '../../../leagueUtils'
import { getSingleLeagueSubscriptionOptions } from '../../../subscription_options/leagues'

const props = defineProps<{
  leagueId: string
}>()

const { data: singleLeagueData } = useSubscription(getSingleLeagueSubscriptionOptions(props.leagueId))

const totalDurationInMilliseconds = computed(() => {
  return (
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Duration.fromISO(singleLeagueData.value.schedule.preview).toMillis() +
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Duration.fromISO(singleLeagueData.value.schedule.duration).toMillis()
  )
})
</script>

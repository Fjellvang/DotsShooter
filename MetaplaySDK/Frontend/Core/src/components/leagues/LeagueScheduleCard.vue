<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Schedule"
  data-testid="league-schedule-card"
  )
  template(#icon)
    fa-icon(:icon="['fas', 'calendar-alt']")

  div(class="font-weight-bold tw-mb-1") Configuration
  b-table-simple(
    v-if="singleLeagueData.schedule"
    small
    style="font-size: 0.85rem"
    )
    b-tbody
      b-tr(v-if="!singleLeagueData.enabled")
        b-td Schedule status
        b-td(class="tw-text-right") #[MBadge(variant="danger") Inactive]
      b-tr
        b-td Time mode
        b-td(class="tw-text-right") #[MBadge {{ metaScheduleTimeModeDisplayString(singleLeagueData.schedule.timeMode) }}]
      b-tr
        b-td First season start
        b-td(class="tw-text-right") #[meta-time(:date="singleLeagueData.schedule.start" showAs="datetime")]
      b-tr
        b-td Preview time
        b-td(class="tw-text-right") #[meta-duration(:duration="singleLeagueData.schedule.preview" showAs="exactDuration")] before start
      b-tr
        b-td Season duration
        b-td(class="tw-text-right")
          meta-duration(
            :duration="singleLeagueData.schedule.duration"
            showAs="exactDuration"
            )
      b-tr
        b-td Ending soon time
        b-td(class="tw-text-right") #[meta-duration(:duration="singleLeagueData.schedule.endingSoon" showAs="exactDuration")] before end
      b-tr
        // TODO: Is this always the same as the season duration + preview? If so, remove?
        b-td Recurrence
        b-td(class="tw-text-right")
          meta-duration(
            v-if="!!singleLeagueData.schedule.recurrence"
            :duration="singleLeagueData.schedule.recurrence"
            showAs="exactDuration"
            )
          div(v-else) Never
  div(
    v-else
    class="text-warning"
    ) No schedule defined!
</template>

<script lang="ts" setup>
import { MBadge, MCard } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { metaScheduleTimeModeDisplayString } from '../../coreUtils'
import { getSingleLeagueSubscriptionOptions } from '../../subscription_options/leagues'

const props = defineProps<{
  leagueId: string
}>()

const { data: singleLeagueData } = useSubscription(getSingleLeagueSubscriptionOptions(props.leagueId))
</script>

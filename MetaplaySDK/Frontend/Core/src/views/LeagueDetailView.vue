<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!singleLeagueData"
  :error="singleLeagueError"
  :alerts="alerts"
  permission="api.leagues.view"
  )
  template(#overview)
    MPageOverviewCard(
      :id="leagueId"
      :title="singleLeagueData.details.leagueDisplayName"
      :error="seasonMigrationError"
      data-testid="league-detail-overview-card"
      )
      p {{ singleLeagueData.details.leagueDescription }}

      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        class="mb-0"
        )
        b-tbody
          b-tr
            b-td League status
            b-td(class="tw-text-right")
              MBadge(
                v-if="singleLeagueData.enabled"
                variant="success"
                ) Enabled
              MBadge(
                v-else
                variant="danger"
                ) Disabled
          b-tr(v-if="singleLeagueData.enabled")
            b-td Total lifetime participants
            b-td(class="tw-text-right") {{ singleLeagueData.currentParticipantCount }}
          b-tr
            b-td Latest season
            b-td(class="tw-text-right")
              span(data-testid="latest-season-link")
                MTextButton(
                  v-if="singleLeagueData.state.currentSeason"
                  :to="`/leagues/${leagueId}/${singleLeagueData.state.currentSeason.seasonId}`"
                  permission="api.leagues.view"
                  data-testid="latest-season-button-link"
                  ) Season {{ singleLeagueData.state.currentSeason.seasonId }}
                span(
                  v-else
                  class="text-right text-muted tw-italic"
                  ) No seasons
          b-tr(v-if="singleLeagueData.enabled && singleLeagueData.currentOrNextSeasonSchedule")
            b-td Current phase
            b-td(class="tw-text-right")
              MBadge(
                v-if="singleLeagueData.enabled"
                :variant="getPhaseVariant(singleLeagueData.currentOrNextSeasonSchedule?.currentPhase.phase)"
                ) {{ schedulePhaseDisplayString(singleLeagueData.currentOrNextSeasonSchedule?.currentPhase.phase) }}
              MBadge(
                v-else
                variant="danger"
                ) Disabled
          b-tr(v-if="singleLeagueData.enabled")
            b-td(colspan="2")

      template(#alerts)
        MCallout(
          v-if="singleLeagueData.migrationProgress.isInProgress"
          title="Season Migration in Progress"
          variant="neutral"
          class="tw-mb-3"
          )
          div(class="tw-text-xs+") Players will be able to join this season once the migrations are complete.
          MProgressBar(
            :value="singleLeagueData.migrationProgress.progressEstimate"
            :max="1"
            )
          div(class="small mt-2 tw-text-center tw-italic") {{ singleLeagueData.migrationProgress.phase }}...

        league-progress-card(
          v-if="singleLeagueData.enabled && !!singleLeagueData.schedule"
          :leagueId="leagueId"
          )
        MCallout(
          v-else
          title="No Schedule"
          variant="warning"
          ) No schedule defined for this league.

      template(#buttons)
        div(v-if="!seasonMigrationError")
          //- Force a season to end.
          MActionModalButton(
            v-if="seasonCurrentPhase === 'Active' || seasonCurrentPhase === 'EndingSoon'"
            modal-title="Force Season to End"
            :action="() => forceSeasonChange(true)"
            trigger-button-label="End Season Now"
            variant="danger"
            permission="api.leagues.phase_debug"
            data-testid="force-end-season"
            )
            p You can force #[span(class="tw-font-semibold") season {{ singleLeagueData?.state.currentSeason.seasonId }}] to end immediately.
            p The next season will start automatically according to the configured schedule.

            p(class="tw-text-xs+ tw-text-neutral-500") Performance note: ending a season early will cause all of the division actors to be woken up. Triggering this in a live game with a large amount of divisions will have a performance impact.

            meta-no-seatbelts(
              message="This action can't be undone. Are you sure the league participants are okay with this?"
              variant="danger"
              class="mt-4"
              )

          //- Force season to start
          MActionModalButton(
            v-else-if="seasonCurrentPhase === 'Preview'"
            modal-title="Force Season To Start"
            :action="() => forceSeasonChange(false)"
            trigger-button-label="Start Season Now"
            :trigger-button-disabled-tooltip="!singleLeagueData.enabled ? 'This league is disabled.' : undefined"
            :ok-button-disabled-tooltip="!singleLeagueData.state.currentSeason.migrationComplete ? 'You cannot force this season to start as season migrations are still ongoing.' : undefined"
            variant="danger"
            permission="api.leagues.phase_debug"
            data-testid="force-end-season"
            )
            p You can force the season to go to #[MBadge(:variant="getPhaseVariant('Active')") Active] immediately.
            p The season will then progress automatically according to its configured schedule.

            b-alert(
              :show="!singleLeagueData.state.currentSeason.migrationComplete"
              variant="danger"
              class="mt-4 tw-text-center"
              ) Season migrations are still ongoing. Please wait for them to finish before forcing this season to start.

          //- Force season to preview.
          MActionModalButton(
            v-else
            modal-title="Force Preview Next Season"
            :action="() => forceSeasonChange(false)"
            trigger-button-label="Preview Next Season"
            :trigger-button-disabled-tooltip="!singleLeagueData.enabled ? 'This league is disabled.' : undefined"
            variant="danger"
            permission="api.leagues.phase_debug"
            data-testid="force-preview-season"
            )
            p You can force the next season to go to #[MBadge(:variant="getPhaseVariant('Preview')") Preview] immediately.
            p The season will progress automatically according to its configured schedule.

  template(#default)
    core-ui-placement(
      :placement-id="'Leagues/Details'"
      :leagueId="leagueId"
      )

    meta-raw-data(
      :kvPair="singleLeagueData"
      name="singleLeagueData"
      )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  DisplayError,
  MProgressBar,
  MTextButton,
  MViewContainer,
  MPageOverviewCard,
  type MViewContainerAlert,
  MCallout,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import LeagueProgressCard from '../components/leagues/leaguedetails/LeagueProgressCard.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue, schedulePhaseDisplayString } from '../coreUtils'
import { getPhaseVariant } from '../leagueUtils'
import { getSingleLeagueSubscriptionOptions } from '../subscription_options/leagues'

const route = useRoute()
const leagueId = routeParamToSingleValue(route.params.leagueId)

const {
  data: singleLeagueData,
  error: singleLeagueError,
  refresh: singleLeagueRefresh,
} = useSubscription(getSingleLeagueSubscriptionOptions(leagueId))

const gameServerApi = useGameServerApi()

/**
 * The current phase of the latest season.
 */
const seasonCurrentPhase = computed(() => singleLeagueData.value?.currentOrNextSeasonSchedule?.currentPhase.phase)

/**
 * Season migration error.
 */
const seasonMigrationError = computed(() => {
  if (singleLeagueData.value?.migrationProgress.error) {
    return new DisplayError(
      'Season migration error',
      'Something went wrong during the season migration process. Contact your game team for assistance.',
      undefined,
      undefined,
      [
        {
          title: 'Internal Error',
          content: singleLeagueData.value?.migrationProgress.error,
        },
      ]
    )
  }
  return undefined
})

/**
 * Array of alert messages to be displayed at the top of this page.
 */
const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []
  if (singleLeagueData.value?.enabled === false) {
    allAlerts.push({
      title: 'Disabled League',
      message:
        'You are currently viewing a disabled league and there are no active seasons. Set up the leagues feature in your runtime options.',
      variant: 'danger',
    })
  }
  if (singleLeagueData.value?.state.currentSeason?.endedEarly === true) {
    allAlerts.push({
      title: 'Latest Season Ended Early',
      message: 'The latest season was forced to end before its configured schedule.',
      variant: 'warning',
    })
  }
  if (singleLeagueData.value?.state.currentSeason?.startedEarly === true) {
    allAlerts.push({
      title: 'Current Season Started Early',
      message: 'The current season was forced to start before its configured schedule.',
      variant: 'warning',
    })
  }
  return allAlerts
})

const { showSuccessNotification, showErrorNotification } = useNotifications()

/**
 * Async function to force a season to advance to next phase.
 */
async function forceSeasonChange(endSeason: boolean): Promise<void> {
  const response = await gameServerApi.post(`/leagues/${leagueId}/advance`, {
    isSeasonEnd: endSeason,
  })
  if (response.data.success) {
    showSuccessNotification(
      endSeason ? 'Current season successfully ended early!' : 'Current season successfully advanced!'
    )
  } else {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    showErrorNotification(response.data.errorMessage)
  }
  singleLeagueRefresh()
}
</script>

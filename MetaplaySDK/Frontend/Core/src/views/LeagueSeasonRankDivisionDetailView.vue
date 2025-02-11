<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!singleDivisionData || !singleLeagueData || !singleLeagueSeasonData"
  :alerts="alerts"
  permission="api.leagues.view"
  )
  template(#overview)
    MPageOverviewCard(
      :id="divisionId"
      :title="`Division ${singleDivisionData.model.divisionIndex.division}`"
      data-testid="league-season-rank-division-overview"
      )
      template(#subtitle)
        span #[MTextButton(:to="`/leagues/${leagueId}`") {{ singleLeagueData.details.leagueDisplayName }}] / #[MTextButton(:to="`/leagues/${leagueId}/${seasonId}`") {{ singleLeagueSeasonData.displayName }}] / Division {{ singleDivisionData.model.divisionIndex.division }} of {{ singleLeagueSeasonData?.ranks[rankId].rankName }}

      //- Standard view
      div(
        style="font-size: 130%"
        class="d-md-flex justify-content-around mt-5 mb-5 d-none"
        )
        MBadge(
          :variant="currentPhase === 'Preview' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Preview
        fa-icon(
          icon="arrow-right"
          class="mt-2"
          )
        MBadge(
          :variant="currentPhase === 'Ongoing' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Ongoing
        fa-icon(
          icon="arrow-right"
          class="mt-2"
          )
        MBadge(
          :variant="currentPhase === 'Resolving' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Resolving
        fa-icon(
          icon="arrow-right"
          class="mt-2"
          )
        MBadge(
          :variant="currentPhase === 'Concluded' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Concluded

      //- Mobile view
      div(
        style="font-size: 130%"
        class="mt-5 mb-5 d-md-none tw-text-center"
        )
        MBadge(
          :variant="currentPhase === 'Preview' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Preview
        div: fa-icon(icon="arrow-down")
        MBadge(
          :variant="currentPhase === 'Ongoing' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Ongoing
        div: fa-icon(icon="arrow-down")
        MBadge(
          :variant="currentPhase === 'Resolving' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Resolving
        div: fa-icon(icon="arrow-down")
        MBadge(
          :variant="currentPhase === 'Concluded' ? 'primary' : 'neutral'"
          class="mx-md-2"
          ) Concluded

      div(class="font-weight-bold tw-mb-1") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(small)
        b-tbody
          b-tr
            b-td Total participants
            b-td(class="text-right") {{ currentParticipantCount }} / {{ singleDivisionData.desiredParticipants }}
          b-tr
            b-td Created at
            b-td(class="text-right") #[meta-time(:date="singleDivisionData.model.createdAt" showAs="datetime")]
          b-tr
            b-td Start time
            b-td(class="text-right") #[meta-time(:date="singleDivisionData.model.startsAt" showAs="timeagoSentenceCase")]
          b-tr
            b-td End time
            b-td(class="text-right") #[meta-time(:date="singleDivisionData.model.endsAt" showAs="timeagoSentenceCase")]

      template(#buttons)
        MActionModalButton(
          modal-title="Add Participant"
          :action="forceAddParticipant"
          trigger-button-label="Add Participant"
          :trigger-button-disabled-tooltip="singleDivisionData.model.isConcluded ? 'You can only add a participant to a division in an active season.' : undefined"
          ok-button-label="Add Participant"
          :ok-button-disabled-tooltip="okButtonDisabledReason"
          variant="warning"
          permission="api.leagues.participant_edit"
          @show="resetParticipantModal"
          data-testid="action-add-participant"
          )
          p You can manually add new participants to this division.

          MCallout(
            title="Participant limit reached"
            :show="currentParticipantCount >= singleDivisionData.desiredParticipants"
            variant="danger"
            class="tw-my-2"
            )
            p(v-if="currentParticipantCount > singleDivisionData.desiredParticipants") This division already has #[span(class="font-weight-bold") {{ currentParticipantCount }} participants], which exceeds the recommended limit of #[span(class="font-weight-bold") {{ singleDivisionData.desiredParticipants }} participants]. Check with your game team if adding another participant might cause issues.
            p(v-else-if="currentParticipantCount === singleDivisionData.desiredParticipants") You are about to exceed the recommended number of #[span(class="font-weight-bold") {{ singleDivisionData.desiredParticipants }} participants] for this division. Check with your game team if this might cause issues.

          span(class="font-weight-bold") Select Player
          meta-input-player-select(
            :value="chosenPlayer"
            :ignorePlayerIds="Object.keys(singleDivisionData.model.participants)"
            class="tw-mt-1"
            @input="chosenPlayer = $event"
            )

          MCallout(
            v-if="singleDivisionData.model.isConcluded"
            title="Season concluded"
            show
            variant="danger"
            class="mt-2"
            ) The season has now concluded. You can no longer add participants to this division.
          meta-no-seatbelts(
            v-else
            message="If the player is participating in a another division, their progress will be reset."
            :name="chosenPlayer?.name"
            class="mt-2"
            )

    //- Navigation buttons
    div(class="d-flex justify-content-between tw-mt-4")
      MTextButton(
        :to="previousDivisionLink"
        :disabled-tooltip="previousDivisionTooltip"
        ) #[fa-icon(icon="arrow-left" class="tw-mr-1")] View previous division

      MTextButton(
        :to="nextDivisionLink"
        :disabled-tooltip="nextDivisionTooltip"
        ) View next division #[fa-icon(icon="arrow-right" class="tw-ml-1")]

  MSingleColumnLayout
    core-ui-placement(
      placementId="Leagues/Season/RankDivision/Details"
      :leagueId="leagueId"
      :divisionId="divisionId"
      )

  meta-raw-data(
    :kvPair="singleLeagueSeasonData"
    name="seasonData"
    )
  meta-raw-data(
    :kvPair="singleDivisionData"
    name="divisionData"
    )
</template>

<script lang="ts" setup>
import { computed, nextTick, ref, watch } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MBadge,
  MCallout,
  MPageOverviewCard,
  MSingleColumnLayout,
  MTextButton,
  MViewContainer,
  useNotifications,
  type MViewContainerAlert,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { routeParamToSingleValue } from '../coreUtils'
import {
  getSingleLeagueSubscriptionOptions,
  getSingleDivisionSubscriptionOptions,
  getSingleLeagueSeasonSubscriptionOptions,
} from '../subscription_options/leagues'

const gameServerApi = useGameServerApi()
const route = useRoute()

/**
 * Entity Id of the division that we are currently viewing.
 */
const divisionId = routeParamToSingleValue(route.params.divisionId)

/**
 * Entity Id of the league that we are currently viewing.
 */
const leagueId = routeParamToSingleValue(route.params.leagueId)

/**
 * Season Id of the division that we are currently viewing.
 */
const seasonId = parseInt(routeParamToSingleValue(route.params.seasonId))

/**
 * Subscribe to League data so that we can get league name for subtitle breadcrumb.
 * TODO: pass league name down to season or division in the future.
 */
const { data: singleLeagueData } = useSubscription(getSingleLeagueSubscriptionOptions(leagueId))

/**
 * Subscribe to the displayed division's data.
 */
const { data: singleDivisionData } = useSubscription(getSingleDivisionSubscriptionOptions(divisionId))

/**
 * Subscribe to season data so that we can find information about the parent season.
 */
const { data: singleLeagueSeasonData } = useSubscription(getSingleLeagueSeasonSubscriptionOptions(leagueId, seasonId))

/**
 * Rank Id for the division that we are currently viewing.
 */
const rankId = computed(() => {
  return singleDivisionData.value?.model.divisionIndex.rank
})

const currentParticipantCount = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return Object.keys(singleDivisionData.value.model.participants).length
})

const alerts = computed((): MViewContainerAlert[] => {
  if (singleDivisionData.value?.model.isConcluded === true) {
    return [
      {
        title: 'Past Division',
        message: 'You are currently viewing a division from a past season.',
        variant: 'neutral',
      },
    ]
  }
  return []
})

const okButtonDisabledReason = computed(() => {
  if (!chosenPlayer.value) return 'Select a player to proceed.'
  if (singleDivisionData.value?.model.isConcluded) {
    return 'You can only add a participant to a division in an active season.'
  }
  return undefined
})

// Navigating to Previous and Next division ---------------------------------------------------------------------------

/**
 * The number of divisions available in this season/rank.
 */
const numDivisions = computed((): number => {
  const rankId = singleDivisionData.value.model.divisionIndex.rank
  return singleLeagueSeasonData.value?.ranks[rankId].numDivisions || 0
})

/**
 * This will be populated to contain the link to the previous division in this rank.
 */
const previousDivisionLink = ref<string>()

/**
 * This will be populated to contain the link to the next division in this rank.
 */
const nextDivisionLink = ref<string>()

const previousDivisionTooltip = ref<string>()

const nextDivisionTooltip = ref<string>()

/**
 * When the division data is loaded, we can look up what the next/previous division links should point to.
 */
const unwatchSingleDivisionData = watch(
  [singleDivisionData],
  async () => {
    if (singleDivisionData.value) {
      const rank = singleDivisionData.value.model.divisionIndex.rank
      const division = singleDivisionData.value.model.divisionIndex.division

      // If we're not on division 0, then look up a link to the previous division.
      if (division > 0) {
        const response = await gameServerApi.get(`/divisions/id/${leagueId}/${seasonId}/${rank}/${division - 1}/`)
        previousDivisionLink.value = `/leagues/${leagueId}/${seasonId}/${response.data}`
      } else {
        previousDivisionTooltip.value = 'You are in the first division.'
      }

      // If we're not on the last division, then look up a link to the next division.
      if (division < numDivisions.value - 1) {
        const response = await gameServerApi.get(`/divisions/id/${leagueId}/${seasonId}/${rank}/${division + 1}/`)
        nextDivisionLink.value = `/leagues/${leagueId}/${seasonId}/${response.data}`
      } else {
        nextDivisionTooltip.value = 'You are in the last division.'
      }

      // We only want to do this once, so unwatch now. If that happens immediately (ie: data was already  cached) then
      // `unwatchSingleDivisionData` won't exist yet, so we need to delay this for a frame.
      void nextTick(() => {
        unwatchSingleDivisionData()
      })
    }
  },
  { immediate: true }
)

// Force division phase ----------------------------------------------------------------------------------------------

/**
 * Display name for the current division phase.
 * Either 'Preview' | 'Ongoing' | 'Resolving' | 'Concluded' | 'NoDivision'
 * TODO: move to API response
 */
const currentPhase = computed(() => (singleDivisionData.value?.seasonPhase as string) || 'NoDivision')

// Add Particiapnt to Division ----------------------------------------------------------------------------------------

/**
 * The selected player.
 */
const chosenPlayer = ref()

/**
 * Reset participant modal.
 */
function resetParticipantModal(): void {
  chosenPlayer.value = undefined
}

const { showSuccessNotification, showErrorNotification } = useNotifications()

/**
 * Add a participant to a division.
 */
async function forceAddParticipant(): Promise<void> {
  if (chosenPlayer.value) {
    const response = await gameServerApi.post(
      `/leagues/${leagueId}/participant/${chosenPlayer.value?.id}/add/${divisionId}`
    )
    if (response.data.success) {
      showSuccessNotification('Participant successfully added!')
    } else {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      showErrorNotification(response.data.errorMessage)
    }
  }
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!singleMatchmakerData"
  :error="singleMatchmakerError"
  :alerts="alerts"
  permission="api.matchmakers.view"
  )
  template(#overview)
    //- Overview
    MPageOverviewCard(
      v-if="singleMatchmakerData"
      :id="matchmakerId"
      :title="singleMatchmakerData.data.name"
      :subtitle="singleMatchmakerData.data.description"
      data-testid="matchmaker-overview-card"
      )
      template(#caption) Save file size:&nbsp;
        MTextButton(
          permission="api.database.inspect_entity"
          :to="`/entities/${matchmakerId}/dbinfo`"
          data-testid="model-size-link"
          )
          meta-abbreviate-number(
            :value="singleMatchmakerData.data.stateSizeInBytes"
            unit="byte"
            )

      div(class="font-weight-bold") #[fa-icon(icon="chart-bar")] Overview
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Number of participants
            b-td(class="text-right") {{ singleMatchmakerData.data.playersInBuckets }}
          b-tr
            b-td Current capacity
            b-td(
              v-if="singleMatchmakerData?.data?.bucketsOverallFillPercentage > 0"
              class="text-right"
              ) {{ Math.round(singleMatchmakerData.data.bucketsOverallFillPercentage * 10000) / 100 }}%
            b-td(
              v-else
              class="text-right text-muted tw-italic"
              ) None
          //- b-tr
            b-td Number of matches during the last hour
            b-td.text-right TBD

      div(class="font-weight-bold") #[fa-icon(icon="scale-unbalanced")] Rebalancing
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Last rebalanced
            b-td(class="tw-text-right")
              meta-time(:date="singleMatchmakerData.data.lastRebalanceOperationTime")

      p(
        v-if="singleMatchmakerData.data.playerScanErrorCount && singleMatchmakerData.data.playerScanErrorCount > 0"
        class="small text-muted"
        ) The matchmaker encountered (#[span(class="font-weight-bold") {{ scanningErrorRate.toFixed(2) }}])% error rate while scanning for players (#[span(class="font-weight-bold") {{ singleMatchmakerData.data.playerScanErrorCount }}] out of #[span(class="font-weight-bold") {{ singleMatchmakerData.data.scannedPlayersCount }}] players scanned). See the server logs for more information.

      template(#buttons)
        div(class="tw-mt-3 tw-flex tw-justify-end tw-gap-2")
          //- Simulation modal
          MButton(
            @click="simulateModal?.open"
            data-testid="simulate-matchmaking-button"
            ) Simulate

          MModal(
            ref="simulateModal"
            title="Simulate Matchmaking"
            data-testid="simulate-matchmaking-modal"
            )
            template(#default)
              div(class="tw-border-r-2 tw-border-neutral-200 tw-pr-4")
                p You can use this tool to preview the matches this matchmaker would return for a given matchmaking ranking (MMR).
                meta-generated-form(
                  v-model="simulationData"
                  :typeName="singleMatchmakerData.queryJsonType"
                  addTypeSpecifier
                  class="tw-mb-3"
                  @status="isSimulationFormValid = $event"
                  )
                MButton(
                  :disabled-tooltip="!isSimulationFormValid ? 'Please complete the form before simulating matchmaking.' : undefined"
                  @click="simulateMatchmaking"
                  data-testid="simulate-matchmaking-ok-button"
                  ) Simulate

            template(#right-panel)
              h5 Matchmaking Results #[span(v-if="simulationResult?.numTries" class="tw-text-xs tw-normal-case") After #[meta-plural-label(:value="simulationResult.numTries" label="iteration")]]

              //- Simulation is running.
              div(
                v-if="isSimulationRunning"
                class="tw-pt-3 tw-text-center tw-italic tw-text-neutral-400"
                )
                span Simulating...

              //- Error.
              div(v-else-if="simulationResult?.response?.data?.error")
                MErrorCallout(:error="simulationResult.response.data.error")

              //- Results.
              div(v-else-if="simulationResult?.response?.responseType === 'Success' && previewCandidateData")
                MList(
                  showBorder
                  class="tw-px-3"
                  data-testid="simulation-results-list"
                  )
                  MListItem {{ previewCandidateData.model.playerName }}
                    template(#top-right) {{ previewCandidateData.id }}
                    template(#bottom-right): MTextButton(
                      permission="api.players.view"
                      :to="`/players/${simulationResult.response.bestCandidate}`"
                      ) View player

              //- No results.
              div(
                v-else-if="simulationResult?.response?.responseType"
                class="tw-pt-4"
                )
                p No matches found!

              //- Haven't run the simulation at all yet.
              div(
                v-else
                class="tw-pt-3 tw-text-center tw-italic tw-text-neutral-400"
                )
                | Simulation not run yet.

          //- Re-balance modal
          MActionModalButton(
            modal-title="Rebalance Matchmaker"
            :action="rebalanceMatchmaker"
            trigger-button-label="Rebalance"
            ok-button-label="Rebalance"
            :ok-button-disabled-tooltip="!singleMatchmakerData.data.hasEnoughDataForBucketRebalance ? 'This matchmaker does not have enough data to rebalance.' : undefined"
            permission="api.matchmakers.admin"
            data-testid="rebalance-matchmaker"
            )
            p Rebalancing this matchmaker will re-distribute participants to the configured matchmaking buckets.
            p(class="text-muted small") Matchmakers automatically rebalance themselves over time. Manually triggering the rebalancing is mostly useful for manual testing during development.

            b-alert(
              :show="!singleMatchmakerData.data.hasEnoughDataForBucketRebalance"
              variant="danger"
              data-testid="rebalance-matchmaker-not-enough-data"
              ) This matchmaker does not have enough data to rebalance. Please wait until the matchmaker has been populated with enough data from players.

          //- Reset modal
          MActionModalButton(
            modal-title="Reset Matchmaker"
            :action="resetMatchmaker"
            trigger-button-label="Reset"
            ok-button-label="Reset"
            variant="warning"
            permission="api.matchmakers.admin"
            data-testid="reset-matchmaker"
            )
            p Resetting this matchmaker will immediately re-initialize it.
            p(class="text-muted small") Resetting is safe to do in a production environment, but might momentarily degrade the matchmaking experience for live players as it takes a few minutes for the matchmaker to re-populate.

  template(#default)
    core-ui-placement(
      placementId="Matchmakers/Details"
      :matchmakerId="matchmakerId"
      )

    meta-raw-data(
      :kvPair="singleMatchmakerData"
      name="singleMatchmakerData"
      )
</template>

<script lang="ts" setup>
import { Chart as ChartJS, Title, Tooltip, BarElement, CategoryScale, LogarithmicScale } from 'chart.js'
import { ref, computed } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModalButton,
  MButton,
  MModal,
  MErrorCallout,
  MList,
  MListItem,
  MTextButton,
  useHeaderbar,
  MViewContainer,
  MPageOverviewCard,
  type MViewContainerAlert,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { fetchSubscriptionDataOnceOnly, useSubscription } from '@metaplay/subscriptions'

import MetaGeneratedForm from '../components/generatedui/components/MetaGeneratedForm.vue'
import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import {
  getSingleMatchmakerSubscriptionOptions,
  getTopPlayersOfSingleMatchmakerSubscriptionOptions,
} from '../subscription_options/matchmaking'
import { getSinglePlayerSubscriptionOptions } from '../subscription_options/players'

ChartJS.register(Title, Tooltip, BarElement, CategoryScale, LogarithmicScale)

const props = defineProps<{
  /**
   * ID of the matchmaker to display.
   */
  matchmakerId: string
}>()

/**
 * Subscribe to the data, error and refresh of a single matchmaker based on its id.
 */
const {
  data: singleMatchmakerData,
  error: singleMatchmakerError,
  refresh: singleMatchmakerRefresh,
} = useSubscription(getSingleMatchmakerSubscriptionOptions(props.matchmakerId))

// Update the headerbar title dynamically as data changes.
useHeaderbar().setDynamicTitle(
  singleMatchmakerData,
  (singleMatchmakerData) => `Manage ${singleMatchmakerData.value?.data.name ?? 'Matchmaker'}`
)

const simulateModal = ref<typeof MModal>()

// Top players.
const { refresh: topPlayersRefresh } = useSubscription(
  getTopPlayersOfSingleMatchmakerSubscriptionOptions(props.matchmakerId)
)

// Reset modal.
async function resetMatchmaker(): Promise<void> {
  await useGameServerApi().post(`matchmakers/${props.matchmakerId}/reset`)
  singleMatchmakerRefresh()
  topPlayersRefresh()
}

const { showSuccessNotification } = useNotifications()

// Rebalance modal.
async function rebalanceMatchmaker(): Promise<void> {
  await useGameServerApi().post(`matchmakers/${props.matchmakerId}/rebalance`)
  showSuccessNotification(`${props.matchmakerId} rebalanced successfully.`)
  singleMatchmakerRefresh()
}

// Simulation modal.
const simulationData = ref(null)
const isSimulationFormValid = ref(false)
const simulationResult = ref<any>(null)
const previewCandidateData = ref()
const isSimulationRunning = ref(false)

async function simulateMatchmaking(): Promise<void> {
  try {
    isSimulationRunning.value = true

    // Fetch simulation results.
    const response = await useGameServerApi().post(`matchmakers/${props.matchmakerId}/test`, simulationData.value)
    simulationResult.value = response.data

    if (simulationResult.value.response?.responseType === 'Success') {
      // Fetch data for the best matching player.
      const previewCandidateId = simulationResult.value.response.bestCandidate
      previewCandidateData.value = await fetchSubscriptionDataOnceOnly(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        getSinglePlayerSubscriptionOptions(previewCandidateId)
      )
    }

    isSimulationRunning.value = false
  } catch (e) {
    simulationResult.value = {
      response: {
        data: {
          error: e,
        },
      },
    }
  }
}

const scanningErrorRate = computed((): number => {
  if (singleMatchmakerData.value?.data.scannedPlayersCount && singleMatchmakerData.value.data.playerScanErrorCount) {
    return (
      (singleMatchmakerData.value.data.playerScanErrorCount / singleMatchmakerData.value.data.scannedPlayersCount) * 100
    )
  } else {
    return 0
  }
})

/**
 * Array of messages to be displayed at the top of the page.
 */
const alerts = computed(() => {
  const allAlerts: MViewContainerAlert[] = []
  // Fixed warning threshold of 5%.
  if (scanningErrorRate.value > 5) {
    allAlerts.push({
      title: 'Warning: Errors encountered',
      message: `The matchmaker encountered ${scanningErrorRate.value.toFixed(2)}% error rate while scanning for players (${singleMatchmakerData.value?.data.playerScanErrorCount} out of ${singleMatchmakerData.value?.data.scannedPlayersCount} players scanned). See the server logs for more information.`,
      variant: 'warning',
    })
  }
  return allAlerts
})
</script>

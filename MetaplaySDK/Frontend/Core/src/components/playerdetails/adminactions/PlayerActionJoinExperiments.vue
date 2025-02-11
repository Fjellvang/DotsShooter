<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Join an Experiment"
  :action="joinOrUpdateExperiment"
  trigger-button-label="Join Experiment"
  variant="warning"
  ok-button-label="Save"
  :ok-button-disabled-tooltip="okButtonDisabledTooltip"
  permission="api.players.edit_experiment_groups"
  @show="resetModal"
  data-testid="action-join-experiment"
  )
  template(#default)
    //- Button to display the modal
    meta-alert(
      v-if="!experimentsAvailable"
      title="No Experiments Available"
      )
      | There are no active experiments available in this environment. First, set them up in your game configs and then configure them from the #[MTextButton(to="/experiments") experiments page].

    div(v-else)
      div(class="tw-mb-3") You can manually enroll #[MBadge {{ playerData.model.playerName }}] in an active experiment, or change their variant in an experiment they are already in.
      div(class="tw-mb-3 tw-text-xs+ tw-text-neutral-500") Note: Players can never leave experiments once enrolled, but you can always change their variant. Moving a player to the control group has the same effect as removing a player from an experiment.

      div(class="tw-mb-3")
        div(class="tw-mb-1 tw-font-semibold") Experiment
        meta-input-select(
          :value="experimentFormInfo.experimentId ?? 'none'"
          :options="experimentOptions"
          placeholder="Select an experiment"
          no-clear
          class="tw-mb-4"
          @input="updateExperimentSelection"
          )

      div(class="tw-mb-3")
        div(class="tw-font-semibold") Variant
        meta-input-select(
          :value="experimentFormInfo.variantId ?? 'none'"
          :options="variantOptions"
          :disabled="!experimentFormInfo.experimentId"
          placeholder="Select a variant"
          no-clear
          @input="experimentFormInfo.variantId = $event"
          )

      div(class="tw-flex tw-justify-between")
        div(class="tw-font-semibold") Tester
        MInputSwitch(
          :model-value="experimentFormInfo.isTester"
          :disabled="!experimentFormInfo.experimentId"
          name="isPlayerTester"
          size="small"
          @update:model-value="experimentFormInfo.isTester = $event"
          )
      div(class="tw-mb-3 tw-text-xs+ tw-text-neutral-500") As a tester, this player can try out the experiment before it is enabled for everyone. This is a great way to test variants before rolling them out!

      meta-no-seatbelts(
        v-if="!okButtonDisabledTooltip"
        message="Enrolling a player to an experiment or modifying the variant will force the player to reconnect!"
        )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MBadge, MInputSwitch, MActionModalButton, MTextButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getAllExperimentsSubscriptionOptions } from '../../../subscription_options/experiments'
import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player to target the change action at.
   */
  playerId: string
}>()

/** Access to the pre-configured HTTP client. */
const gameServerApi = useGameServerApi()

/** Subscribe to target player's data. */
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/** Subscribe to experiment data. */
const { data: experimentsData } = useSubscription(getAllExperimentsSubscriptionOptions())

/**
 * Type definition for the information collected on this form.
 */
interface ExperimentFormInfo {
  experimentId: string | null
  variantId: string | null
  isTester: boolean
}

/**
 * Experiment details collected using this form.
 */
const experimentFormInfo = ref(getNewExperimentFormInfo())

/**
 * Data needed to initialize the form.
 */
function getNewExperimentFormInfo(): ExperimentFormInfo {
  return {
    experimentId: null,
    variantId: null,
    isTester: false,
  }
}

/**
 * Checks that the experiments data has active experiments for the target player to join.
 */
const experimentsAvailable = computed((): boolean => {
  return experimentsData.value.experiments.length
})

/**
 * All experiments that the target player is enrolled in.
 */
const playerExperiments = computed(() => {
  return playerData.value.experiments
})

/**
 * Experiment options that are to be selected from the dropdown.
 */
const experimentOptions = computed((): Array<MetaInputSelectOption<string>> => {
  // Find experiments that are in a phase where the player is able to join.
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const experiments = Object.values(experimentsData.value.experiments).filter(
    (experiment: any) =>
      ['Inactive', 'Testing', 'Ongoing', 'Paused'].includes(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        experiment.phase
      ) && experiment.whyInvalid === null
  )

  // Create a list for the dropdown.
  const options: Array<MetaInputSelectOption<string>> = experiments.map((experiment: any) => {
    return {
      value: experiment.experimentId,
      id: Object.keys(
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        playerData.value.model.experiments.experimentGroupAssignment
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      ).includes(experiment.experimentId)
        ? `${experiment.displayName} / ${experiment.experimentId} (already enrolled)`
        : experiment.displayName,
    }
  })

  return options
})

/**
 * Check if target player is enrolled in an experiment.
 */
const alreadyInSelectedExperiment = computed(() => {
  if (experimentFormInfo.value.experimentId === null) {
    return false
  }
  return Object.keys(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    playerData.value.model.experiments.experimentGroupAssignment
  ).includes(experimentFormInfo.value.experimentId)
})

/**
 * Check if target player is already enrolled in an experiment variant.
 */
const alreadyInSelectedVariant = computed(() => {
  if (experimentFormInfo.value.variantId === null || experimentFormInfo.value.experimentId === null) {
    return false
  }
  const newVariantId =
    experimentFormInfo.value.variantId === 'Control group' ? null : experimentFormInfo.value.variantId
  return (
    playerData.value.model.experiments.experimentGroupAssignment[experimentFormInfo.value.experimentId]?.variantId ===
    newVariantId
  )
})

/**
 * Enables the 'Ok' button and the 'noSeatBelts' warning when a valid experiment variant is selected.
 */
const okButtonDisabledTooltip = computed((): string | undefined => {
  if (!experimentFormInfo.value.experimentId || !experimentFormInfo.value.variantId) {
    return 'Please select an experiment and variant.'
  } else if (alreadyInSelectedExperiment.value && alreadyInSelectedVariant.value) {
    return playerExperiments.value[experimentFormInfo.value.experimentId]?.isPlayerTester !==
      experimentFormInfo.value.isTester
      ? undefined
      : 'No changes detected.'
  } else return undefined
})

/**
 All available variants to be selected on the form dropdown.
 */
const variantOptions = ref<Array<MetaInputSelectOption<string>>>([])

/**
 * Update the selected experiment and/or variant option(s).
 */
async function updateExperimentSelection(newSelection: string): Promise<void> {
  experimentFormInfo.value.experimentId = newSelection

  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const isTesterExperiments = Object.entries(playerExperiments.value).filter(
    ([key, value]: any) => value.isPlayerTester
  )
  experimentFormInfo.value.isTester =
    isTesterExperiments.find(([key, value]) => key === experimentFormInfo.value.experimentId) !== undefined

  // Prefill variantId if player is already in selected experiment.
  if (alreadyInSelectedExperiment.value) {
    experimentFormInfo.value.variantId =
      playerData.value.model.experiments.experimentGroupAssignment[experimentFormInfo.value.experimentId]?.variantId ||
      'Control group'
  } else {
    experimentFormInfo.value.variantId = null
  }

  // Fetch the experiment details so that we can get the list of variants.
  variantOptions.value = []
  if (!experimentFormInfo.value.experimentId || experimentFormInfo.value.experimentId === 'none') {
    return
  }

  const response = await gameServerApi.get(`/experiments/${experimentFormInfo.value.experimentId}`)
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const options = Object.keys(response.data.state.variants).map((item) => ({
    value: item,
    id:
      playerData.value.model.experiments.experimentGroupAssignment[
        // @ts-expect-error -- TS is failing to type narrow here.
        experimentFormInfo.value.experimentId
      ]?.variantId === item
        ? `${item} (already enrolled)`
        : item,
  }))

  // Create a list for the dropdown.
  variantOptions.value = [{ value: 'Control group', id: 'Control group' }, ...options]
}

const { showSuccessNotification } = useNotifications()

/**
 * Join or update a selected experiment.
 */
async function joinOrUpdateExperiment(): Promise<void> {
  const message = alreadyInSelectedExperiment.value
    ? `${playerData.value.model.playerName} changed experiment variant.`
    : `${playerData.value.id} enrolled into the experiment.`
  const newVariantId =
    experimentFormInfo.value.variantId === 'Control group' ? null : experimentFormInfo.value.variantId
  await gameServerApi.post(`/players/${playerData.value.id}/changeExperiment`, {
    ExperimentId: experimentFormInfo.value.experimentId,
    VariantId: newVariantId,
    IsTester: experimentFormInfo.value.isTester,
  })
  showSuccessNotification(message)
  playerRefresh()
}

/**
 * Reset the modal.
 */
function resetModal(): void {
  experimentFormInfo.value = getNewExperimentFormInfo()
  variantOptions.value = [{ value: 'none', id: 'Select an experiment' }]
}
</script>

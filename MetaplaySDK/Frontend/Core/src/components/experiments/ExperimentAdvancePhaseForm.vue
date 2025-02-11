<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MButton(
  v-for="button in buttons"
  :disabledTooltip="button.disabledTooltip"
  permission="api.experiments.edit"
  @click="onActionClick(button.phaseInfoButton)"
  ) {{ button.phaseInfoButton.action }}

MActionModal(
  ref="modal"
  :title="selectedAction.modalTitle"
  :action="advanceToNextPhase"
  :ok-button-label="selectedAction.modalOkButtonText"
  )
  div(class="tw-mb-3") {{ selectedAction.modalText }}
  meta-alert(
    v-if="nextPhasePerformanceTip(selectedAction.nextPhase) === 'increase' && singleExperimentData.combinations.exceedsThreshold"
    noShadow
    variant="warning"
    title="Performance Tip"
    :message="`Rolling this experiment out will increase the total number of live game config combinations to ${singleExperimentData.combinations.newCombinations} from the current ${singleExperimentData.combinations.currentCombinations}. This is a very high number and may cause a large use of memory on your game servers depending on how the experiments have been targeted. Consider concluding other experiments to keep the number of live game config combinations low.`"
    )
  meta-alert(
    v-else-if="nextPhasePerformanceTip(selectedAction.nextPhase) === 'increase' && selectedAction.nextPhase !== 'Testing'"
    noShadow
    variant="secondary"
    title="Performance Tip"
    :message="`Rolling out or continuing this experiment will increase the total number of live game config combinations to ${singleExperimentData.combinations.newCombinations} from the current ${singleExperimentData.combinations.currentCombinations}. Remember to keep an eye on your game server memory use when running many experiments in parallel.`"
    )
  meta-alert(
    v-else-if="nextPhasePerformanceTip(selectedAction.nextPhase) === 'decrease'"
    noShadow
    variant="secondary"
    title="Performance Tip"
    :message="`Concluding or pausing this experiment will decrease the total number of live game config combinations to ${singleExperimentData.combinations.newCombinations} from the current ${singleExperimentData.combinations.currentCombinations}. This will reduce the memory use on your game servers.`"
    )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModal, MButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'

const props = defineProps<{
  experimentId: string
}>()

// MODAL STUFF -----------------------------------------
const modal = ref<typeof MActionModal>()

function onActionClick(button: PhaseInfoButton): void {
  selectedAction.value = button
  modal.value?.open(button)
}

const gameServerApi = useGameServerApi()

// EXPERIMENTS -----------------------------------------
const {
  data: singleExperimentData,
  refresh: singleExperimentRefresh,
  error: singleExperimentError,
} = useSubscription(getSingleExperimentSubscriptionOptions(props.experimentId || ''))

/**
 * The selected action that the user has clicked on.
 */
const selectedAction = ref<PhaseInfoButton>({
  action: '',
  modalTitle: '',
  modalText: '',
  modalOkButtonText: '',
  nextPhase: '',
  nextPhaseToast: '',
  forceNexPhase: false,
})

/**
 * The current phase of the experiment.
 */
const phase = computed(() => singleExperimentData.value.state.lifecyclePhase)

// PHASE STUFF -----------------------------------------

interface PhaseInfoButton {
  action: string
  modalTitle: string
  modalText: string
  modalOkButtonText: string
  nextPhase: string
  nextPhaseToast: string
  forceNexPhase: boolean
}

/**
 * The actions and modal information that are available for the each experiment phase.
 */
const advancePhaseButton: Record<string, PhaseInfoButton> = {
  rollout: {
    action: 'Rollout',
    modalTitle: 'Rollout Experiment',
    modalText:
      'Rolling out an experiment will enable it for the targeted players. Please make sure you are happy with how the experiment is configured, because changing an experiment while it is running may make it harder to analyse the results.',
    modalOkButtonText: 'Rollout',
    nextPhase: 'Ongoing',
    nextPhaseToast: 'Experiment [EXPERIMENT_NAME] rolled out to players!',
    forceNexPhase: false,
  },
  pause: {
    action: 'Pause',
    modalTitle: 'Pause Experiment',
    modalText:
      'Pausing an experiment will stop further rollout to players and only players who were enrolled as testers for the experiment will see the variants. Use this if you are unsure of your variants and you need to re-test them before you continue.',
    modalOkButtonText: 'Pause',
    nextPhase: 'Paused',
    nextPhaseToast: 'Experiment [EXPERIMENT_NAME] paused.',
    forceNexPhase: false,
  },
  continue: {
    action: 'Continue',
    modalTitle: 'Continue Experiment',
    modalText:
      'Continuing the experiment will put it back into the active state. Players who are already in the experiment will start seeing their variants again, and new players will get enrolled into the experiment.',
    modalOkButtonText: 'Continue',
    nextPhase: 'Ongoing',
    nextPhaseToast: 'Experiment [EXPERIMENT_NAME] unpaused.',
    forceNexPhase: false,
  },
  conclude: {
    action: 'Conclude',
    modalTitle: 'Conclude Experiment',
    modalText:
      'Concluding an experiment will prevent further people from joining it. After concluding, all participating players will see the control variant of the experiment.',
    modalOkButtonText: 'Conclude',
    nextPhase: 'Concluded',
    nextPhaseToast: 'Experiment [EXPERIMENT_NAME] concluded.',
    forceNexPhase: false,
  },
  restart: {
    action: 'Restart',
    modalTitle: 'Restart Experiment',
    modalText:
      'Restarting an experiment will place it back into the testing phase. Players who were enrolled as testers for the experiment will start to see their assigned variants again. Players who joined the experiment naturally will see the control variant until the experiment is rolled out again.',
    modalOkButtonText: 'Restart',
    nextPhase: 'Testing',
    nextPhaseToast: 'Experiment [EXPERIMENT_NAME] restarted.',
    forceNexPhase: true,
  },
}

/**
 * Describes a button that can be clicked to perform an action on the experiment. These buttons open a modal.
 */
interface ActionButton {
  phaseInfoButton: PhaseInfoButton
  disabledTooltip?: string
}

/**
 * The buttons to be displayed for the current phase of the experiment.
 * Note that these buttons are listed in the opposite order to how they are be displayed.
 */
const buttons = computed((): ActionButton[] => {
  switch (phase.value) {
    case 'Testing':
      return [
        {
          phaseInfoButton: advancePhaseButton.pause,
          disabledTooltip: 'Cannot pause an experiment while it is in testing.',
        },
        { phaseInfoButton: advancePhaseButton.rollout },
      ]
    case 'Ongoing':
      return [{ phaseInfoButton: advancePhaseButton.pause }, { phaseInfoButton: advancePhaseButton.conclude }]
    case 'Paused':
      return [{ phaseInfoButton: advancePhaseButton.continue }, { phaseInfoButton: advancePhaseButton.conclude }]
    case 'Concluded':
      return [
        { phaseInfoButton: advancePhaseButton.pause, disabledTooltip: 'Cannot pause a concluded experiment.' },
        { phaseInfoButton: advancePhaseButton.restart },
      ]
    default:
      return []
  }
})

/**
 * A hint to the user about the performance impact of the next phase.
 */
function nextPhasePerformanceTip(action: string | undefined): 'increase' | 'decrease' | undefined {
  if (action === 'Testing') {
    return 'increase'
  } else if (action === 'Ongoing') {
    return 'increase'
  } else if (action === 'Paused') {
    return 'decrease'
  } else if (action === 'Concluded') {
    return 'decrease'
  }

  return undefined
}

const { showSuccessNotification } = useNotifications()

/**
 * Advance the experiment to the next phase.
 */
async function advanceToNextPhase(): Promise<void> {
  if (selectedAction.value) {
    const nextPhase = selectedAction.value.nextPhase
    if (nextPhase && phase.value !== nextPhase) {
      // Change phase
      await gameServerApi.post(`/experiments/${props.experimentId}/phase`, {
        Phase: nextPhase,
        Force: selectedAction.value.forceNexPhase,
      })

      // Toast the success
      const message = selectedAction.value.nextPhaseToast.replace(
        '[EXPERIMENT_NAME]',
        `'${singleExperimentData.value.stats.displayName}'`
      )
      showSuccessNotification(message)
    }
    singleExperimentRefresh()
  }
}
</script>

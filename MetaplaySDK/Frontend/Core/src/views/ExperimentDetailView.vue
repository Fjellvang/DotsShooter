<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.experiments.view"
  :is-loading="!singleExperimentData || !playerExperiments"
  :error="singleExperimentError"
  :alerts="headerAlerts"
  full-width
  )
  template(#overview)
    MPageOverviewCard(
      :id="experimentId"
      :title="singleExperimentData.stats.displayName"
      :subtitle="singleExperimentData.stats.description"
      data-testid="experiment-detail-overview-card"
      )
      //- Experiment phase overview.
      div(class="tw-mb-5")
        experiment-phase-overview

      //- Experiment overview list.
      experiment-overview-list

      //- Performance tip
      //- TODO: show the right label conditionally. Could also do something better if there are good ideas?
      div(class="tw-mt-4 tw-text-sm tw-font-semibold") Performance Tip
      p(
        v-if="['Ongoing'].includes(phase)"
        class="tw-my-1 tw-text-xs+ tw-text-neutral-500"
        )
        | This experiment is currently adding {{ singleExperimentData.combinations.currentCombinations - singleExperimentData.combinations.newCombinations }} live game config combinations to the total of {{ singleExperimentData.combinations.currentCombinations }} possible combinations.
      p(
        v-if="['Testing', 'Paused', 'Concluded'].includes(phase)"
        class="tw-my-1 tw-text-xs+ tw-text-neutral-500"
        )
        | This experiment is currently not running and thus is not affecting game server memory use.

      //- Action buttons
      template(#buttons)
        //- Edit modal
        experiment-form(
          ref="editExperimentModal"
          :experiment-id="experimentId"
          )
        //- Advance phase modal
        experiment-advance-phase-form(
          ref="advancePhaseModal"
          :experiment-id="experimentId"
          )

  //- Tabs
  template(#default)
    MTabLayout(:tabs="tabOptions")
      //- Details
      template(#tab-0)
        //- Game config contents
        config-contents-card(
          v-if="!isExperimentMissing"
          :experiment-id="experimentId"
          hide-no-diffs
          exclude-server-libraries
          )

        //- Missing experiment
        MCard(
          v-else
          title=""
          )
          div(class="justify-content-center py-5") This experiment is missing from the game config and cannot be displayed.

        //- Single experiment raw data
        meta-raw-data(
          :kvPair="singleExperimentData"
          name="experimentInfo"
          )

      //- Audience & Targeting
      template(#tab-1)
        MTwoColumnLayout
          //- Target audience
          experiment-target-audience(data-testid="experiment-detail-target-audience-card")

          //- Variants
          experiment-variants-card(
            :experiment-id="experimentId"
            data-testid="experiment-detail-variants-card"
            )

          //- Segment targeting
          targeting-card(
            :target-condition="singleExperimentData.state.targetCondition"
            owner-title="This experiment"
            data-testid="experiment-detail-segments-card"
            )

          //- Test players
          player-list-card(
            :player-ids="singleExperimentData.state.testerPlayerIds"
            title="Test Players"
            empty-message="No players have been assigned to test this experiment."
            data-testid="experiment-detail-test-players-card"
            )
        //- Experiment raw data
        meta-raw-data(
          :kvPair="experiment"
          name="experiment"
          )

      //- Audit logs
      template(#tab-2)
        MTwoColumnLayout
          //- Audit log
          audit-log-card(
            target-type="$Experiment"
            :target-id="experimentId"
            )

        //- Experiment raw data
        meta-raw-data(
          :kvPair="experiment"
          name="experiment"
          )

        //- Single experiment raw data
        meta-raw-data(
          :kvPair="singleExperimentData"
          name="experimentInfo"
          )
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'

import {
  MViewContainer,
  MTwoColumnLayout,
  type Variant,
  type MViewContainerAlert,
  MPageOverviewCard,
  MTabLayout,
  type TabOption,
  MCard,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import AuditLogCard from '../components/auditlogs/AuditLogCard.vue'
import ExperimentAdvancePhaseForm from '../components/experiments/ExperimentAdvancePhaseForm.vue'
import ExperimentForm from '../components/experiments/ExperimentForm.vue'
import ExperimentOverviewList from '../components/experiments/ExperimentOverviewList.vue'
import ExperimentPhaseOverview from '../components/experiments/ExperimentPhaseOverview.vue'
import ExperimentTargetAudience from '../components/experiments/ExperimentTargetAudience.vue'
import ExperimentVariantsCard from '../components/experiments/ExperimentVariantsCard.vue'
import ConfigContentsCard from '../components/global/ConfigContentsCard.vue'
import PlayerListCard from '../components/global/PlayerListCard.vue'
import TargetingCard from '../components/mails/TargetingCard.vue'
import { routeParamToSingleValue, isNullOrUndefined } from '../coreUtils'
import { getSingleExperimentSubscriptionOptions } from '../subscription_options/experiments'
import { getGameDataSubscriptionOptions } from '../subscription_options/general'

const route = useRoute()

const { data: gameData } = useSubscription(getGameDataSubscriptionOptions())

// MODAL STUFF -----------------------------------------

const advancePhaseModal = ref<typeof ExperimentAdvancePhaseForm>()
const editExperimentModal = ref<typeof ExperimentForm>()

// PHASE STUFF -----------------------------------------

interface PhaseInfo {
  title: string
  titleVariant: Variant
}

type Phase = 'Testing' | 'Ongoing' | 'Paused' | 'Concluded'

/**
 * The current phase of the experiment.
 */
const phase = computed((): Phase => singleExperimentData.value?.state.lifecyclePhase)

// EXPERIMENTS -----------------------------------------

const experimentId = routeParamToSingleValue(route.params.id)
const { data: singleExperimentData, error: singleExperimentError } = useSubscription(
  getSingleExperimentSubscriptionOptions(experimentId || '')
)

const playerExperiments = computed(() => gameData.value?.serverGameConfig.PlayerExperiments)
const experiment = computed(() => gameData.value?.serverGameConfig.PlayerExperiments[experimentId])

const isExperimentMissing = computed(() => !experiment.value)

// TABS -----------------------------------------

const tabOptions: TabOption[] = [
  {
    label: 'Details',
  },
  {
    label: 'Audience & Targeting',
  },
  {
    label: 'Audit Log',
  },
]

// MISC UI -----------------------------------------

const headerAlerts = computed(() => {
  const alerts: MViewContainerAlert[] = []

  // Experiment missing
  if (isExperimentMissing.value) {
    alerts.push({
      title: 'Experiment removed',
      variant: 'danger',
      message: `The experiment '${singleExperimentData.value?.stats.displayName}' is missing from the game config and has been disabled. Restore the experiment to your game config to re-enable it.`,
      dataTest: 'missing-experiment-alert',
    })
  }

  // Missing variants
  const missingVariantIds: string[] = []
  if (isNullOrUndefined(singleExperimentData.value)) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Object.entries(singleExperimentData.value.state.variants).forEach(([id, variant]) => {
      if ((variant as any).isConfigMissing === true) {
        missingVariantIds.push(id)
      }
    })
    if (missingVariantIds.length === 1) {
      alerts.push({
        title: 'Variant removed',
        variant: 'warning',
        message: `The variant '${missingVariantIds[0]}' has been removed from the game config and has been disabled. Restore the variant to your game config to re-enable it.`,
      })
    } else if (missingVariantIds.length > 1) {
      let variantNameList = ''
      while (missingVariantIds.length > 0) {
        variantNameList += `'${missingVariantIds.shift()}'`
        if (missingVariantIds.length > 1) variantNameList += ', '
        else if (missingVariantIds.length === 1) variantNameList += ' and '
      }
      alerts.push({
        title: 'Variants removed',
        variant: 'warning',
        message: `The variants ${variantNameList} have been removed from the game config and has been disabled. Restore the variant to your game config to re-enable it.`,
      })
    }

    // Empty variant weights
    const weightlessVariantIds = []
    if (singleExperimentData.value.state.controlWeight === 0) {
      weightlessVariantIds.push('Control group')
    }
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    Object.entries(singleExperimentData.value.state.variants).forEach(([id, variant]) => {
      if ((variant as any).weight === 0) {
        weightlessVariantIds.push(id)
      }
    })
    if (weightlessVariantIds.length === 1) {
      alerts.push({
        title: 'Variant inaccessible',
        variant: 'warning',
        message: `The variant '${weightlessVariantIds[0]}' has a weight of 0%. This means that the variant will never be shown to any players. Is this what you intended?`,
      })
    } else if (weightlessVariantIds.length > 1) {
      let variantNameList = ''
      while (weightlessVariantIds.length > 0) {
        variantNameList += `'${weightlessVariantIds.shift()}'`
        if (weightlessVariantIds.length > 1) variantNameList += ', '
        else if (weightlessVariantIds.length === 1) variantNameList += ' and '
      }
      alerts.push({
        title: 'Variants inaccessible',
        variant: 'warning',
        message: `The variants ${variantNameList} have been removed from the game config and has been disabled. Restore the variant to your game config to re-enable it.`,
      })
    }
  }

  return alerts
})
</script>

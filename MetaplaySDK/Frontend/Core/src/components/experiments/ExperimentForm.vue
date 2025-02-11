<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MButton(
  permission="api.experiments.edit"
  :disabled-tooltip="phase === 'Concluded' ? 'Cannot reconfigure a concluded experiment.' : undefined"
  @click="modal?.open()"
  ) {{ phase === 'Testing' || phase === 'Concluded' ? 'Configure' : 'Reconfigure' }}

MActionModal(
  ref="modal"
  :title="modalTitle"
  :action="updateConfiguration"
  ok-button-label="Update Configuration"
  :ok-button-disabled-tooltip="!isFormValid ? 'Variant weights must add up to 100% to proceed.' : undefined"
  @show="resetForm"
  )
  div(class="mb-4")
    meta-alert(
      v-if="modalText"
      title
      variant="info"
      ) {{ modalText }}

    h6 Rollout Settings
    div(class="p-3 bg-light rounded border")
      b-row(
        align-h="between"
        no-gutters
        )
        span(class="font-weight-bold") Rollout Enabled
        MInputSwitch(
          :model-value="rolloutEnabled"
          class="tw-relative tw-top-1 tw-mr-1"
          name="rolloutEnabled"
          size="small"
          @update:model-value="rolloutEnabled = $event"
          )
      hr(v-show="rolloutEnabled")
      b-row(v-show="rolloutEnabled")
        b-col(md class="mb-2")
          MInputNumber(
            label="Rollout %"
            :model-value="rolloutPercentage"
            :min="0"
            :max="100"
            @update:model-value="rolloutPercentage = $event"
            )
        b-col(md class="mb-2")
          MInputNumber(
            label="Capacity Limit"
            :model-value="maxCapacity"
            :min="0"
            placeholder="Unlimited"
            clearOnZero
            @update:model-value="maxCapacity = $event"
            )
      div(class="small text-muted tw-mt-1")
        span(v-if="rolloutEnabled") With rollout enabled, a percentage of your player base will be able to join the experiment, up to the optional capacity limit.
        span(v-else) Players will not be able to join an experiment with rollout disabled. You can use this to manually close an experiment to new players.

  div(class="mb-4")
    h6 Audience
    message-audience-form(
      :model-value="audience"
      @update:model-value="(event: any) => audience = event"
      :isPlayerTargetingSupported="false"
      class="mb-2"
      )
    b-row(
      align-h="between"
      no-gutters
      )
      div(class="font-weight-bold") Account Age
      MInputSingleSelectRadio(
        :model-value="enrollTrigger"
        :options="enrollTriggerOptions"
        size="small"
        @update:model-value="enrollTrigger = $event"
        )
    div(v-if="enrollTrigger !== 'Login'" class="small text-muted mb-3") Players can join the experiment at the time of account creation only.
    div(v-else class="small text-muted mb-3") Players can join the experiment the next time they login.

  div(class="tw-mb-4")
    h6 Variant Rollout Percentages
    div(class="@tw-container tw-bg-neutral-50 tw-rounded-md tw-border tw-border-neutral-200 tw-p-4")
      div(class="tw-grid @sm:tw-grid-cols-3 tw-gap-3")
        MInputNumber(
          v-for="(value, key) in variantWeights"
          :key="key"
          :label="key + ' %'"
          :model-value="value.weight"
          :min="0"
          :variant="totalWeights !== 100 ? 'danger' : 'default'"
          @update:model-value="(event) => onVariantWeightChange(event, value, key)"
          )
      div(v-if="totalWeights !== 100" class="small text-danger") Variant weights do not add up to 100%. #[MTextButton(@click="balanceVariantWeights()") Balance automatically]?
      div(v-if="variantWeights && parseInt(variantWeights.Control.weight) === 0" class="small text-warning") Empty control group! Validating this experiment's results may not be possible.
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MActionModal,
  MButton,
  MInputSwitch,
  MInputSingleSelectRadio,
  MInputNumber,
  MTextButton,
  useNotifications,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'
import MessageAudienceForm from '../mails/MessageAudienceForm.vue'

const props = defineProps<{
  experimentId: string
}>()

// Modal ---------------------------------------------------------------
/**
 * Reference to the modal component
 */
const modal = ref<typeof MActionModal>()

/**
 * The title of the modal.
 */
const modalTitle = computed(() => {
  if (phase.value === 'Testing' || phase.value === 'Concluded') {
    return 'Configure Experiment'
  } else {
    return 'Reconfigure Experiment'
  }
})

/**
 * The text to be shown in the modal.
 */
const modalText = computed(() => {
  if (phase.value === 'Testing' || phase.value === 'Concluded') {
    return undefined
  } else {
    return 'Reconfiguring an experiment after it has been rolled out may not achieve the expected result and is therefore generally discouraged. For example, decreasing rollout % or capacity limit will have no effect if those limits have already been reached. Changing targeting or weights may make it harder to analyse the experiment results.'
  }
})

function open(): void {
  modal.value?.open()
}

defineExpose({
  open,
})

/**
 * The base data for the current experiment.
 */
const {
  data: singleExperimentData,
  refresh: singleExperimentRefresh,
  error: singleExperimentError,
} = useSubscription(getSingleExperimentSubscriptionOptions(props.experimentId || ''))

/**
 * The current lifecycle phase of the experiment.
 */
const phase = computed(() => singleExperimentData.value.state.lifecyclePhase)

/**
 * The data detailing the current state of the experiment.
 */
const { data: experimentInfoData, refresh: experimentInfoRefresh } = useSubscription(
  getSingleExperimentSubscriptionOptions(props.experimentId)
)

/**
 * Indicates whether the current experiment is currently active (rolled).
 */
const rolloutEnabled = ref(false)

/**
 * The percentage of players that will be enrolled n the experiment.
 */
const rolloutPercentage = ref<any>(null)

/**
 * The trigger that will enroll players in the experiment.
 * By default players are enrolled when they login.
 */
const enrollTrigger = ref<'Login' | 'NewPlayers'>('Login')

/**
 * The options for when players are enrolled in the experiment.
 */
const enrollTriggerOptions: Array<{
  label: string
  value: 'Login' | 'NewPlayers'
}> = [
  { label: 'Everyone', value: 'Login' },
  { label: 'New players', value: 'NewPlayers' },
]

/**
 * Indicates whether the experiment has a capacity to enroll more players.
 */
const hasCapacityLimit = computed((): boolean => maxCapacity.value !== undefined && maxCapacity.value > 0)

/**
 * The maximum number of players that can be enrolled in the experiment.
 */
const maxCapacity = ref<number | undefined>()

/**
 * The weights of each of the variants in the experiment.
 */
const variantWeights = ref<any>(undefined)

/**
 * The total weight of all the variants.
 */
const totalWeights = ref(0)

/**
 * The audience that will be targeted by the experiment.
 */
const audience = ref<any>(MessageAudienceForm.props.modelValue.default())

function onVariantWeightChange(event: any, value: any, key: any): void {
  value.weight = event
  updateVariantWeights(key)
}

/**
 * Indicates whether the form is valid and can be submitted.
 */
const isFormValid = computed(() => {
  if (rolloutPercentage.value === undefined) return false

  if (
    !variantWeights.value ||
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    !Object.keys(variantWeights.value).every((variantId) => validateVariantWeight(variantId))
  ) {
    return false
  }
  return true
})

/**
 * Update the weights assigned to each variant in the experiment.
 * @param changedVariantId The id of the variant that was changed.
 */
function updateVariantWeights(changedVariantId: any): void {
  if (
    variantWeights.value[changedVariantId].weight === '' ||
    !variantWeights.value[changedVariantId].weight ||
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    parseInt(variantWeights.value[changedVariantId].weight) < 0
  ) {
    variantWeights.value[changedVariantId].weight = 0
  }

  // Round all
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  for (const variant of Object.values(variantWeights.value) as any) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    variant.weight = Math.round(variant.weight)
  }

  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  totalWeights.value = Object.values(variantWeights.value).reduce(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    (sum: number, variant: any) => sum + parseInt(variant.weight),
    0
  )
}

/**
 * Balance the weights of the variants so that they add up to 100%.
 */
function balanceVariantWeights(): void {
  // Count
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  let totalWeightsTemp = Object.values(variantWeights.value).reduce(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    (sum: number, variant: any) => sum + parseInt(variant.weight),
    0
  )

  // Gracefully handle edge case where all variants have 0 weight
  if (totalWeightsTemp === 0) {
    for (const key in variantWeights.value) {
      variantWeights.value[key].weight = 1
      totalWeightsTemp++
    }
  }

  // Redistribute if needed
  if (totalWeightsTemp !== 100) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    for (const variant of Object.values(variantWeights.value) as any) {
      variant.weight = Math.round((variant.weight / totalWeightsTemp) * 100)
    }

    // Finally adjust control group to fix rounding errors
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    totalWeightsTemp = Object.values(variantWeights.value).reduce(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      (sum: number, variant: any) => sum + parseInt(variant.weight),
      0
    )
    if (totalWeightsTemp !== 100) {
      variantWeights.value.Control.weight += 100 - totalWeightsTemp
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  totalWeights.value = Object.values(variantWeights.value).reduce(
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    (sum: number, variant: any) => sum + parseInt(variant.weight),
    0
  )
}

/**
 * Validates that the total weight of the variants is 100%.
 * @param variantId The id of the variant to validate.
 */
function validateVariantWeight(variantId: any): boolean {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  const value = parseInt(variantWeights.value[variantId].weight)

  if (isNaN(value)) return false
  if (value < 0 || totalWeights.value !== 100) return false
  return true
}

/**
 * Resets the form to the current experiment configuration.
 */
function resetForm(): void {
  rolloutEnabled.value = !experimentInfoData.value.state.isRolloutDisabled
  rolloutPercentage.value = experimentInfoData.value.state.rolloutRatioPermille / 10
  enrollTrigger.value = experimentInfoData.value.state.enrollTrigger
  maxCapacity.value = experimentInfoData.value.state.hasCapacityLimit
    ? experimentInfoData.value.state.maxCapacity
    : undefined

  variantWeights.value = {
    Control: { weight: experimentInfoData.value.state.controlWeight },
  }
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  Object.entries(experimentInfoData.value.state.variants).forEach(([id, data]) => {
    variantWeights.value[id] = {
      weight: (data as any).weight,
    }
  })
  balanceVariantWeights()
  audience.value = MessageAudienceForm.props.modelValue.default()
  audience.value.targetCondition = experimentInfoData.value.state.targetCondition
}

const gameServerApi = useGameServerApi()
const emits = defineEmits(['ok'])

const { showSuccessNotification } = useNotifications()

/**
 * Updates the experiment configuration on the server.
 */
async function updateConfiguration(): Promise<void> {
  const config = {
    isRolloutDisabled: !rolloutEnabled.value,
    enrollTrigger: enrollTrigger.value,
    hasCapacityLimit: hasCapacityLimit.value,
    maxCapacity: hasCapacityLimit.value ? maxCapacity.value : null,
    rolloutRatioPermille: rolloutPercentage.value * 10,
    variantWeights: Object.fromEntries(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      Object.keys(variantWeights.value).map((key) => [key === 'Control' ? null : key, variantWeights.value[key].weight])
    ),
    variantIsDisabled: null,
    targetCondition: audience.value.targetCondition,
  }
  await gameServerApi.post(`/experiments/${props.experimentId}/config`, config)
  showSuccessNotification('Configuration set.')
  experimentInfoRefresh()
  emits('ok')
}
</script>

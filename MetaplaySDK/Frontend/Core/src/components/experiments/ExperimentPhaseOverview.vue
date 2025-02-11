<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Default view.
div(
  style="font-size: 130%"
  class="tw-hidden tw-justify-around @md:tw-flex"
  )
  MBadge(
    :variant="phase === 'Testing' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos.Testing.title }}
  fa-icon(
    icon="arrow-right"
    class="tw-mt-2"
    )
  MBadge(
    :variant="phase === 'Ongoing' || phase === 'Paused' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos[phase === 'Paused' ? 'Paused' : 'Ongoing'].title }}
  fa-icon(
    icon="arrow-right"
    class="tw-mt-2"
    )
  MBadge(
    :variant="phase === 'Concluded' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos.Concluded.title }}

//- Mobile veiw.
div(
  style="font-size: 130%"
  class="tw-block tw-text-center @md:tw-hidden"
  )
  MBadge(
    :variant="phase === 'Testing' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos.Testing.title }}
  div: fa-icon(icon="arrow-down")
  MBadge(
    :variant="phase === 'Ongoing' || phase === 'Paused' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos[phase === 'Paused' ? 'Paused' : 'Ongoing'].title }}
  div: fa-icon(icon="arrow-down")
  MBadge(
    :variant="phase === 'Concluded' ? 'primary' : 'neutral'"
    class="tw-mx-2"
    ) {{ phaseInfos.Concluded.title }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'

import { type Variant, MBadge } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { routeParamToSingleValue } from '../../coreUtils'
import { getSingleExperimentSubscriptionOptions } from '../../subscription_options/experiments'

const route = useRoute()
const experimentId = routeParamToSingleValue(route.params.id)

const { data: singleExperimentData, error: singleExperimentError } = useSubscription(
  getSingleExperimentSubscriptionOptions(experimentId || '')
)

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

/**
 * The title and variant of the current experiment phase.
 */
const phaseInfo = computed((): PhaseInfo => phaseInfos[phase.value])

/**
 * List of titles and variants for all experiment phases.
 */
const phaseInfos: Record<Phase, PhaseInfo> = {
  Testing: {
    title: 'Testing',
    titleVariant: 'primary',
  },
  Ongoing: {
    title: 'Active',
    titleVariant: 'success',
  },
  Paused: {
    title: 'Paused',
    titleVariant: 'warning',
  },
  Concluded: {
    title: 'Concluded',
    titleVariant: 'neutral',
  },
}
</script>

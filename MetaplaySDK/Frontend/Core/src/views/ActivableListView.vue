<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Note: error handling not necessary due to staticConfigData already loaded during initialization.
MViewContainer(
  permission="api.activables.view"
  :is-loading="!activablesMetadata"
  )
  template(#overview)
    MPageOverviewCard(
      :title="`View ${categoryDisplayName}`"
      :subtitle="categoryDescription"
      :data-testid="`${sentenceCaseToKebabCase(props.categoryKey)}-overview-card`"
      )

  MSingleColumnLayout
    meta-generic-activables-card(
      :category="categoryKey"
      :longList="true"
      :title="categoryDisplayName"
      :emptyMessage="`No ${categoryDisplayName} defined. Set them up in your game configs to start using the feature!`"
      :customEvaluationIsoDateTime="customEvaluationTime ? String(customEvaluationTime.toISO()) : undefined"
      hideCollapse
      )

  b-row(
    align-h="center"
    class="tw-mt-4"
    )
    b-col(
      md="10"
      xl="9"
      class="tw-mb-4"
      )
      div(
        class="pl-3 bg-white rounded border shadow-sm tw-pb-4 tw-pr-4"
        data-testid="custom-time"
        )
        b-row(
          align-h="between"
          no-gutters
          class="mb-2 tw-mt-4"
          )
          span(class="font-weight-bold") Enable Custom Evaluation Time
            MBadge(
              tooltip="The phases on the page are evaluated according to the local time of your browser. Enabling custom evaluation allows you to set an exact time to evaluate against."
              shape="pill"
              class="tw-ml-1"
              ) ?
          MInputSwitch(
            :model-value="userEvaluationEnabled"
            class="tw-relative tw-top-1 tw-mr-1"
            name="customEvaluationTimeEnabled"
            size="small"
            @update:model-value="userEvaluationEnabled = $event"
            )
        div(
          v-if="userEvaluationEnabled"
          class="border-top pt-2 tw-mt-4"
          )
          MInputDateTime(
            label="Evaluation Time (UTC)"
            :model-value="userEvaluationTime"
            @update:model-value="onDateTimeChange"
            )
        div(class="mt-2 tw-w-full tw-text-center")
          span(class="small text-muted tw-italic") Schedules evaluated at {{ evaluationTimeUsed }}

  meta-raw-data(
    :kvPair="activablesMetadata"
    name="activablesMetadata"
    )
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import {
  MBadge,
  MInputDateTime,
  MInputSwitch,
  MPageOverviewCard,
  MSingleColumnLayout,
  MViewContainer,
} from '@metaplay/meta-ui-next'
import { sentenceCaseToKebabCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import MetaGenericActivablesCard from '../components/activables/MetaGenericActivablesCard.vue'
import { getStaticConfigSubscriptionOptions } from '../subscription_options/general'

// Props --------------------------------------------------------------------------------------------------------------

const props = defineProps<{
  categoryKey: string
}>()

// Custom user evaluation time ----------------------------------------------------------------------------------------

/**
 * Model for whether custom user evaluation time is enabled ore not.
 */
const userEvaluationEnabled = ref(false)

/**
 * Model for custom user evaluation time input.
 */
const userEvaluationTime = ref<DateTime>(DateTime.now())

/**
 * What time to use for evaluating the activables card/
 */
const customEvaluationTime = computed((): DateTime | undefined => {
  if (userEvaluationEnabled.value) {
    return userEvaluationTime.value
  } else {
    return undefined
  }
})

/**
 * Utility function to prevent undefined inputs.
 */
function onDateTimeChange(value?: DateTime): void {
  if (!value) return
  userEvaluationTime.value = value
}

/**
 * Returns ISO string of time that is being used to evaluate availability of activables.
 */
const evaluationTimeUsed = computed((): string => {
  if (userEvaluationEnabled.value) {
    return String(userEvaluationTime.value.toISO())
  } else {
    return String(DateTime.now().toISO())
  }
})

// Activables data ----------------------------------------------------------------------------------------------------

const { data: staticConfigData } = useSubscription(getStaticConfigSubscriptionOptions())

const categoryInfo = computed((): any => {
  return activablesMetadata.value.categories[props.categoryKey]
})

const categoryDisplayName = computed((): string => {
  return categoryInfo.value.displayName
})

const categoryDescription = computed((): string => {
  return categoryInfo.value.description
})

const activablesMetadata = computed((): any => {
  return staticConfigData.value?.activablesMetadata
})
</script>

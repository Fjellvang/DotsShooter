<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Localization still building.
MCallout(
  v-if="localizationData?.bestEffortStatus === 'Building'"
  title="Building Localizations..."
  variant="neutral"
  ) This localizations are still building. Check back soon!

//- Localization was not built due to an error.
div(v-else-if="localizationData?.bestEffortStatus === 'Failed'")
  MCard(
    title="Error Accessing Localizations Data"
    subtitle="This localization has one or more errors that are preventing it from being published. Here's what we know:"
    variant="danger"
    )
    div(class="tw-space-y-2")
      MErrorCallout(
        v-for="error in localizationData?.publishBlockingErrors"
        :error="localizationErrorToDisplayError(error)"
        )

localization-contents-card(
  v-else
  :localizationId="localizationId"
  )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MCallout, MCard, MErrorCallout } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import type { MinimalLocalizationInfo } from '../../localizationServerTypes'
import { localizationErrorToDisplayError } from '../../localizationUtils'
import { getAllLocalizationsSubscriptionOptions } from '../../subscription_options/localization'
import LocalizationContentsCard from '../global/LocalizationContentsCard.vue'

const props = defineProps<{
  /**
   * Id of localization to display.
   */
  localizationId: string
}>()

// Load localization data ----------------------------------------------------------------------------------------------

/**
 * Fetch data for the specific localization that is to be displayed.
 */
const { data: allLocalizationsData } = useSubscription(getAllLocalizationsSubscriptionOptions())

const localizationData = computed((): MinimalLocalizationInfo | undefined => {
  return (allLocalizationsData.value ?? []).find((localization) => localization.id === props.localizationId)
})
</script>

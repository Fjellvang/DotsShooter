<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Game config still building.
MCallout(
  v-if="gameConfigData?.status === 'Building'"
  title="Building Game Config..."
  variant="neutral"
  ) This game config is still building. Check back soon!

//- Game config was not built due to an error.
div(v-else-if="!gameConfigData?.contents?.sharedLibraries || !gameConfigData?.contents?.serverLibraries")
  MCard(
    title="Error Accessing Config Data"
    subtitle="This game config has one or more errors that are preventing it from being published. Here's what we know:"
    variant="danger"
    )
    div(class="tw-space-y-2")
      MErrorCallout(
        v-for="error in gameConfigData?.publishBlockingErrors"
        :error="gameConfigErrorToDisplayError(error)"
        )

config-contents-card(
  v-else
  :gameConfigId="gameConfigId"
  show-experiment-selector
  )
</template>

<script lang="ts" setup>
import { MCallout, MCard, MErrorCallout } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { gameConfigErrorToDisplayError } from '../../gameConfigUtils'
import { getSingleGameConfigCountsSubscriptionOptions } from '../../subscription_options/gameConfigs'
import ConfigContentsCard from '../global/ConfigContentsCard.vue'

const props = defineProps<{
  /**
   * Id of game config to display.
   */
  gameConfigId: string
}>()

// Load game config data ----------------------------------------------------------------------------------------------

/**
 * Fetch data for the specific game config that is to be displayed.
 */
const { data: gameConfigData } = useSubscription(getSingleGameConfigCountsSubscriptionOptions(props.gameConfigId))
</script>

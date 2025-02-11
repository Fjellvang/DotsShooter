<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allMatchmakersData"
  :error="allMatchmakersError"
  permission="api.matchmakers.view"
  )
  template(#overview)
    MPageOverviewCard(
      title="Matchmaking"
      data-testid="matchmakers-overview-card"
      )
      p(class="tw-mb-1") Matchmakers help players find the best match for themselves for multiplayer game modes.
      p(class="tw-text-xs+ tw-text-neutral-500") You can have multiple unique matchmakers in the same game, each with different settings.

  template(#default)
    core-ui-placement(:placement-id="'Matchmakers/List'")

    meta-raw-data(
      :kvPair="allMatchmakersData"
      name="allMatchmakersData"
      )
</template>

<script lang="ts" setup>
import { MPageOverviewCard, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { getAllMatchmakersSubscriptionOptions } from '../subscription_options/matchmaking'

/**
 * Subscribe to all matchmakers data and error.
 */
const { data: allMatchmakersData, error: allMatchmakersError } = useSubscription(getAllMatchmakersSubscriptionOptions())
</script>

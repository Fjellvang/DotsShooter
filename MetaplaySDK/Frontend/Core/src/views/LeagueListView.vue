<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!allLeaguesData"
  :error="allLeaguesError"
  permission="api.leagues.view"
  )
  template(#alerts)
    MCallout(
      v-if="allLeaguesData.length === 0"
      title="No Leagues Configured"
      variant="neutral"
      ) No leagues have been configured for this deployment. Set them up in your #[MTextButton(to="/environment?tab=2") runtime options].

  template(#overview)
    MPageOverviewCard(
      title="Leagues"
      data-testid="league-list-overview-card"
      )
      p(class="tw-mb-1") Leagues are a season-based leaderboard system for competitive multiplayer.
      p(class="tw-text-sm tw-text-neutral-500") You can have multiple unique leagues in the same game, each with a different configuration.

  MSingleColumnLayout
    core-ui-placement(
      :placement-id="'Leagues/List'"
      always-full-width
      )

  meta-raw-data(
    :kvPair="allLeaguesData"
    name="allLeaguesData"
    )
</template>

<script lang="ts" setup>
import { MCallout, MPageOverviewCard, MSingleColumnLayout, MTextButton, MViewContainer } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { getAllLeaguesSubscriptionOptions } from '../subscription_options/leagues'

/**
 * Subscribe to all leagues data.
 */
const { data: allLeaguesData, error: allLeaguesError } = useSubscription(getAllLeaguesSubscriptionOptions())
</script>

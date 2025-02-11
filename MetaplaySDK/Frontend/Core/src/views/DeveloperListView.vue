<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!developerPlayersData"
  permission="api.players.view_developers"
  :error="developerPlayersError"
  )
  template(#overview)
    MPageOverviewCard(
      title="Developer Players"
      :alerts="overviewAlert"
      data-testid="matchmakers-overview-card"
      )
      p(class="tw-mb-1") Players who have a special developer status
      p(class="tw-text-xs tw-text-neutral-500") These players can be online during maintenance breaks and make it easier for you to test development-only features in production environments.

  //- MetaListCard for Developer Players
  MSingleColumnLayout
    core-ui-placement(
      :placement-id="'Developers/List'"
      always-full-width
      )

  meta-raw-data(
    :kvPair="developerPlayersData"
    name="activePlayers"
    )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import {
  MCallout,
  MPageOverviewCard,
  MSingleColumnLayout,
  MTextButton,
  MViewContainer,
  type MPageOverviewCardAlert,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import CoreUiPlacement from '../components/system/CoreUiPlacement.vue'
import { getDeveloperPlayersSubscriptionOptions } from '../subscription_options/players'

/**
 * Subscribe to developer players data.
 */
const { data: developerPlayersData, error: developerPlayersError } = useSubscription(
  getDeveloperPlayersSubscriptionOptions()
)

const overviewAlert = computed(() => {
  const alerts: MPageOverviewCardAlert[] = []
  if (developerPlayersData.value?.length === 0) {
    alerts.push({
      title: 'No Developer Players',
      message: 'You can find out how to mark a player as a developer from',
      variant: 'neutral',
      linkText: 'our docs',
      link: 'https://docs.metaplay.io/feature-cookbooks/developer-players/working-with-developer-players.html#mark-a-player-as-a-developer',
    })
  }
  return alerts
})
</script>

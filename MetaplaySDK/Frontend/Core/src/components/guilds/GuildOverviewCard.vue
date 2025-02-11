<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Player card (anything important enough to be visible at first glance should go here)
MPageOverviewCard(
  v-if="guildData"
  :id="guildData.model.guildId"
  :title="guildData.model.displayName || '☠️ Closed'"
  :sub-title="guildData.model.description"
  )
  template(#caption)
    | Save file size:
    |
    MTextButton(
      permission="api.database.inspect_entity"
      :to="`/entities/${guildData.model.guildId}/dbinfo`"
      data-testid="model-size-link"
      )
      meta-abbreviate-number(
        :value="guildData.persistedSize"
        unit="byte"
        )

  overview-list(
    listTitle="Overview"
    icon="chart-bar"
    :sourceObject="guildData"
    :items="coreStore.overviewLists.guild"
    )
</template>

<script lang="ts" setup>
import { MPageOverviewCard, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { useCoreStore } from '../../coreStore'
import { getSingleGuildSubscriptionOptions } from '../../subscription_options/guilds'
import OverviewList from '../global/OverviewList.vue'

const coreStore = useCoreStore()

const props = defineProps<{
  guildId: string
}>()

const { data: guildData } = useSubscription(getSingleGuildSubscriptionOptions(props.guildId))
</script>

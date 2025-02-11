<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A wrapper component for displaying a player's guild membership. -->

<template lang="pug">
div(v-if="staticInfos.featureFlags.guilds")
  span(v-if="playerData.guild !== null")
    MTextButton(:to="`/guilds/${playerData.guild.id}`") {{ playerData.guild.displayName }}
    small(class="text-muted tw-ml-1") ({{ guildRoleDisplayString(playerData.guild.role) }})
  span(
    v-else
    class="text-muted"
    ) Not in a guild
</template>

<script lang="ts" setup>
import { useStaticInfos } from '@metaplay/game-server-api'
import { MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { guildRoleDisplayString } from '../../coreUtils'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player displayed on the overview card.
   */
  playerId: string
}>()

const staticInfos = useStaticInfos()
const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))
</script>

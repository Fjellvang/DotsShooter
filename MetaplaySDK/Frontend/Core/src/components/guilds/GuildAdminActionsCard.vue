<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Admin Tools"
  id="guild-admin-actions-card"
  )
  template(#icon)
    fa-icon(icon="wrench")

  div(class="tw-flex tw-flex-wrap tw-justify-center tw-gap-y-3 tw-@container")
    div(class="tw-basis-full tw-pr-1.5 tw-@container @sm:tw-basis-1/2")
      div(class="tw-mb-1 tw-font-bold tw-leading-6") Gentle Actions
      core-ui-placement(
        placementId="Guilds/Details/AdminActions:Gentle"
        :guildId="guildId"
        alwaysFullWidth
        smallBottomMargin
        )

    div(class="tw-basis-full tw-pl-1.5 tw-@container @sm:tw-basis-1/2")
      div(class="tw-mb-1 tw-font-bold tw-leading-6") Disruptive Actions
      core-ui-placement(
        placementId="Guilds/Details/AdminActions:Disruptive"
        :guildId="guildId"
        alwaysFullWidth
        smallBottomMargin
        )
</template>

<script lang="ts" setup>
import { MCard } from '@metaplay/meta-ui-next'

import CoreUiPlacement from '../../components/system/CoreUiPlacement.vue'

defineProps<{
  guildId: string
}>()
</script>

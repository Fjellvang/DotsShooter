<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Admin Tools"
  data-testid="player-admin-actions-card"
  )
  template(#icon)
    fa-icon(icon="wrench")

  div(class="tw-flex tw-flex-wrap tw-justify-center tw-gap-y-3 tw-@container")
    div(class="tw-basis-full tw-pr-1.5 tw-@container @sm:tw-basis-1/2 @xl:tw-basis-1/3")
      div(class="tw-mb-1 tw-font-bold tw-leading-6") Gentle Actions
      core-ui-placement(
        placementId="Players/Details/AdminActions:Gentle"
        :playerId="playerId"
        always-full-width
        smallBottomMargin
        )

    div(class="tw-basis-full tw-px-1.5 tw-@container @sm:tw-basis-1/2 @xl:tw-basis-1/3")
      div(class="tw-mb-1 tw-font-bold tw-leading-6") Disruptive Actions
      core-ui-placement(
        placementId="Players/Details/AdminActions:Disruptive"
        :playerId="playerId"
        always-full-width
        smallBottomMargin
        )

    div(class="tw-basis-full tw-pl-1.5 tw-@container @sm:tw-basis-1/2 @xl:tw-basis-1/3")
      div(class="tw-mb-1 tw-font-bold tw-leading-6") Dangerous Actions
      core-ui-placement(
        placementId="Players/Details/AdminActions:Dangerous"
        :playerId="playerId"
        always-full-width
        smallBottomMargin
        )
</template>

<script lang="ts" setup>
import { MCard } from '@metaplay/meta-ui-next'

import CoreUiPlacement from '../system/CoreUiPlacement.vue'

defineProps<{
  /**
   * ID of the player to show admin actions for.
   */
  playerId: string
}>()
</script>

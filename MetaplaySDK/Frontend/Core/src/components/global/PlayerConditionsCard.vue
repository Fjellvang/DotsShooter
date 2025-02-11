<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  title="Additional Conditions"
  icon="cog"
  :itemList="decoratedPlayerConditions"
  emptyMessage="No additional conditions defined."
  data-testid="player-conditions-card"
  )
  template(#item-card="{ item: condition }")
    MListItem(v-if="condition.conditionType === 'MetaOfferPrecursorCondition'") #[meta-duration(:duration="condition.delay")] after offer #[MTextButton(:to="`/offerGroups/offer/${condition.offerId}`") {{ condition.offerId }}] {{ condition.purchased == true ? 'was' : 'was not' }} purchased.
    MListItem(v-else) Unknown condition type: {{ condition.conditionType }}
      template(#bottom-left)
        pre(style="font-size: 0.7rem") {{ condition }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MListItem, MTextButton } from '@metaplay/meta-ui-next'

const props = defineProps<{
  /**
   * Optional: Array of additional criteria for targeting players.
   */
  playerConditions?: any[]
}>()

/**
 * Decorate the conditions with their type, extracted from the class name.
 */
const decoratedPlayerConditions = computed(() => {
  if (props.playerConditions) {
    return props.playerConditions.map((x: any) => {
      return {
        ...x,
        conditionType: x.$type.split('.').pop().split(',')[0],
      }
    })
  } else {
    return []
  }
})
</script>

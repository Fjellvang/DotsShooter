<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->
<!-- An opinionated component to display humanized MetaReward types. -->

<template lang="pug">
//- TODO: This 'rewardWithMetaData' is not actually needed -> refactor to just look up the reward entry to make this simpler
MBadge(class="tw-mr-1") {{ rewardDisplayValue() }}
</template>

<script lang="ts" setup>
import type { PropType } from 'vue'

import { MBadge } from '@metaplay/meta-ui-next'

import { rewardWithMetaData } from '../utils/rewardUtils'

const props = defineProps({
  reward: {
    type: Object as PropType<{ $type: string; amount: number }>,
    default: null,
  },
})

const rewardDisplayValue = (): string => {
  if (props.reward) {
    return rewardWithMetaData(props.reward).getDisplayValue(props.reward)
  } else {
    return 'null'
  }
}
</script>

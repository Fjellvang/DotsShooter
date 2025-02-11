<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
span {{ toOrdinalString(number) }}
</template>

<script lang="ts" setup>
import { watch } from 'vue'

import { toOrdinalString } from '@metaplay/meta-utilities'

const props = defineProps<{
  number: number
}>()

watch(
  () => props.number,
  (newValue) => {
    if (newValue <= 0) {
      throw new Error('Number must be greater than 0.')
    }
  },
  { immediate: true }
)
</script>

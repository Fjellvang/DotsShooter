<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(
  v-if="hasGameTimeOffset"
  class="text-warning"
  ) The time in this environment has been skipped #[meta-duration(:duration="gameTimeOffset" showAs="exactDuration" disableTooltip)] ahead of real time.
</template>

<script setup lang="ts">
import { useGameTimeOffset } from '../composables/useGameTimeOffset'

const { gameTimeOffset, hasGameTimeOffset } = useGameTimeOffset()
</script>

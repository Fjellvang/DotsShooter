<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
iframe(
  v-if="staticInfos.environmentInfo.grafanaUri"
  :src="staticInfos.environmentInfo.grafanaUri"
  frameborder="0"
  width="100%"
  height="800px"
  )
</template>

<script lang="ts" setup>
import { useStaticInfos } from '@metaplay/game-server-api'

const staticInfos = useStaticInfos()
</script>

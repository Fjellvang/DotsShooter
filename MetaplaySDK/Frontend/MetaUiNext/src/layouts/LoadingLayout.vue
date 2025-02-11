<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-flex tw-h-dvh tw-items-center tw-justify-center")
  div(class="tw-space-y-5")
    MetaplayLogo(class="tw-h-auto tw-w-72 tw-fill-green-500")
    MProgressBar(
      :value="0.5"
      variant="success"
      )
</template>

<script lang="ts" setup>
import MetaplayLogo from '../assets/MetaplayLogo.vue'
import MProgressBar from '../unstable/MProgressBar.vue'
</script>

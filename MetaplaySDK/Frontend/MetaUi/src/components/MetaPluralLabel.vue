<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->
<!-- A small component to display a plural after a number label, if needed. -->

<template lang="pug">
span {{ maybePluralString(value, label, !hideCount) }}
</template>

<script lang="ts" setup>
import { maybePluralString } from '@metaplay/meta-utilities'

defineProps({
  value: {
    type: Number,
    required: true,
    validator: (n: number) => {
      return n >= 0
    },
  },
  label: {
    type: String,
    required: true,
  },
  hideCount: {
    type: Boolean,
    default: false,
  },
})
</script>

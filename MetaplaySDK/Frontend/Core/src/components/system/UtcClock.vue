<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTooltip(no-underline)
  template(#content)
    div UTC: {{ utcTooltipTime }}
    div UTC{{ offset }}: {{ localTooltipTime }}
    div Your local time is UTC{{ offset }}.
    MGameTimeOffsetHint(class="tw-mt-2")

  div(
    :class="[textClass.normal, { 'tw-border tw-rounded-md tw-border-orange-200 tw-py-1 tw-px-2 tw-bg-orange-100 tw-text-orange-900': hasGameTimeOffset }]"
    )
    div(class="tw-flex tw-justify-between tw-gap-1.5 tw-text-xs")
      div
        div UTC
        div(:class="`${textClass.muted}`") UTC{{ offset }}
      div
        div(:class="`tw-flex ${textClass.normal} tw-justify-center`")
          span(
            v-for="digit in utcDisplayTime.hours"
            class="tw-w-[0.45rem] tw-text-center"
            ) {{ digit }}
          span(class="tw-mx-[0.045rem]") :
          span(
            v-for="digit in utcDisplayTime.minutes"
            class="tw-w-[0.45rem] tw-text-center"
            ) {{ digit }}
        div(class="tw-flex tw-justify-center")
          span(
            v-for="digit in localDisplayTime.hours"
            class="tw-w-[0.45rem] tw-text-center"
            ) {{ digit }}
          span(class="tw-mx-[0.045rem]") :
          span(
            v-for="digit in localDisplayTime.minutes"
            class="tw-w-[0.45rem] tw-text-center"
            ) {{ digit }}
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { computed, onMounted, onUnmounted, ref } from 'vue'

import { MTooltip, MGameTimeOffsetHint, useGameTimeOffset } from '@metaplay/meta-ui-next'

const { hasGameTimeOffset } = useGameTimeOffset()

const props = defineProps<{
  backgroundColorString?: string
}>()

const now = ref(DateTime.now())

const utcTooltipTime = computed(() => now.value.setZone('UTC').toFormat('yyyy-MM-dd HH:mm'))
const localTooltipTime = computed(() => now.value.toFormat('yyyy-MM-dd HH:mm'))

/**
 * The offset of local time zone compared to UTC in hours.
 */
const offset = computed(() => {
  // When timezone is at UTC offset Luxon returns -0 instead of 0, which is wrong.
  const offset = now.value.offset ? now.value.offset / 60 : 0
  return Intl.NumberFormat(undefined, { signDisplay: 'always' }).format(offset)
})

const utcDisplayTime = computed(() => {
  const utcTime = now.value.setZone('UTC').toFormat('HH.mm')
  const [hours, minutes] = utcTime.split('.')
  return { hours, minutes }
})

const localDisplayTime = computed(() => {
  const localTime = now.value.toFormat('HH.mm')
  const [hours, minutes] = localTime.split('.')
  return { hours, minutes }
})

let intervalId: ReturnType<typeof setTimeout>

/**
 * Update the current time on a timer.
 */
onMounted(() => {
  intervalId = setInterval(() => {
    now.value = DateTime.now()
  }, 3000)
})

onUnmounted(() => {
  clearInterval(intervalId)
})

/**
 * The text color classes for the UTC clock. This is based on the background color of the parent element.
 */
const textClass = computed((): { normal?: string; muted?: string } => {
  if (hasGameTimeOffset.value || !headerLightTextColor.value) {
    // Background color is light (or GameTimeOffset is active so we have an orange background behind the clock) so use
    // dark text.
    return {
      normal: undefined,
      muted: 'tw-text-neutral-500',
    }
  } else {
    // Background color is dark, so use light text.
    return {
      normal: 'tw-text-neutral-50',
      muted: 'tw-text-neutral-300',
    }
  }
})

const headerLightTextColor = computed(() => {
  // Return true if the header background color is dark enough to have better contrast with light text.
  // See https://stackoverflow.com/a/41491220/1243212
  if (!props.backgroundColorString) {
    return false
  }
  const hex = props.backgroundColorString.replace('#', '')
  const c = hex.length === 3 ? hex.split('').map((x) => x + x) : hex.match(/.{2}/g)
  if (!c) {
    return false
  }
  const r = parseInt(c[0], 16)
  const g = parseInt(c[1], 16)
  const b = parseInt(c[2], 16)
  const brightness = (r * 299 + g * 587 + b * 114) / 1000
  return brightness < 125
})
</script>

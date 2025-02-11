<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A utility to display humanized timestamps. -->

<template lang="pug">
MTooltip
  template(
    v-if="!disableTooltip"
    #content
    )
    span {{ tooltipContent }}
  span {{ bodyContent }}
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import MTooltip from '../primitives/MTooltip.vue'

const props = defineProps<{
  /**
   * The date and time to display.
   */
  instant: DateTime
  /**
   * Optional: Disable the tooltip that shows local time.
   */
  disableTooltip?: boolean
}>()

/**
 * Content to display in the main body of the component.
 */
const bodyContent = computed(() => {
  // Explicity setting the locale to 'en-gb' to ensure the date is formatted in a consistent way.
  return props.instant.toUTC().toLocaleString({
    year: 'numeric',
    month: 'short',
    weekday: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
})

/**
 * Content to display in the tooltip.
 */
const tooltipContent = computed(() => {
  const timezoneOffset = timezoneOffsetString(props.instant)
  return (
    `UTC: ${props.instant.toUTC().toFormat('yyyy-MM-dd HH:mm')}\n` +
    `UTC ${timezoneOffset}: ${props.instant.toFormat('yyyy-MM-dd HH:mm')} \n` +
    `Your local time on this date will be UTC${timezoneOffset}.`
  )
})

/**
 * The timezone offset as a string.
 * @param instant The instant to get the timezone offset for.
 * @returns The timezone offset as a string.
 * @example timezoneOffsetString(DateTime.fromISO('2021-06-01T12:00:00.000+03:00')) -> '+3'
 */
function timezoneOffsetString(instant: DateTime): string {
  // Luxon returns offset as `-0` instead of `+0` when timezone is at UTC, so we correct it here to `+0`.
  const offset = instant.offset ? instant.offset / 60 : +0
  return Intl.NumberFormat(undefined, { signDisplay: 'always' }).format(offset)
}
</script>

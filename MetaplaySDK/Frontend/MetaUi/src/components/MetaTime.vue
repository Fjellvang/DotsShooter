<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A utility to display humanized time stamps. -->

<template lang="pug">
MTooltip
  template(
    v-if="!disableTooltip"
    #content
    )
    div {{ tooltipContent.firstLine }}
    div {{ tooltipContent.utc }}
    MGameTimeOffsetHint(class="tw-mt-2")
  span(
    v-if="bodyContent !== 'invalid'"
    :class="hasGameTimeOffset ? 'text-warning' : ''"
    ) {{ bodyContent }}
  span(
    v-else
    class="text-danger"
    ) Invalid showAs prop!
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { ref, computed, onUnmounted } from 'vue'

import { MTooltip, MGameTimeOffsetHint, useGameTimeOffset } from '@metaplay/meta-ui-next'
import { stringToSentenceCase, toOrdinalString } from '@metaplay/meta-utilities'

const props = withDefaults(
  defineProps<{
    /**
     * The date to display. Can be a string or a Luxon DateTime object.
     */
    date: string | DateTime
    /**
     * Optional: Disable the tooltip that shows the full date and time.
     */
    disableTooltip?: boolean
    /**
     * Optional: How to display the date. Defaults to 'timeago'.
     */
    showAs?: 'timeago' | 'timeagoSentenceCase' | 'time' | 'date' | 'datetime'
  }>(),
  {
    showAs: 'timeago',
  }
)

const { hasGameTimeOffset } = useGameTimeOffset()

/**
 * Which tooltip to show depending on exactDuration showAs.
 */
const tooltipContent = computed(() => {
  const datetime = luxonDateTime.value.toFormat('yyyy-MM-dd HH:mm:ss')
  const utcDateTime = luxonDateTime.value.setZone('UTC').toFormat('yyyy-MM-dd HH:mm:ss') // The date and time in UTC. e.g "2021-01-01 12:00:00"

  if (props.showAs.startsWith('timeago')) {
    return {
      firstLine: `Local: ${datetime}`, // The date and time in local time. e.g "2021-01-01 12:00:00"
      utc: `UTC: ${utcDateTime}`,
    }
  } else {
    return {
      firstLine: stringToSentenceCase(timeAgoString.value),
      utc: `UTC: ${utcDateTime}`,
    }
  }
})

/**
 * The string for the body of the tooltip.
 */
const bodyContent = computed(() => {
  switch (props.showAs) {
    case 'timeago':
      return timeAgoString.value
    case 'timeagoSentenceCase':
      return stringToSentenceCase(timeAgoString.value)
    case 'time':
      return luxonDateTime.value.toFormat('HH:mm:ss') // The time in local time. e.g "12:00:00"
    case 'date':
      return `${luxonDateTime.value.toFormat('MMM')} ${toOrdinalString(luxonDateTime.value.day)}, ${luxonDateTime.value.toFormat('yyyy')}` // The date in local time. e.g "Jan 1st, 2021"
    case 'datetime':
      return `${luxonDateTime.value.toFormat('MMM')} ${toOrdinalString(luxonDateTime.value.day)}, ${luxonDateTime.value.toFormat('yyyy')} ${luxonDateTime.value.toFormat('HH:mm:ss')}` // The date and time in local time. e.g "Jan 1st, 2021 12:00:00"
    // eslint-disable-next-line @typescript-eslint/switch-exhaustiveness-check -- Keep in case of bad runtime data.
    default:
      return 'invalid'
  }
})

/**
 * The date as a Luxon DateTime object.
 */
const luxonDateTime = computed((): DateTime => {
  if (props.date instanceof DateTime) {
    return props.date
  } else {
    return DateTime.fromISO(props.date)
  }
})

/**
 * Humanised time string.
 * @example "5 minutes ago"
 */
const timeAgoString = computed((): string => {
  // Fake dependency so that timeAgoString gets re-computed whenever the refresh timer gets reset
  if (refreshTimer.value === undefined) {
    // First time we compute this string, start a periodic refresh timer
    // TODO: This causes a `[Vue warn] Computed is still dirty after getter evaluation` warning when the `showAs` type
    // is `timeago` or `timeagoSentenceCase`. It's harmless but should be fixed.
    refreshPeriodically()
  }
  return luxonDateTime.value.toRelative() ?? 'Invalid DateTime'
})

// Hack to update the above text.
const refreshTimer = ref<ReturnType<typeof setTimeout>>()
onUnmounted(() => {
  clearTimeout(refreshTimer.value)
})

function refreshPeriodically(): void {
  refreshTimer.value = setTimeout(() => {
    refreshPeriodically()
  }, 5000)
}
</script>

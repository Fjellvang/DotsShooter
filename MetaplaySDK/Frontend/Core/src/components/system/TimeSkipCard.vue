<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Time Skip"
  :badge="cardBadge.text"
  :badge-variant="cardBadge.variant"
  data-testid="system-game-time-settings-card"
  )
  template(#subtitle) Set the game server's time to a future date to test complex LiveOps setups.

  p(v-if="hasGameTimeOffset") Current game server time is #[meta-duration(:duration="gameTimeOffset" showAs="exactDuration" disableTooltip)] ahead of real time.

  //- Action Modal.
  template(#buttons)
    MActionModalButton(
      modal-title="Change Game Server Time Skip"
      :action="submitGameTimeOffset"
      trigger-button-label="Open Time Skip Menu"
      :trigger-button-disabled-tooltip="!staticInfos.featureFlags.gameTimeSkip ? 'Time skipping is disabled in this environment.' : undefined"
      ok-button-label="Apply Time Skip"
      :ok-button-disabled-tooltip="offsetDisabledReason"
      permission="api.game_time_offset.edit"
      variant="danger"
      @show="resetModal"
      )
      template(#default)
        p Time skipping is a way to set the game server's time to a future date. The game server time is directly set to the time you specify without simulating all the ticks in between.
        //p(class="tw-text-red-500 tw-mt-2") This is an advanced development feature and should only be used in environments where it is ok to reset the database after you are done.
        p(class="tw-mt-2 tw-text-sm tw-text-neutral-400") Time skipping is a useful tool for testing how the game would feel like at a future date. For example, you might want to test how the game server behaves when a certain scheduled event happens in the future.
        MetaNoSeatbelts(class="tw-mx-auto tw-mt-4 tw-max-w-xl") Time skipping only goes one way. You will be able to skip further into the future, but can never time travel backwards. You will have to reset your database to disable time skipping.

      template(#right-panel)
        div Current #[span(class="tw-font-bold") real] time is:
        div(class="tw-mt-1 tw-rounded-md tw-border tw-border-neutral-300 tw-p-2")
          div UTC: {{ currentRealTimeUtc }}
          div UTC{{ localUtcOffset }}: {{ currentRealTimeLocal }}

        div(v-if="hasGameTimeOffset")
          div(class="tw-mt-2") Current #[span(class="tw-font-bold") game server] time is #[meta-duration(:duration="gameTimeOffset" showAs="exactDuration" disableTooltip class="tw-font-bold")] ahead of real time:
          div(class="tw-mt-1 tw-rounded-md tw-border tw-border-neutral-300 tw-p-2")
            div UTC: {{ currentGameTimeUtc }}
            div UTC{{ localUtcOffset }}: {{ currentGameTimeLocal }}
        div(
          v-else
          class="tw-mt-2"
          ) No time skip has been applied yet.

        div(class="tw-mt-4")
          //- NOTE: Here, "end date time" of MInputDurationOrEndDateTime is a misnomer,
          //-       since we're picking something else than an *end* time. Note that
          //-       we're customizing the title and hint texts accordingly.
          //-       Other than that, this picker is the appropriate tool for what we want here.
          //-       Maybe "target" rather than "end" date time would be a more appropriate term.
          MInputDurationOrEndDateTime(
            :model-value="offsetInput"
            :reference-date-time="realNowLocal"
            input-mode="endDateTime"
            duration-title="Offset From Real Time"
            dateTime-title="Target Time"
            @update:model-value="offsetInput = $event"
            )
            template(#duration-hint-message="durationHintProps") The skipped time will be {{ durationHintProps.dateTime }}.
            template(#end-time-hint-message="endTimeHintProps") The skipped time will be {{ endTimeHintProps.duration }} ahead of real time.
</template>

<script lang="ts" setup>
import { DateTime, Duration } from 'luxon'
import { computed, onMounted, onUnmounted, ref } from 'vue'

import { useGameServerApi, useStaticInfos } from '@metaplay/game-server-api'
import { MetaNoSeatbelts } from '@metaplay/meta-ui'
import {
  MActionModalButton,
  MCard,
  MInputDurationOrEndDateTime,
  useGameTimeOffset,
  useNotifications,
  type Variant,
} from '@metaplay/meta-ui-next'

const staticInfos = useStaticInfos()
const gameServerApi = useGameServerApi()
const { gameTimeOffset, hasGameTimeOffset } = useGameTimeOffset()
const { showSuccessNotification } = useNotifications()

// Real world time ----------------------------------------------------------------------------------------------------

/**
 * Current real world time. We derive all other times form this.
 * Note that this is rounded down to the nearest minute.
 */
const realNowLocal = ref(getRealLocalDateTime())

/**
 * Helper function to get the current real local time, rounded down to the nearest minute.
 */
function getRealLocalDateTime(): DateTime {
  return DateTime.fromMillis(Date.now()).startOf('minute')
}

/**
 * Update the current time on a timer.
 */
onMounted(() => {
  const intervalId = setInterval(() => {
    realNowLocal.value = getRealLocalDateTime()
  }, 3000)

  // Clear the timer when the component is unmounted.
  onUnmounted(() => {
    clearInterval(intervalId)
  })
})

/**
 * The current local UTC offset.
 */
const localUtcOffset = computed(() => {
  // When timezone is at UTC offset Luxon returns -0 instead of 0, which is wrong.
  const offset = realNowLocal.value.offset ? realNowLocal.value.offset / 60 : 0
  return Intl.NumberFormat(undefined, { signDisplay: 'always' }).format(offset)
})

// Derived time display values ----------------------------------------------------------------------------------------

/**
 * Current real time in UTC.
 * Note that this is rounded down to the nearest minute.
 */
const currentRealTimeUtc = computed(() => {
  return realNowLocal.value.toUTC().toFormat('yyyy-MM-dd HH:mm')
})

/**
 * Current real time in local time.
 * Note that this is rounded down to the nearest minute.
 */
const currentRealTimeLocal = computed(() => {
  return realNowLocal.value.toFormat('yyyy-MM-dd HH:mm')
})

/**
 * Current game time in UTC.
 * Note that this is rounded down to the nearest minute.
 */
const currentGameTimeUtc = computed(() => {
  const currentOffset: Duration = gameTimeOffset.value
  return realNowLocal.value.plus(currentOffset).toUTC().toFormat('yyyy-MM-dd HH:mm')
})

/**
 * Current game time in local time.
 * Note that this is rounded down to the nearest minute.
 */
const currentGameTimeLocal = computed(() => {
  const currentOffset: Duration = gameTimeOffset.value
  return realNowLocal.value.plus(currentOffset).toFormat('yyyy-MM-dd HH:mm')
})

// UI and input -------------------------------------------------------------------------------------------------------

/**
 * User input for the time offset.
 */
const offsetInput = ref(gameTimeOffset.value)

/**
 * Reset the modal to its initial state.
 */
function resetModal(): void {
  offsetInput.value = gameTimeOffset.value
}

/**
 * Submit the time offset to the backend.
 */
async function submitGameTimeOffset(): Promise<void> {
  const payload = { offsetMilliseconds: offsetInput.value.toMillis() }
  await gameServerApi.put('/gameTimeOffset', payload)
  showSuccessNotification('Time skip updated.')
}

/**
 * String explaining why the submit button is disabled, or `undefined` to enable it.
 */
const offsetDisabledReason = computed((): string | undefined => {
  const offsetDiffInMs = offsetInput.value.toMillis() - gameTimeOffset.value.toMillis()
  if (offsetDiffInMs < 0) {
    return 'The time skip can only be increased; it cannot go backwards from its current value.'
  } else if (offsetDiffInMs === 0 && gameTimeOffset.value.toMillis() === 0) {
    return 'Enter a time skip value.'
  } else if (offsetInput.value.toMillis() === gameTimeOffset.value.toMillis()) {
    return 'Enter a new time skip value.'
  } else {
    return undefined
  }
})

/**
 * Details for the badge on the main card.
 */
const cardBadge = computed((): { text: string; variant: Variant } => {
  if (hasGameTimeOffset.value) {
    return {
      text: 'Active',
      variant: 'danger',
    }
  } else if (!staticInfos.featureFlags.gameTimeSkip) {
    return {
      text: 'Disabled',
      variant: 'neutral',
    }
  } else {
    return {
      text: 'Off',
      variant: 'neutral',
    }
  }
})
</script>

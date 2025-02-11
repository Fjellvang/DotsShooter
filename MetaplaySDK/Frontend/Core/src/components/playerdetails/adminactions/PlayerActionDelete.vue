<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  v-if="playerData"
  modal-title="Schedule Player for Deletion"
  :action="updateDeletionSchedule"
  :trigger-button-label="!isCurrentlyScheduledForDeletion ? 'Delete Player' : 'Cancel Deletion'"
  variant="danger"
  ok-button-label="Save"
  :ok-button-disabled-Tooltip="!isFormChanged ? `Toggle the switch to ${!isCurrentlyScheduledForDeletion ? 'delete' : 'cancel the deletion of'} this player.` : undefined"
  permission="api.players.edit_scheduled_deletion"
  @show="resetModal"
  data-testid="action-delete-player"
  )
  p(
    v-if="!isCurrentlyBanned && !isCurrentlyScheduledForDeletion"
    class="tw-mb-4"
    ) Scheduling a player for deletion does not prevent the player from playing the game. The player can still connect and play the game until the deletion has completed. If you wish to stop the player from connecting you should also ban them.
  p(v-if="isCurrentlyBanned && isCurrentlyScheduledForDeletion") The player is currently banned and will not be able to play the game, even if you cancel the scheduled deletion. To allow the player to play the game you must also un-ban them.
  p(v-if="isCurrentlyScheduledForDeletion") The player is currently scheduled for deletion. {{ deletionStatusText }}

  MInputDateTime(
    label="Scheduled Deletion Time (UTC)"
    :model-value="scheduledDateTime"
    min-date-time="utcNow"
    allow-empty
    :hintMessage="hintMessage"
    @update:model-value="(newValue) => (scheduledDateTime = newValue)"
    data-testid="scheduled-date-time"
    )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { useGameServerApi, useStaticInfos } from '@metaplay/game-server-api'
import type { PlayerDeletionStatus } from '@metaplay/meta-ui'
import { MActionModalButton, MInputDateTime, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { parseDotnetTimeSpanToLuxon } from '../../../coreUtils'
import { getSinglePlayerSubscriptionOptions } from '../../../subscription_options/players'

const props = defineProps<{
  /**
   * Id of the player to target the reset action at.
   **/
  playerId: string
}>()

const gameServerApi = useGameServerApi()
const staticInfos = useStaticInfos()

/**
 * Subscribe to player data used to render this component.
 */
const { data: playerData, refresh: playerRefresh } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * Specifies the date and time when the target player will be deleted. Undefined if the player is not scheduled for deletion.
 */
const scheduledDateTime = ref<DateTime | undefined>(DateTime.now().plus({ days: 7 }))

/**
 * When true the target player is currently scheduled for deletion.
 */
const isCurrentlyScheduledForDeletion = computed(() => {
  return playerData.value.model.deletionStatus !== 'None'
})

/**
 * Checks whether the player is currently banned.
 */
const isCurrentlyBanned = computed(() => {
  return playerData.value.model.isBanned
})

/**
 * Checks whether the deletion status or scheduled deletion date/time of a target player has been changed.
 */
const isFormChanged = computed(() => {
  if (!!scheduledDateTime.value !== isCurrentlyScheduledForDeletion.value) {
    // Toggle was changed.
    return true
  } else if (
    scheduledDateTime.value &&
    !scheduledDateTime.value.equals(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      DateTime.fromISO(playerData.value.model.scheduledForDeletionAt)
    )
  ) {
    // Was already scheduled, but the date was changed.
    return true
  } else {
    return false
  }
})

/**
 * Indicates whether the target player will be deleted immediately or at a future date/time.
 */
const isScheduledDateTimeInTheFuture = computed(() => {
  if (!scheduledDateTime.value) return false
  else return scheduledDateTime.value.diff(DateTime.now()).toMillis() >= 0
})

/**
 * Human readable description of how the player's deletion was scheduled.
 */
const deletionStatusText = computed(() => {
  switch (playerData.value.model.deletionStatus as PlayerDeletionStatus) {
    case 'ScheduledByAdmin':
      return 'The deletion was scheduled by a dashboard user.'
    case 'ScheduledByUser':
      return 'The deletion was requested in-game by the player.'
    case 'ScheduledBySystem':
      return 'The deletion was scheduled by an automated system.'
    default:
      return 'Unexpected deletion status.'
  }
})

const hintMessage = computed(() => {
  if (!isCurrentlyScheduledForDeletion.value && !scheduledDateTime.value) {
    return 'This player is not currently scheduled for deletion.'
  } else if (!isCurrentlyScheduledForDeletion.value && scheduledDateTime.value) {
    return `This player will be deleted on ${scheduledDateTime.value.toUTC().toLocaleString(DateTime.DATETIME_FULL_WITH_SECONDS)}.`
  } else if (isCurrentlyScheduledForDeletion.value && !scheduledDateTime.value) {
    return 'This player will no longer be deleted.'
  } else if (scheduledDateTime.value) {
    return `This player is scheduled for deletion on ${scheduledDateTime.value.toUTC().toLocaleString(DateTime.DATETIME_FULL_WITH_SECONDS)}.`
  } else return 'Unexpected deletion status.'
})

/**
 * Reset state of the modal.
 */
function resetModal(): void {
  if (isCurrentlyScheduledForDeletion.value) {
    // User has specified an exact time.
    scheduledDateTime.value = DateTime.fromISO(
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      playerData.value.model.scheduledForDeletionAt
    )
  } else {
    // Use default date of current time + delay.
    const delay = parseDotnetTimeSpanToLuxon(staticInfos.liveOpsDashboardInfo.playerDeletionDefaultDelay)
    const delayedDateTime = DateTime.now().plus(delay)
    scheduledDateTime.value = delayedDateTime
  }
}

const { showSuccessNotification } = useNotifications()

/**
 * Update the date and time when the target player is to be deleted.
 */
async function updateDeletionSchedule(): Promise<void> {
  const message = `${playerData.value.model.playerName || 'n/a'} ${scheduledDateTime.value ? 'scheduled for deletion' : 'is no longer scheduled for deletion'}.`
  if (scheduledDateTime.value) {
    await gameServerApi.put(`/players/${playerData.value.id}/scheduledDeletion`, {
      scheduledForDeleteAt: scheduledDateTime.value,
    })
  } else {
    await gameServerApi.delete(`/players/${playerData.value.id}/scheduledDeletion`)
  }
  showSuccessNotification(message)
  playerRefresh()
}
</script>

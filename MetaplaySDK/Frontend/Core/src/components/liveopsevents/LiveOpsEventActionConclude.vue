<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Conclude Event"
  :action="onOk"
  trigger-button-label="Conclude"
  :trigger-button-disabled-tooltip="singleLiveOpsEventData?.currentPhase === 'Ended' ? 'This event has already ended.' : undefined"
  trigger-button-full-width
  variant="warning"
  ok-button-label="Conclude"
  permission="api.liveops_events.edit"
  )
  p Concluding #[MBadge {{ singleLiveOpsEventData?.eventParams.displayName }}] will force it to end immediately, regardless of its schedule.
  meta-no-seatbelts
</template>

<script lang="ts" setup>
import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, MBadge, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getSingleLiveOpsEventsSubscriptionOptions } from '../../subscription_options/liveOpsEvents'

const gameServerApi = useGameServerApi()

const props = defineProps<{
  /**
   * ID of the event to conclude.
   */
  eventId: string
}>()

/**
 * Information about the event.
 */
const { data: singleLiveOpsEventData, refresh: singleLiveOpsEventRefresh } = useSubscription(
  getSingleLiveOpsEventsSubscriptionOptions(props.eventId)
)

const { showSuccessNotification } = useNotifications()

/**
 * Called when the modal OK button is clicked.
 */
async function onOk(): Promise<void> {
  await gameServerApi.post(`/concludeLiveOpsEvent/${props.eventId}`)
  showSuccessNotification('Event concluded.')
  singleLiveOpsEventRefresh()
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- Delete modal
MActionModalButton(
  modal-title="Delete broadcast"
  :action="deleteBroadcast"
  trigger-button-label="Delete"
  ok-button-label="Delete"
  variant="danger"
  permission="api.broadcasts.edit"
  data-testid="action-delete-broadcast"
  )
  div(class="tw-flex tw-flex-col tw-space-y-2")
    p Deleting this broadcast will prevent any more players from receiving it. However, it will not be removed from the inbox of those who have already received it.
    meta-no-seatbelts(class="tw-mx-auto tw-w-3/4")
</template>

<script lang="ts" setup>
import { useRoute, useRouter } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import { MActionModalButton, useNotifications } from '@metaplay/meta-ui-next'

const props = defineProps<{
  /** Id of the broadcast to delete */
  id: string
}>()

const gameServerApi = useGameServerApi()
const route = useRoute()
const router = useRouter()

const emits = defineEmits(['refresh'])

const { showSuccessNotification } = useNotifications()

/**
 * Deletes the displayed broadcast and navigates back to the broadcast list view.
 */
async function deleteBroadcast(): Promise<void> {
  await gameServerApi.delete(`/broadcasts/${props.id}`)
  const message = `Broadcast with id ${props.id} deleted.`
  showSuccessNotification(message)

  // Close modal
  if (route.path.includes(props.id)) {
    // Navigate back to broadcast list page
    await router.push('/broadcasts')
  }
  emits('refresh')
}
</script>

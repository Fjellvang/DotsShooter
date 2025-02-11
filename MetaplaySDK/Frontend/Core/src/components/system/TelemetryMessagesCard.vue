<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->
<template lang="pug">
meta-list-card(
  v-if="telemetryMessagesData"
  icon="message"
  title="Telemetry Messages"
  :itemList="telemetryMessagesData?.messages"
  :searchFields="['category', 'level', 'title', 'content']"
  emptyMessage="No telemetry messages. All components are up-to-date!"
  permission="api.system.view_telemetry_messages"
  data-testid="telemetry-messages-card"
  )
  template(#item-card="{ item: msgInfo }")
    MListItem
      | {{ msgInfo.title }}

      template(#top-right)
        MBadge(:variant="getBadgeVariantForMessageLevel(msgInfo.level)") {{ msgInfo.level }}
        div(v-for="link in msgInfo.links")
          MTextButton(:to="link.url") {{ link.text }}

      template(#bottom-left)
        span {{ msgInfo.body }}

MCard(
  v-else-if="telemetryMessagesError"
  title="Telemetry Messages"
  :error="telemetryMessagesError"
  data-testid="telemetry-messages-card"
  )
  template(#icon)
    fa-icon(icon="message")
</template>

<script setup lang="ts">
import { MetaListCard } from '@metaplay/meta-ui'
import { MBadge, MTextButton, MListItem, type Variant, MCard } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { getTelemetryMessagesSubscriptionOptions } from '../../subscription_options/general'

const { data: telemetryMessagesData, error: telemetryMessagesError } = useSubscription(
  getTelemetryMessagesSubscriptionOptions()
)

function getBadgeVariantForMessageLevel(level: string): Variant {
  switch (level) {
    case 'Information':
      return 'primary'
    case 'Warning':
      return 'warning'
    case 'Error':
      return 'danger'
    default:
      return 'primary'
  }
}
</script>

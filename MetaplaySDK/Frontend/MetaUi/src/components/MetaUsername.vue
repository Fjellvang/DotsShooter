<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!--
A small component to display dashboard usernames. The result is a clickable link that takes you to the audit log page.
-->

<template lang="pug">
MTextButton(
  v-if="props.renderAs === 'link'"
  :to="auditLogSearchLink"
  :variant="isOwnUsername ? 'success' : undefined"
  permission="api.audit_logs.search"
  ) {{ humanizeUsername(username) }}
span(
  v-else
  :class="isOwnUsername ? 'tw-text-green-500' : ''"
  ) {{ humanizeUsername(props.username) }}
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useGameServerApiStore } from '@metaplay/game-server-api'
import { MTextButton } from '@metaplay/meta-ui-next'

import { humanizeUsername } from '../utils/utils'

const props = withDefaults(
  defineProps<{
    /**
     * The username to render.
     */
    username: string
    /**
     * Optional: Render the username as a `link` (which is clickable) or as `text` (which is not). Defaults to `link`.
     */
    renderAs?: 'link' | 'text'
  }>(),
  {
    renderAs: 'link',
  }
)

/**
 * Does the supplied username match with the currently authenticated username?
 */
const isOwnUsername = computed(() => {
  return props.username === useGameServerApiStore().auth.userDetails.email
})

/**
 * Create a link to the audit logs out of the username.
 */
const auditLogSearchLink = computed(() => {
  return `/auditLogs?sourceId=${props.username}`
})
</script>

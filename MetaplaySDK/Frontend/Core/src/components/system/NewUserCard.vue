<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
div(class="tw-container tw-mx-auto tw-h-dvh tw-space-y-3")
  MCard(
    title="No Role Configured"
    data-testid="new-user-card"
    )
    p(class="tw-mb-2") To use the Metaplay LiveOps Dashboard you need to have a role assigned to you. Please ask your administrator to set this up for you.
    p(class="tw-mb-4") Your administrator will ask for the following information:

    MList(showBorder)
      MListItem(class="tw-px-5") Name
        template(#top-right) {{ userDetails.name }}
      MListItem(class="tw-px-5") Email
        template(#top-right) {{ userDetails.email }}
      MListItem(class="tw-px-5") ID
        template(#top-right)
          span(class="text-truncate") {{ userDetails.id }}
      //- MListItem(class="tw-px-5") Roles
      //-   template(#top-right) {{ userDetails.userRoles.length }}
      //- MListItem(class="tw-px-5") Permissions
      //-   template(#top-right) {{ userDetails.userPermissions.length }}
      MListItem(class="tw-px-5") Environment
        template(#top-right)
          span(class="text-truncate") {{ staticInfos.environmentInfo.environmentName }}
      MListItem(class="tw-px-5") URL
        template(#top-right)
          span(class="text-truncate") {{ hostUrl }}

    template(#buttons)
      MClipboardCopy(
        :contents="detailsForClipboard"
        full-size
        ) Copy to Clipboard

  MCard(
    title="Log out"
    data-testid="log-out-card"
    )
    div Once your administrator has configured your role, you will need to log out and log back in for the changes to take effect.

    template(#buttons)
      MButton(@click="logout") Log Out
        template(#icon): fa-icon(
          icon="sign-out-alt"
          class="tw-mr-1 tw-h-3.5 tw-w-3"
          )

  //- An escape hatch for if the user has accidentally assumed a role without the dashboard.view permission.
  MCard(
    v-if="allowClearingAssumedRoles"
    title="Assumed Role Detected"
    variant="warning"
    )
    div It looks like you've assumed a role. You can use this button to clear the assumed roles and return to your original user.

    template(#buttons)
      MButton(@click="clearAssumedRoles") Clear Assumed Roles
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { logout, assumeRoles, useGameServerApiStore, useStaticInfos } from '@metaplay/game-server-api'
import { MCard, MList, MListItem, MButton, MClipboardCopy } from '@metaplay/meta-ui-next'

const gameServerApiStore = useGameServerApiStore()
const staticInfos = useStaticInfos()

const userDetails = computed(() => {
  // Return a list of user details except for the picture item, which just isn't
  // relevant here.
  const result: any = Object.fromEntries(
    Object.entries(gameServerApiStore.auth.userDetails).filter(([key]) => key !== 'picture')
  )

  // Add in details about the user's current roles and permissions.
  result.userRoles = gameServerApiStore.auth.userRoles
  result.userPermissions = gameServerApiStore.auth.userPermissions

  return result
})

const hostUrl = computed(() => {
  return window.location.origin
})

const detailsForClipboard = computed((): any => {
  return JSON.stringify({
    ...userDetails.value,
    environment: staticInfos.environmentInfo.environmentName,
    hostUrl: hostUrl.value,
  })
})

const allowClearingAssumedRoles = computed(() => {
  return gameServerApiStore.auth.userAssumedRoles.length > 0
})

const clearAssumedRoles = async (): Promise<void> => {
  await assumeRoles(null)
}
</script>

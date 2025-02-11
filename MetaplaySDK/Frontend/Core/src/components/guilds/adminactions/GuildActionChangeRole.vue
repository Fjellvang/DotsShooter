<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MActionModalButton(
  modal-title="Change a Guild Member's Role"
  :action="changeRole"
  trigger-button-label="Change a Role"
  :trigger-button-disabled-tooltip="triggerButtonDisabledReason"
  :ok-button-label="'Change Role'"
  :ok-button-disabled-tooltip="okButtonDisabledReason"
  permission="api.guilds.edit_roles"
  @show="resetModal"
  data-testid="action-change-role"
  )
  div(class="tw-mb-1 tw-font-semibold") 1. Select a Guild Member
  meta-input-select(
    :value="chosenPlayer"
    :options="playerList"
    placeholder="Select a player..."
    :searchFields="['displayName', 'playerId', 'roleId']"
    @input="chosenPlayer = $event"
    )
    template(#option="{ option }")
      MListItem(class="!tw-px-0 !tw-py-0")
        span(class="tw-text-xs+") {{ option?.displayName }} - {{ option?.roleId }}
        template(#top-right) {{ option?.playerId }}

  div(:class="['tw-font-semibold tw-my-3', { 'tw-text-neutral-400': !chosenPlayer }]") 2. Select a New Role
  meta-input-select(
    :value="chosenRole"
    :options="roleList"
    :placeholder="!chosenPlayer ? 'Select a guild member first' : 'Select a role...'"
    :searchFields="['displayName', 'id']"
    :disabled="!chosenPlayer"
    @input="chosenRole = $event"
    )
    template(#option="{ option }")
      div {{ option?.displayName }}

  div(
    :class="['tw-my-3 tw-font-semibold', { 'tw-text-neutral-400': !chosenPlayer || !chosenRole }]"
    class="tw-mb-1"
    ) 3. Preview Role Changes
  div(v-if="roleChangePreviewLoading")
    b-row(class="justify-content-center mt-5")
      b-spinner(
        label="Loading..."
        class="mt-4"
        )/

  div(
    v-else-if="!roleChangePreview"
    class="text-muted mb-3 small tw-pt-4 tw-text-center tw-italic"
    ) Choose a player and a role to preview the results.

  meta-alert(
    v-else-if="roleChangePreview.length == 0"
    variant="warning"
    title="No Changes to Roles"
    message="This change is no-op, invalid, or the resulting guild state would break the role invariants. Contact Metaplay if you think this is a bug!"
    )

  MList(
    v-else
    showBorder
    )
    MListItem(
      v-for="change in roleChangePreview"
      :key="change.displayName"
      class="tw-px-5"
      )
      span #[fa-icon(icon="user")] {{ change.displayName || 'n/a' }}
      template(#top-right): MTextButton(:to="`/players/${change.playerId}`") View player
      template(#bottom-left) Role will be changed from #[MBadge(:variant="getRoleVariant(change.oldRole)") {{ guildRoleDisplayString(change.oldRole) }}] to #[MBadge(:variant="getRoleVariant(change.newRole)") {{ guildRoleDisplayString(change.newRole) }}]
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { useGameServerApi } from '@metaplay/game-server-api'
import type { MetaInputSelectOption } from '@metaplay/meta-ui'
import { MActionModalButton, MBadge, MList, MListItem, MTextButton, useNotifications } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { guildRoleDisplayString } from '../../../coreUtils'
import { getSingleGuildSubscriptionOptions } from '../../../subscription_options/guilds'

const props = defineProps<{
  guildId: string
}>()

const gameServerApi = useGameServerApi()

/**
 * Subscribe to guild data.
 */
const { data: guildData, refresh: guildRefresh } = useSubscription(getSingleGuildSubscriptionOptions(props.guildId))

const hasMembers = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
  return Object.keys(guildData.value.model.members).length !== 0
})

interface PlayerInfo {
  playerId: string
  displayName: string
  roleId: string
}

interface RoleInfo {
  id: string
  displayName: string
}

/**
 * Response from server to a `validateEditRole` request.
 */
interface MemberValidateRoleEditResult {
  // Map of "playerId" to "new role".
  changes: Record<string, string>
}

/**
 * List of players to be displayed as options on the multiselect dropdown.
 */
const playerList = ref<Array<MetaInputSelectOption<PlayerInfo>>>([])

/**
 * The selected player.
 */
const chosenPlayer = ref<PlayerInfo>()

/**
 * List of roles to be displayed as options on the multiselect dropdown.
 */
const roleList = computed((): Array<MetaInputSelectOption<RoleInfo>> => {
  return ['Leader', 'MiddleTier', 'LowTier'].map((id): MetaInputSelectOption<RoleInfo> => {
    return {
      id,
      value: {
        id,
        displayName: guildRoleDisplayString(id),
      },
      disabled: chosenPlayer.value?.roleId === id,
    }
  })
})

/**
 * The selected role.
 */
const chosenRole = ref<RoleInfo>()

const roleChanges = ref<MemberValidateRoleEditResult>()
const roleChangePreview = ref()
const roleChangePreviewLoading = ref(false)

/**
 * Reason for disabling the OK button.
 */
const okButtonDisabledReason = computed(() => {
  if (playerList.value.length === 1) return 'This guild has only one member. You cannot change their role.'
  else if (!chosenPlayer.value) return 'Select a player to proceed.'
  else if (!chosenRole.value) return 'Select a role to proceed.'
  else if (!roleChanges.value) return 'Change is not valid.'
  else return undefined
})

/**
 * Reset the modal.
 */
function resetModal(): void {
  // Reset.
  chosenPlayer.value = undefined
  chosenRole.value = undefined
  roleChanges.value = undefined
  roleChangePreview.value = undefined

  // Update the available players list as the guild data changes and can't be hot-loaded.
  const newPlayerList: Array<MetaInputSelectOption<PlayerInfo>> = []
  for (const playerId in guildData.value.model.members) {
    const member = guildData.value.model.members[playerId]
    newPlayerList.push({
      id: playerId,
      value: {
        playerId,
        displayName: member.displayName,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        roleId: guildRoleDisplayString(member.role),
      },
    })
  }
  playerList.value = newPlayerList

  // Preselect the remaining member if only one left.
  if (playerList.value.length === 1) {
    chosenPlayer.value = playerList.value[0].value
  }
}

watch(chosenPlayer, validateEdit)
watch(chosenRole, validateEdit)

/**
 * Checks that the selected role change is valid for the selected player.
 * Returns the role change preview that is displayed.
 */
async function validateEdit(): Promise<void> {
  roleChangePreview.value = undefined
  roleChanges.value = undefined

  // If all fields are selected...
  if (chosenPlayer.value && chosenRole.value) {
    // Preview.
    roleChangePreviewLoading.value = true
    const reply = await gameServerApi.post(`/guilds/${guildData.value.id}/validateEditRole`, {
      playerId: chosenPlayer.value.playerId,
      role: chosenRole.value.id,
    })
    const changes = (reply.data as MemberValidateRoleEditResult).changes
    roleChangePreview.value = []
    Object.entries(changes).forEach((playerIdRole) => {
      const [playerId, newRole] = playerIdRole
      roleChangePreview.value.push({
        playerId,
        newRole,
        displayName: guildData.value.model.members[playerId].displayName || 'n/a',
        oldRole: guildData.value.model.members[playerId].role,
      })

      // Move selected player to the top.
      const i = roleChangePreview.value.findIndex((o: any) => o.playerId === chosenPlayer.value?.playerId)
      if (i > 0) {
        const p = roleChangePreview.value[i]
        roleChangePreview.value.splice(i, 1)
        roleChangePreview.value.unshift(p)
      }
    })
    if (Object.keys(changes).length > 0) {
      roleChanges.value = reply.data
    }
    roleChangePreviewLoading.value = false
  } else if (!chosenPlayer.value) {
    // Deselect role when deselecting player.
    chosenRole.value = undefined
  }
}

const { showSuccessNotification } = useNotifications()

/**
 * Update's the chosen player(s) new role(s) on the game server.
 * When the 'Leader' role is changed a new 'Leader' is nominated the affected players' roles will be updated.
 */
async function changeRole(): Promise<void> {
  await gameServerApi.post(`/guilds/${guildData.value.id}/editRole`, {
    playerId: chosenPlayer.value?.playerId,
    role: chosenRole.value?.id,
    expectedChanges: roleChanges.value?.changes,
  })
  showSuccessNotification(`${chosenPlayer.value?.displayName} is now a ${chosenRole.value?.displayName}.`)
  guildRefresh()
}

/**
 * Selects the color variant to use when rendering the role badge.
 * @param role Player's role in the guild.
 */
function getRoleVariant(role: string): 'primary' | 'neutral' {
  if (role === 'Leader') return 'primary'
  else return 'neutral'
}

/**
 * Disable the trigger button under certain conditions.
 */
const triggerButtonDisabledReason = computed(() => {
  if (guildData.value.model.lifecyclePhase === 'Closed') {
    return 'Guild is closed.'
  } else if (!hasMembers.value) {
    return 'Guild has no members.'
  } else {
    return undefined
  }
})
</script>

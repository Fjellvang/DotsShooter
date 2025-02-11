<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer
  template(#overview)
    b-card(
      class="shadow-sm mb-3"
      data-testid="user-overview-card"
      )
      b-row(
        align-h="center"
        class="mb-5"
        )
        b-avatar(
          :src="gameServerApiStore.auth.userDetails.picture"
          variant="light"
          size="7em"
          )

      MList(showBorder)
        MListItem Name
          template(#top-right) {{ gameServerApiStore.auth.userDetails.name }}
        MListItem Email
          template(#top-right) {{ gameServerApiStore.auth.userDetails.email }}
        MListItem ID
          template(#top-right) {{ gameServerApiStore.auth.userDetails.id }}
        MListItem {{ roleLabel }}
          template(#top-right)
            span(
              v-if="allUserRolesSorted.length > 0"
              class="text-right"
              )
              div(
                v-for="role in allUserRolesSorted"
                :key="role"
                ) {{ role }}
            span(v-else)
              span(class="text-warning") None 😢
        MListItem
          template(#default)
            div(
              style="padding-top: 0.1rem"
              class="font-weight-bold mr-2"
              ) #[fa-icon(:icon="uiStore.isSafetyLockOn ? 'lock' : 'lock-open'" style="width: 1rem")] Engage Safety Locks by Default
          template(#top-right)
            MInputSwitch(
              :model-value="uiStore.isSafetyLockOn"
              class="tw-relative tw-top-1.5"
              name="safetyToggleEnabled"
              @update:model-value="uiStore.toggleSafetyLock($event)"
              )
        MListItem Show Developer UI
          MBadge(
            tooltip="Adds advanced technical information onto most pages."
            shape="pill"
            class="tw-ml-1"
            ) ?
          template(#top-right)
            MInputSwitch(
              :model-value="uiStore.showDeveloperUi"
              :disabled="!permissions.doesHavePermission('dashboard.developer_mode')"
              class="tw-relative tw-top-1.5"
              name="isDeveloperUiShown"
              permission="dashboard.developer_mode"
              @update:model-value="uiStore.toggleDeveloperUi($event)"
              )
  b-row(align-h="center")
    b-col(lg="6")
      meta-list-card(
        title="Permissions"
        description="These are the combined permissions granted by all your roles."
        emptyMessage="You have no permissions on this user account!"
        :searchFields="['permissionDetails.group', 'permissionDetails.name', 'permissionDetails.description']"
        :itemList="decoratedPermissions"
        :getItemKey="getPermissionsItemKey"
        :sortOptions="permissionListSortOptions"
        :defaultSortOption="1"
        :filterSets="permissionListFilterSets"
        data-testid="user-permissions-card"
        )
        template(#item-card="slot")
          MListItem
            MBadge(:variant="slot.item.hasPermission ? 'success' : 'neutral'") {{ slot.item.permissionDetails.name }}
            template(#bottom-left)
              div(
                v-if="!slot.item.hasPermission"
                class="tw-italic"
                ) You do not have this permission.
              div {{ slot.item.permissionDetails.description }}
            template(#top-right) {{ slot.item.permissionDetails.group }}

    b-col(lg="6")
      b-card(
        :style="canAssumeRoles ? '' : 'bg-light'"
        class="shadow-sm mb-3"
        data-testid="assume-role-card"
        )
        b-card-title Assume Roles
        div(
          v-if="canAssumeRoles"
          class="tw-mb-4"
          )
          div(class="small mb-3") This is an advanced developer feature for quickly testing different roles.

          MInputMultiSelectCheckbox(
            :model-value="rolesToAssume"
            :options="allRolesSorted"
            vertical
            @update:model-value="rolesToAssume = $event"
            )

        div(
          v-else
          class="text-muted mt-4 tw-text-center tw-italic"
          ) Assuming roles is disabled in this environment.

  meta-raw-data(
    :kvPair="gameServerApiStore.auth.userDetails"
    name="userDetails"
    )
  meta-raw-data(
    :kvPair="staticInfos.liveOpsDashboardInfo.authConfig"
    name="authConfig"
    )
</template>

<script lang="ts" setup>
import { computed, ref, watch } from 'vue'

import { assumeRoles, useGameServerApiStore, useStaticInfos } from '@metaplay/game-server-api'
import type { PermissionDetails } from '@metaplay/game-server-api'
import {
  useUiStore,
  MetaListFilterOption,
  MetaListFilterSet,
  MetaListSortDirection,
  MetaListSortOption,
} from '@metaplay/meta-ui'
import {
  MBadge,
  MList,
  MListItem,
  MInputSwitch,
  MInputMultiSelectCheckbox,
  usePermissions,
  MViewContainer,
} from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'

interface DecoratedPermission {
  permissionDetails: PermissionDetails
  hasPermission: boolean
}

const staticInfos = useStaticInfos()
const uiStore = useUiStore()
const gameServerApiStore = useGameServerApiStore()
const permissions = usePermissions()

// Take all possible permissions and decorate them with extra info.
const decoratedPermissions = computed<DecoratedPermission[]>(() => {
  return gameServerApiStore.auth.serverPermissions.map((permissionDetails) => {
    return {
      permissionDetails,
      hasPermission: permissions.doesHavePermission(permissionDetails.name),
    }
  })
})

// MetaListCard sort options for the permissions list.
const permissionListSortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Name', 'permissionDetails.name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'permissionDetails.name', MetaListSortDirection.Descending),
  new MetaListSortOption('Group', 'permissionDetails.group', MetaListSortDirection.Ascending),
  new MetaListSortOption('Group', 'permissionDetails.group', MetaListSortDirection.Descending),
  new MetaListSortOption('Type', 'permissionDetails.type', MetaListSortDirection.Ascending),
  new MetaListSortOption('Type', 'permissionDetails.type', MetaListSortDirection.Descending),
]

// MetaListCard filter sets for the permissions list.
const permissionListFilterSets = computed(() => {
  return [
    new MetaListFilterSet('hasPermission', [
      new MetaListFilterOption('Has permission', (x) => (x as DecoratedPermission).hasPermission, true),
      new MetaListFilterOption('Does not have permission', (x) => !(x as DecoratedPermission).hasPermission),
    ]),
    new MetaListFilterSet(
      'group',
      decoratedPermissions.value // Start with all possible permissions.
        .map((x) => x.permissionDetails.group) // Get the group of each permission.
        .filter((value, index, self) => self.indexOf(value) === index) // Filter to get unique values.
        .map(
          (group) =>
            new MetaListFilterOption(group, (y) => (y as DecoratedPermission).permissionDetails.group === group)
        ) // Create option.
    ),
    new MetaListFilterSet(
      'type',
      decoratedPermissions.value // Start with all possible permissions.
        .map((x) => x.permissionDetails.type) // Get the type of each permission.
        .filter((value, index, self) => self.indexOf(value) === index) // Filter to get unique values.
        .map(
          (type) => new MetaListFilterOption(type, (y) => (y as DecoratedPermission).permissionDetails.type === type)
        ) // Create option.
    ),
  ]
})

function getPermissionsItemKey(permissionItem: any): string {
  return (permissionItem as DecoratedPermission).permissionDetails.name
}

// Information about user roles.
const allRolesSorted = [...gameServerApiStore.auth.serverRoles].sort().map((role) => ({ label: role, value: role }))
const allUserRolesSorted = computed(() => [...gameServerApiStore.auth.userRoles].sort())

// The role label is complex. It needs to show single and plural role counts, and needs to consider whether the
// user has assumed roles or not.
const roleLabel = computed(() => {
  const baseLabel = gameServerApiStore.auth.userAssumedRoles.length > 0 ? 'Assumed Role' : 'Role'
  return maybePluralString(allUserRolesSorted.value.length, baseLabel, false)
})

// Assumed roles.
const canAssumeRoles = gameServerApiStore.auth.canAssumeRoles
const rolesToAssume = ref(gameServerApiStore.auth.userAssumedRoles)
watch(rolesToAssume, async (newVal) => {
  await assumeRoles(newVal)
})
</script>

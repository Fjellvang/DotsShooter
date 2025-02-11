<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MRootLayout(
  :headerBadgeLabel="gameServerApiStore.auth.userDetails.name"
  :headerAvatarImageUrl="gameServerApiStore.auth.userDetails.picture"
  :headerBackgroundColorString="dashboardOptionsData?.dashboardHeaderColorInHex ?? undefined"
  @headerAvatarClick="router.push('/user')"
  )
  template(#header-right)
    UtcClock(
      :backgroundColorString="dashboardOptionsData?.dashboardHeaderColorInHex ?? undefined"
      class="tw-hidden sm:tw-inline"
      )

  template(#sidebar-header)
    //- A masking element for the monogram image.
    div(class="tw-rounded-full")
      //- Monogram.
      router-link(
        to="/"
        data-testid="overview-link"
        )
        img(
          :src="coreStore.gameSpecific.gameIconUrl"
          width="45"
          height="45"
          class="tw-cursor-pointer tw-rounded-lg tw-filter hover:tw-brightness-90 active:tw-brightness-75"
          role="button"
          )

    //- Project name.
    div
      div(
        role="heading"
        class="tw-text-xl tw-font-semibold"
        ) {{ staticInfos.projectInfo.projectName }}

      //- Quick links.
      MPopover(
        v-if="quickLinksDropdownEnabled"
        :triggerLabel="staticInfos.environmentInfo.environmentName"
        title="Quick Links"
        size="small"
        data-testid="quick-links"
        )
        //- Quick links content.
        MList
          div(
            v-for="(quickLink, index) in quickLinksData"
            :key="quickLink.uri"
            :tabindex="index"
            role="link"
            class="tw-flex tw-cursor-pointer tw-items-center tw-space-x-2 tw-px-4 tw-py-3 -tw-outline-offset-2 hover:tw-bg-neutral-200 active:tw-bg-neutral-300"
            @click="openQuickLink(quickLink.uri)"
            :data-testid="'quick-link-' + index"
            )
            img(
              v-if="quickLink.icon"
              :src="quickLink.icon === '@game-icon' ? coreStore.gameSpecific.gameIconUrl : quickLink.icon"
              class="tw-h-8 tw-w-8 tw-rounded-lg"
              )
            div(class="tw-flex tw-grow tw-items-baseline tw-justify-between tw-space-y-1.5")
              span {{ quickLink.title }}
              fa-icon(
                icon="external-link-alt"
                size="sm"
                class="tw-relative tw-ml-2 tw-text-neutral-500"
                style="bottom: -1px"
                )

      //- Quick links disabled state.
      div(
        v-else
        class="tw-text-sm"
        ) {{ staticInfos.environmentInfo.environmentName }}

  template(#sidebar="{ closeSidebarOnNarrowScreens }")
    //- Top.
    div(data-testid="sidebar")
      //- Sidebar links.
      div(
        v-for="category in sortedCategories"
        :key="category"
        class="tw-mb-4"
        )
        MSidebarSection(:title="category")
          RouterLink(
            v-for="route in sidebarRoutes[category]"
            :to="permissions.doesHavePermission(getRouteOptions(route).permission) ? route.path : ''"
            class="tw-block hover:tw-no-underline"
            )
            MSidebarLink(
              :label="getRouteOptions(route)?.sidebarTitle || 'Title TBD'"
              :permission="getRouteOptions(route)?.permission"
              :active-path-fragment="route.path"
              :secondaryPathHighlights="getRouteOptions(route)?.secondaryPathHighlights || []"
              @click="closeSidebarOnNarrowScreens"
              )
              template(
                v-if="getRouteOptions(route)?.icon"
                #icon
                )
                FontAwesomeIcon(
                  :icon="getRouteOptions(route).icon ?? ''"
                  fixed-width
                  )

      //- List debug roles if needed.
      div(
        v-if="gameServerApiStore.auth.userAssumedRoles.length > 0"
        class="tw-mb-3 tw-px-4"
        )
        div(
          class="tw-mb-2 tw-font-bold"
          role="heading"
          )
          meta-plural-label(
            :value="gameServerApiStore.auth.userAssumedRoles.length"
            label="Assumed Role"
            hide-count
            )
        MBadge(
          v-for="role in gameServerApiStore.auth.userAssumedRoles"
          :key="role"
          class="tw-mr-1"
          variant="warning"
          ) {{ role }}

      //- Logout.
      MTooltip(
        :content="!gameServerApiStore.auth.canLogout ? 'Cannot log out when authentication is disabled.' : undefined"
        no-underline
        )
        MSidebarLink(
          label="Log Out"
          icon="sign-out-alt"
          :disabled="!gameServerApiStore.auth.canLogout"
          class="tw-select-none"
          :class="{ 'tw-cursor-pointer': gameServerApiStore.auth.canLogout, 'tw-cursor-not-allowed': !gameServerApiStore.auth.canLogout }"
          @click="logout"
          )
          template(#icon)
            FontAwesomeIcon(
              :icon="'sign-out-alt'"
              fixed-width
              )

  //- Alerts.
  header-alerts

  //- router-view element will contain the currently active view as controlled by the Vue router.
  //- Note that we use `path` as the key here, not `fullPath`. This is because `path` omits the query string (and also
  //- the anchor) which means that the page will not refresh when the query string changes. This is important for
  //- `MTabLayout` so that changing tabs doesn't cause the page to refresh.
  router-view(
    :key="route.path"
    role="main"
    )
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { RouteRecordNormalized } from 'vue-router'

import { logout, useGameServerApiStore, useStaticInfos } from '@metaplay/game-server-api'
import {
  MRootLayout,
  MSidebarSection,
  MSidebarLink,
  MPopover,
  MList,
  MBadge,
  MTooltip,
  usePermissions,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

import { useCoreStore } from '../coreStore'
import type { NavigationEntryOptions } from '../integration_api/integrationApi'
import {
  getDashboardOptionsSubscriptionOptions,
  getQuickLinksSubscriptionOptions,
} from '../subscription_options/general'
import HeaderAlerts from './navigation/HeaderAlerts.vue'
import UtcClock from './system/UtcClock.vue'

const route = useRoute()
const router = useRouter()
const coreStore = useCoreStore()
const gameServerApiStore = useGameServerApiStore()
const permissions = usePermissions()

const { data: dashboardOptionsData } = useSubscription(getDashboardOptionsSubscriptionOptions())
const staticInfos = useStaticInfos()

const sidebarRoutes = computed(() => {
  const routes: Record<string, RouteRecordNormalized[]> = {}
  // Build an object of routes grouped by category
  for (const route of router.getRoutes()) {
    if (typeof route.meta.category === 'string') {
      if (routes[route.meta.category]) routes[route.meta.category].push(route)
      else routes[route.meta.category] = [route]
    }
  }
  // Sort the routes by sidebarOrder
  for (const category of Object.keys(routes)) {
    routes[category].sort((a, b) => {
      if (typeof a.meta.sidebarOrder === 'number' && typeof b.meta.sidebarOrder === 'number') {
        return a.meta.sidebarOrder - b.meta.sidebarOrder
      }
      if (a.meta.sidebarOrder) return -1
      if (b.meta.sidebarOrder) return 1
      return 0
    })
  }

  return routes
})

const sortedCategories = computed(() => {
  return Object.keys(sidebarRoutes.value).sort()
})

function getRouteOptions(route: RouteRecordNormalized): NavigationEntryOptions {
  return route.meta as NavigationEntryOptions
}

const { data: quickLinksData, hasPermission: quickLinksHasPermission } = useSubscription<
  Array<{ icon?: string; title: string; uri: string }>
>(getQuickLinksSubscriptionOptions())

/**
 * Should the environment links dropdown by clickable or not?
 */
const quickLinksDropdownEnabled = computed(() => {
  if (quickLinksData.value) {
    return quickLinksHasPermission.value && quickLinksData.value.length > 0
  }
  return false
})

function openQuickLink(uri: string): void {
  window.open(uri, '_blank')
}
</script>

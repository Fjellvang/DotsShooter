<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  v-if="playerData"
  title="Latest Logins"
  icon="clipboard-list"
  tooltip="Shows 20 latest logins and their source."
  :itemList="decoratedItems"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  emptyMessage="No logins yet!"
  data-testid="player-login-history-card"
  )
  template(#item-card="{ item: login }")
    MListItem #[meta-country-code(isoCode="slotProps.item.location ? slotProps.item.location.country.isoCode : null")] {{ login.deviceModel }}
      template(#badge)
        span(class="tw-mt-0.5 tw-text-xs+ tw-text-neutral-500") (client {{ login.clientVersion }})
      template(#top-right)
        meta-time(
          :date="login.timestamp"
          showAs="timeagoSentenceCase"
          )

      template(#bottom-left)
        div
          //- TODO: Rename 'DeviceId' to 'Client token' in the API.
          span(v-if="login.authenticationKey") Authentication: {{ login.authenticationKey.platform === 'DeviceId' ? 'Client token' : login.authenticationKey.platform }}
          span(v-if="login.ipAddress") | IP: #[meta-ip-address(:ip-address="login.ipAddress")]
        div OS: {{ login.operatingSystem ?? 'Unknown' }}

      template(#bottom-right)
        div(v-if="login.sessionLengthApprox") Session length: #[meta-duration(:duration="parseDotnetTimeSpanToLuxon(login.sessionLengthApprox)" showAs="exactDuration" hideMilliseconds)]
        div(v-else) Session length: Unknown
        MTextButton(
          permission="dashboard.grafana.view"
          :disabled-tooltip="!grafanaEnabled ? 'Grafana is not configured for this environment' : undefined"
          :to="getGrafanaLogsUrl(login)"
          ) #[fa-icon(icon="external-link-alt" size="sm" class="tw-mr-1")] View logs
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { useStaticInfos } from '@metaplay/game-server-api'
import {
  MetaListSortOption,
  MetaListSortDirection,
  MetaListFilterSet,
  isoCodeToCountryName,
  isoCodeToCountryFlag,
} from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import { parseDotnetTimeSpanToLuxon } from '../../coreUtils'
import { getSinglePlayerSubscriptionOptions } from '../../subscription_options/players'

const props = defineProps<{
  /**
   * ID of the player whose login history to show.
   */
  playerId: string
}>()

const { data: playerData } = useSubscription(getSinglePlayerSubscriptionOptions(props.playerId))

/**
 * Typings for the login history items returned by the game server as of R24.
 */
interface PlayerLoginEvent {
  timestamp: string
  deviceId: string
  deviceModel: string
  clientPlatform: string
  clientVersion: string
  location: {
    country: {
      isoCode: string
    }
  } | null
  authenticationKey: {
    platform: string
    id: string
  } | null // Cannot be null since early 2022, but very old players may still have null.
  operatingSystem: string | null
  sessionLengthApprox: string | null
}

/**
 * Login history items with locally resolved country name and flag.
 */
interface DecoratedPlayerLoginEvent extends PlayerLoginEvent {
  countryName: string
  countryFlag?: string
  ipAddress?: string // TODO: Add to backend response to resolve a customer diff.
}

/**
 * Login history items with added country name and flag.
 * TODO: Should be done in the backend.
 */
const decoratedItems = computed((): DecoratedPlayerLoginEvent[] => {
  return playerData.value.model.loginHistory.map((login: PlayerLoginEvent) => {
    return {
      ...login,
      countryName: login.location ? isoCodeToCountryName(login.location.country.isoCode) : 'Unknown',
      countryFlag: login.location ? isoCodeToCountryFlag(login.location.country.isoCode) : undefined,
    }
  })
})

const staticInfos = useStaticInfos()

const grafanaEnabled = computed(() => !!staticInfos.environmentInfo.grafanaUri)

/**
 * Returns a URL to the Grafana logs for the given login event.
 */
function getGrafanaLogsUrl(ev: any): string | undefined {
  if (staticInfos.environmentInfo.grafanaUri) {
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    const startAt = new Date(ev.timestamp)
    const endAt = new Date(+startAt + 15 * 60_000) // Assume 15min sessions.
    const namespaceStr = staticInfos.environmentInfo.kubernetesNamespace
      ? `,namespace=\\"${staticInfos.environmentInfo.kubernetesNamespace}\\"`
      : ''
    return `${staticInfos.environmentInfo.grafanaUri}/explore?orgId=1&left={"datasource": "Loki", "queries":[{"expr":"{app=\\"metaplay-server\\"${namespaceStr}} |= \\"${props.playerId}\\""}],"range":{"from":"${+startAt - 3000}","to":"${+endAt + 3000}"}}`
  } else {
    return undefined
  }
}

// Searching, sorting & filtering -------------------------------------------------------------------------------------

const searchFields = ['deviceModel', 'countryName', 'countryFlag']

const sortOptions = [
  new MetaListSortOption('Time', 'timestamp', MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', 'timestamp', MetaListSortDirection.Descending),
]

const defaultSortOption = 1

const filterSets = computed(() => {
  return [
    MetaListFilterSet.asDynamicFilterSet(decoratedItems.value, 'device-model', (x: any) => x.deviceModel),
    MetaListFilterSet.asDynamicFilterSet(decoratedItems.value, 'country-name', (x: any) => x.countryName),
  ]
})
</script>

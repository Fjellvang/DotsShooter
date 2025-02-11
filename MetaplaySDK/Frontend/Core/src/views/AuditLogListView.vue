<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(permission="api.audit_logs.search")
  template(#overview)
    MPageOverviewCard(
      title="Audit Log Events"
      data-testid="audit-log-list-overview-search-card"
      )
      p(class="tw-mb-4") Audit log events are a record of all actions taken by users and the system. You can use them to see who has done what and when.

      div(class="tw-mb-2")
        h6 Search
        div(class="tw-flex tw-gap-2")
          span(class="tw-basis-1/2")
            label(:class="['tw-block tw-text-sm tw-font-bold tw-leading-6 tw-mb-1 tw-text-neutral-900']") Type
            meta-input-select(
              :value="searchAuditLogEventsTargetType"
              :options="targetTypes"
              placeholder="Select type"
              @input="searchAuditLogEventsTargetType = $event"
              )
          MInputText(
            label="ID"
            :model-value="searchAuditLogEventsTargetId"
            :disabled="!searchAuditLogEventsTargetType"
            :variant="searchAuditLogEventIdUiState !== null ? (searchAuditLogEventIdUiState ? 'success' : 'danger') : 'default'"
            :hint-message="!searchAuditLogEventsTargetType ? 'Select a type first.' : undefined"
            placeholder="000000000R"
            class="tw-grow"
            show-clear-button
            @blur:model-value="searchAuditLogEventsTargetId = $event"
            )

      div
        div(class="tw-flex tw-flex-wrap tw-gap-2")
          MInputText(
            label="Account"
            :model-value="searchAuditLogEventsSourceId"
            :variant="searchAuditLogEventUserAccountUiState !== null ? (searchAuditLogEventUserAccountUiState ? 'success' : 'danger') : 'default'"
            placeholder="john.doe@example.org"
            class="tw-basis-full sm:tw-basis-1/2"
            show-clear-button
            @blur:model-value="searchAuditLogEventsSourceId = $event"
            )
          MInputText(
            label="IP Address"
            :model-value="searchAuditLogEventsSourceIpAddress"
            :variant="searchAuditLogEventUserIpAddressUiState !== null ? (searchAuditLogEventUserIpAddressUiState ? 'success' : 'danger') : 'default'"
            placeholder="192.168.0.1"
            class="tw-grow"
            show-clear-button
            @blur:model-value="searchAuditLogEventsSourceIpAddress = $event"
            )
          MInputText(
            label="Country Code"
            :model-value="searchAuditLogEventsSourceCountryIsoCode"
            :variant="searchAuditLogEventUserCountryIsoCodeUiState !== null ? (searchAuditLogEventUserCountryIsoCodeUiState ? 'success' : 'danger') : 'default'"
            :hint-message="searchAuditLogEventUserCountryIsoCodeUiState === false ? 'Invalid ISO code.' : undefined"
            placeholder="FI"
            class="tw-basis-full sm:tw-basis-24"
            show-clear-button
            @blur:model-value="searchAuditLogEventsSourceCountryIsoCode = $event"
            )

  template(#default)
    div(v-if="showSearchResults")
      b-col(class="tw-mb-4")
        b-card(
          title="Search Results"
          class="shadow-sm"
          data-testid="audit-log-list-search-results-card"
          )
          b-row(
            v-if="searchEvents.length > 0"
            no-gutters
            )
            b-table(
              small
              striped
              hover
              responsive
              :items="searchEvents"
              :fields="tableFields"
              primary-key="eventId"
              sort-by="startAt"
              sort-desc
              tbody-tr-class="table-row-link"
              @row-clicked="clickOnEventRow"
              )
              template(#cell(target)="data")
                span(class="text-nowrap") {{ data.item.target.targetType.replace(/\$/, '') }}:{{ data.item.target.targetId }}

              template(#cell(displayTitle)="data")
                MTooltip(
                  :content="data.item.displayDescription"
                  noUnderline
                  class="text-nowrap"
                  ) {{ data.item.displayTitle }}

              template(#cell(source)="data")
                span(class="text-nowrap") #[meta-username(:username="data.item.source.sourceId" render-as="text")]

              template(#cell(createdAt)="data")
                meta-time(
                  :date="data.item.createdAt"
                  class="text-nowrap"
                  )

            div(class="tw-mt-2 tw-flex tw-w-full tw-justify-center")
              MButton(
                v-if="searchEventsHasMore"
                size="small"
                @click="showMoreSearch"
                ) Load More

          b-row(
            v-else
            no-gutters
            align-h="center"
            class="mt-4 mb-3"
            )
            p(class="m-0 text-muted") No search results.

      meta-raw-data(
        :kvPair="searchEvents"
        name="Search results"
        )

    div(v-else)
      b-col(class="tw-mb-4")
        b-card(
          title="Latest Audit Log Events"
          class="shadow-sm"
          data-testid="audit-log-list-latest-events-card"
          )
          b-row(
            v-if="latestEvents.length > 0"
            no-gutters
            )
            b-table(
              small
              striped
              hover
              responsive
              :items="latestEvents"
              :fields="tableFields"
              primary-key="eventId"
              sort-by="startAt"
              sort-desc
              tbody-tr-class="table-row-link"
              class="table-fixed-column"
              @row-clicked="clickOnEventRow"
              )
              template(#cell(target)="data")
                span(class="text-nowrap") {{ data.item.target.targetType.replace(/\$/, '') }}:{{ data.item.target.targetId }}

              template(#cell(displayTitle)="data")
                MTooltip(
                  :content="data.item.displayDescription"
                  noUnderline
                  class="text-nowrap"
                  ) {{ data.item.displayTitle }}

              template(#cell(source)="data")
                span(class="text-nowrap") #[meta-username(:username="data.item.source.sourceId" render-as="text")]

              template(#cell(createdAt)="data")
                meta-time(
                  :date="data.item.createdAt"
                  class="text-nowrap"
                  )

            div(class="tw-mt-2 tw-flex tw-w-full tw-justify-center")
              MButton(
                v-if="latestEventsHasMore"
                size="small"
                @click="showMoreLatest"
                ) Load More

          b-row(
            v-else
            no-gutters
            align-h="center"
            class="mt-4 mb-3"
            )
            p(class="m-0 text-muted") No events.

      meta-raw-data(
        :kvPair="latestEvents"
        name="Latest events"
        )
</template>

<script lang="ts" setup>
import { computed, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useStaticInfos } from '@metaplay/game-server-api'
import { isoCodeToCountryName } from '@metaplay/meta-ui'
import { MButton, MInputText, MPageOverviewCard, MTooltip, MViewContainer } from '@metaplay/meta-ui-next'
import { useManuallyManagedStaticSubscription } from '@metaplay/subscriptions'

import { extractSingleValueFromQueryStringOrDefault } from '../coreUtils'
import {
  getAllAuditLogEventsSubscriptionOptions,
  getAuditLogEventsSearchSubscriptionOptions,
} from '../subscription_options/auditLogs'

const staticInfos = useStaticInfos()
const route = useRoute()
const router = useRouter()

const pageSize = 10

// The "search" results list ------------------------------------------------------------------------------------------

const searchAuditLogEventsTargetType = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'targetType', ''))
const searchAuditLogEventsTargetId = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'targetId', ''))
const searchAuditLogEventsSourceId = ref(extractSingleValueFromQueryStringOrDefault(route.query, 'sourceId', ''))
const searchAuditLogEventsSourceIpAddress = ref(
  extractSingleValueFromQueryStringOrDefault(route.query, 'sourceIpAddress', '')
)
const searchAuditLogEventsSourceCountryIsoCode = ref(
  extractSingleValueFromQueryStringOrDefault(route.query, 'sourceCountryIsoCode', '')
)

const targetTypes = computed((): Array<{ id: string; value: string }> => {
  const targetTypes = [
    {
      value: 'Player',
      id: 'Player',
    },
    {
      value: '$GameServer',
      id: 'GameServer',
    },
    {
      value: '$GameConfig',
      id: 'GameConfig',
    },
    {
      value: '$Broadcast',
      id: 'Broadcast',
    },
    {
      value: '$Notification',
      id: 'Notification',
    },
    {
      value: '$Experiment',
      id: 'Experiment',
    },
    {
      value: 'AsyncMatchmaker',
      id: 'AsyncMatchmaker',
    },
  ]

  // Add types for features behind feature flags
  if (staticInfos.featureFlags.guilds) {
    targetTypes.push({
      value: 'Guild',
      id: 'Guild',
    })
  }
  if (staticInfos.featureFlags.web3) {
    targetTypes.push(
      {
        value: '$Nft',
        id: 'NFT',
      },
      {
        value: '$NftCollection',
        id: 'NFT Collection',
      }
    )
  }
  if (staticInfos.featureFlags.playerLeagues) {
    targetTypes.push({
      value: 'LeagueManager',
      id: 'League',
    })
  }
  if (staticInfos.featureFlags.localization) {
    targetTypes.push({
      value: '$Localization',
      id: 'Localization',
    })
  }
  if (staticInfos.featureFlags.liveOpsEvents) {
    targetTypes.push({
      value: '$LiveOpsEventOccurrence',
      id: 'LiveOps Event',
    })
    targetTypes.push({
      value: '$LiveOpsTimelineNode',
      id: 'LiveOps Timeline Node',
    })
  }

  return targetTypes
})

/**
 * Subscription details for search events.
 */
const searchEventsSubscription = ref()

/**
 * Current fetch size for search events subscription.
 */
const searchEventsLimit = ref(pageSize)

/**
 * Is there a search in progress?
 */
const showSearchResults = computed(() => {
  return (
    searchAuditLogEventTypeUiState.value ??
    searchAuditLogEventIdUiState.value ??
    searchAuditLogEventUserAccountUiState.value ??
    searchAuditLogEventUserIpAddressUiState.value ??
    searchAuditLogEventUserCountryIsoCodeUiState.value !== null
  )
})

/**
 * Search events.
 */
const searchEvents = computed((): any[] => {
  return searchEventsSubscription.value?.data?.entries || []
})

/**
 * Are there more search events to fetch?
 */
const searchEventsHasMore = computed(() => {
  return !!searchEventsSubscription.value?.data?.hasMore
})

/**
 * Increase fetch size.
 */
function showMoreSearch(): void {
  searchEventsLimit.value += pageSize
  subscribeShowSearch()
}

/**
 * Set up new subscription for search events.
 */
function subscribeShowSearch(): void {
  if (searchEventsSubscription.value) {
    searchEventsSubscription.value.unsubscribe()
  }

  searchEventsSubscription.value = useManuallyManagedStaticSubscription(
    getAuditLogEventsSearchSubscriptionOptions({
      targetType: searchAuditLogEventsTargetType.value,
      targetId: searchAuditLogEventsTargetType.value ? searchAuditLogEventsTargetId.value : undefined,
      sourceId: searchAuditLogEventsSourceId.value ? '$AdminApi:' + searchAuditLogEventsSourceId.value : '',
      sourceIpAddress: searchAuditLogEventsSourceIpAddress.value,
      sourceCountryIsoCode: searchAuditLogEventsSourceCountryIsoCode.value,
      limit: searchEventsLimit.value,
    })
  )
}

/**
 * Kick off the initial subscription.
 */
subscribeShowSearch()

/**
 * Remember to unsubscribe when page unmounts.
 */
onUnmounted(() => {
  searchEventsSubscription.value.unsubscribe()
})

// If any search parameter updates...
watch(
  [
    searchAuditLogEventsTargetType,
    searchAuditLogEventsTargetId,
    searchAuditLogEventsSourceId,
    searchAuditLogEventsSourceIpAddress,
    searchAuditLogEventsSourceCountryIsoCode,
    searchEventsLimit,
  ],
  async () => {
    // Update the URL with the new search parameters.
    const params: Record<string, string> = {}

    if (searchAuditLogEventsTargetType.value) {
      params.targetType = searchAuditLogEventsTargetType.value
      if (searchAuditLogEventsTargetId.value) {
        params.targetId = searchAuditLogEventsTargetId.value
      }
    }
    if (searchAuditLogEventsSourceId.value) {
      params.sourceId = searchAuditLogEventsSourceId.value
    }
    if (searchAuditLogEventsSourceIpAddress.value) {
      params.sourceIpAddress = searchAuditLogEventsSourceIpAddress.value
    }
    if (upperCasedSearchAuditLogEventsSourceCountryIsoCode.value) {
      params.sourceCountryIsoCode = upperCasedSearchAuditLogEventsSourceCountryIsoCode.value
    }

    // Update the query string in the URL.
    await router.replace({ path: '/auditLogs', query: params })

    // Update the subscription to refresh the results.
    subscribeShowSearch()
  }
)

const upperCasedSearchAuditLogEventsSourceCountryIsoCode = computed(() => {
  return searchAuditLogEventsSourceCountryIsoCode.value
    ? searchAuditLogEventsSourceCountryIsoCode.value.toUpperCase()
    : null
})

/* UI state for the Event Type form component. */
const searchAuditLogEventTypeUiState = computed(() => {
  return searchAuditLogEventsTargetType.value ? true : null
})

/* UI state for the Event Id form component. */
const searchAuditLogEventIdUiState = computed(() => {
  return searchAuditLogEventsTargetId.value ? true : null
})

/* UI state for the User Account form component. */
const searchAuditLogEventUserAccountUiState = computed(() => {
  return searchAuditLogEventsSourceId.value ? true : null
})

/* UI state for the User IP Address form component. */
const searchAuditLogEventUserIpAddressUiState = computed(() => {
  return searchAuditLogEventsSourceIpAddress.value ? true : null
})

/* UI state for the User Country ISO Code form component. */
const searchAuditLogEventUserCountryIsoCodeUiState = computed(() => {
  const isoCode = upperCasedSearchAuditLogEventsSourceCountryIsoCode.value
  if (isoCode) {
    return isoCodeToCountryName(isoCode) !== isoCode
  } else {
    return null
  }
})

// The "latest" events list -------------------------------------------------------------------------------------------

/**
 * Subscription details for latest events.
 */
const latestEventsSubscription = ref()

/**
 * Current fetch size for latest events subscription.
 */
const latestEventsLimit = ref(pageSize)

/**
 * Latest events.
 */
const latestEvents = computed((): any[] => {
  return latestEventsSubscription.value?.data?.entries || []
})

/**
 * Are there more latest events to fetch?
 */
const latestEventsHasMore = computed(() => {
  return !!latestEventsSubscription.value?.data?.hasMore
})

/**
 * Increase fetch size.
 */
function showMoreLatest(): void {
  latestEventsLimit.value += pageSize
  subscribeShowLatest()
}

/**
 * Set up new subscription for latest events.
 */
function subscribeShowLatest(): void {
  if (latestEventsSubscription.value) {
    latestEventsSubscription.value.unsubscribe()
  }
  latestEventsSubscription.value = useManuallyManagedStaticSubscription(
    getAllAuditLogEventsSubscriptionOptions('', '', latestEventsLimit.value)
  )
}

/**
 * Kick off the initial subscription.
 */
subscribeShowLatest()

/**
 * Remember to unsubscribe when page unmounts.
 */
onUnmounted(() => {
  latestEventsSubscription.value.unsubscribe()
})

// Other --------------------------------------------------------------------------------------------------------------

const tableFields = [
  {
    key: 'target',
    label: 'Event',
  },
  {
    key: 'displayTitle',
    label: 'Title',
  },
  {
    key: 'source',
    label: 'User',
  },
  {
    key: 'createdAt',
    label: 'Date',
  },
]

async function clickOnEventRow(item: any): Promise<void> {
  await router.push(`/auditLogs/${item.eventId}`)
}
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  permission="api.audit_logs.view"
  :is-loading="!logEventData"
  :error="logEventError"
  )
  template(#overview)
    MPageOverviewCard(
      v-if="logEventData"
      :title="`Event: ${logEventData.displayTitle}`"
      data-testid="audit-log-detail-overview-card"
      )
      template(#subtitle)
        div ID: {{ route.params.id }} #[MClipboardCopy(:contents="`${route.params.id}`")]
        div(class="tw-text-black") {{ logEventData.displayDescription }}
        div By #[meta-username(:username="logEventData.source.sourceId")] #[meta-time(:date="logEventData.createdAt")].
      template(#buttons)
        MButton(
          v-if="targetLink"
          :to="targetLink"
          permission="api.players.view"
          ) View Target {{ humanReadableTargetType }}

      h6 Event Data
      b-table-simple(
        small
        responsive
        )
        b-tbody
          b-tr
            b-td Target Type
            b-td(class="tw-text-right")
              MTextButton(
                :to="`/auditLogs/?targetType=${logEventData.target.targetType}`"
                permission="api.audit_logs.search"
                ) {{ humanReadableTargetType }}
          b-tr
            b-td Target ID
            b-td(class="tw-text-right")
              MTextButton(
                :to="`/auditLogs/?targetType=${logEventData.target.targetType}&targetId=${logEventData.target.targetId}`"
                permission="api.audit_logs.search"
                ) {{ humanReadableTargetType }}:{{ logEventData.target.targetId }}
          b-tr
            b-td Event Date
            b-td(class="tw-text-right") #[meta-time(:date="logEventData.createdAt" showAs="datetime")]
          b-tr
            b-td User IP Address
            b-td(
              v-if="logEventData.sourceIpAddress"
              class="tw-text-right"
              )
              MTextButton(
                :to="`/auditLogs/?sourceIpAddress=${logEventData.sourceIpAddress}`"
                permission="api.audit_logs.search"
                )
                meta-ip-address(:ipAddress="logEventData.sourceIpAddress")
            b-td(
              v-else
              class="tw-text-right"
              )
              span(class="text-muted") None recorded
          b-tr
            b-td User Country
            b-td(
              v-if="logEventData.sourceCountryIsoCode"
              class="tw-text-right"
              )
              MTextButton(
                :to="`/auditLogs/?sourceCountryIsoCode=${logEventData.sourceCountryIsoCode}`"
                permission="api.audit_logs.search"
                )
                meta-country-code(
                  :isoCode="logEventData.sourceCountryIsoCode"
                  showName
                  )
            b-td(
              v-else
              class="tw-text-right"
              )
              span(class="text-muted") Unknown

      h6(class="tw-mb-2 tw-mt-4") Related Audit Log Events
      MList(
        v-if="relatedLogEventsData && relatedLogEventsData.length > 0"
        showBorder
        )
        div(
          v-for="event in relatedLogEventsData"
          :key="event.eventId"
          )
          audit-log-entry(:item="event")

      div(
        v-else
        class="text-muted tw-italic"
        ) None

  template(#default)
    MCard(
      title="Event Payload"
      data-testid="audit-log-detail-event-payload-card"
      )
      MList(
        v-if="eventPayload && Object.keys(eventPayload).length > 0"
        show-border
        )
        div(
          v-for="(value, key) in eventPayload"
          :key="key"
          class="tw-px-5 tw-py-3"
          )
          div(class="tw-mb-1 tw-font-semibold") {{ key }}
          MBadge(v-if="value === null") null
          pre(
            v-else
            class="tw-text-xs tw-text-neutral-600"
            ) {{ value }}
      div(v-else) This event has no payload data.

    meta-raw-data(
      :kvPair="logEventData"
      name="logEventData"
      )
    meta-raw-data(
      :kvPair="eventPayload"
      name="eventPayload"
      )

  //- TODO: Generated forms aren't quite good enough at rendering this payload data yet, which is why we're still using
  //- the custom renderer above. Revisit this at some point in the future and fix it properly. See also the TODO in
  //- `eventPayload` below.
  //- template(#default)
    b-card(title="Event Payload").shadow-sm
      meta-generated-content(
        :value="eventPayload"
        v-if="eventPayload && Object.keys(eventPayload).length > 1"
        )
      p.text-muted(v-else) This event has no payload data.
</template>

<script lang="ts" setup>
import { cloneDeep } from 'lodash-es'
import { computed, ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'

import { useGameServerApi } from '@metaplay/game-server-api'
import {
  MBadge,
  MButton,
  MCard,
  MClipboardCopy,
  MList,
  MListItem,
  MPageOverviewCard,
  MTextButton,
  MViewContainer,
} from '@metaplay/meta-ui-next'

import AuditLogEntry from '../components/auditlogs/AuditLogEntry.vue'

// import MetaGeneratedContent from '../components/generatedui/components/MetaGeneratedContent.vue'

const props = defineProps<{
  id: string
}>()

const gameServerApi = useGameServerApi()
const route = useRoute()

/**
 * Type helper for the log event payload as of R24. Not complete.
 */
interface LogEventData {
  $type: string
  createdAt: string
  eventId: string
  source: {
    $type: string
    sourceType: string
    sourceId: string
  }
  target: {
    $type: string
    targetType: string
    targetId: string
  }
  sourceCountryIsoCode: string | null
  sourceIpAddress: string
  payload: {
    $type: string
    request: {
      $type: string
      entities: any
    }
    subsystemName: string
    eventTitle: string
    relatedEventIds: string[]
    eventDescription: string
  }
  relatedEventIds: string[]
  displayTitle: string
  displayDescription: string
}

const logEventData = ref<LogEventData>()
const relatedLogEventsData = ref<LogEventData[]>([])
const logEventError = ref<Error>()

/**
 * Link to the target entity of the event.
 */
const targetLink = computed(() => {
  if (!logEventData.value) return undefined

  const type = logEventData.value.target.targetType
  const id = logEventData.value.target.targetId

  if (type === 'Player') return `/players/Player:${id}`
  if (type === 'Guild') return `/guilds/Guild:${id}`
  if (type === '$Broadcast') return `/Broadcasts/${id}`
  if (type === '$Notification') return `/Notifications/${id}`
  if (type === '$Experiment') return `/Experiments/${id}`
  if (type === '$GameConfig') return `/GameConfigs/${id}`
  if (type === 'AsyncMatchmaker') return `/matchmakers/AsyncMatchmaker:${id}`
  if (type === '$Nft') return `/web3/nft/${id}`
  if (type === '$NftCollection') return `/web3/nft/${id}`
  if (type === 'LeagueManager') return `/leagues/LeagueManager:${id}`

  return undefined
})

onMounted(async () => {
  try {
    // Get event data.
    logEventData.value = (await gameServerApi.get(`/auditLog/${props.id}`)).data as LogEventData

    // Get related events.
    for (const id of logEventData.value.relatedEventIds) {
      const data = (await gameServerApi.get(`/auditLog/${id}`)).data as LogEventData
      relatedLogEventsData.value.push(data)
    }
  } catch (e) {
    logEventError.value = e as Error
  }
})

/**
 * Helper function to only show the non-generic parts of an event payload.
 * TODO: This should be done on the backend.
 */
const eventPayload = computed(() => {
  if (!logEventData.value) return undefined

  type GenericPayload = Record<string, any>

  const payload = cloneDeep(logEventData.value.payload) as GenericPayload

  delete payload.eventTitle
  delete payload.eventDescription
  delete payload.relatedEventIds
  delete payload.subsystemName

  // TODO: When switching back to `metaGeneratedContent` rendering we won't want to strip this `$type` any more.
  delete payload.$type

  return payload
})

/**
 * Helper computed to remove the $ sign from the type.
 */
const humanReadableTargetType = computed(() => {
  if (!logEventData.value) return undefined

  // Remove the $ sign from the type.
  return logEventData.value.target.targetType.replace(/^\$/, '')
})
</script>

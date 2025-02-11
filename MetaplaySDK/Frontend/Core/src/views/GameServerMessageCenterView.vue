<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  :is-loading="!errorCountsData"
  permission="api.system.view_error_logs"
  :error="errorCountsError"
  :alerts="pageAlerts"
  )
  template(#overview)
    MPageOverviewCard(
      title="Game Server Message Center"
      subtitle=""
      )
      div(class="tw-space-y-1")
        h4 About Game Server Errors
        p These are the most recent errors that have been logged by the server. Errors are often indicative of a problem with the server, and should be investigated.
        p
          span(v-if="errorCountsData") The logging collector was restarted #[MetaTime(:date="errorCountsData.collectorRestartTime" showAs="timeago")].
          span(v-else) Server errors not loaded...
        p(
          v-if="errorCountsData"
          class="tw-text-xs+ tw-text-neutral-500"
          ) Errors listed on this page are only stored for #[MetaDuration(:duration="errorCountsData.maxAge" showAs="humanized" disable-tooltip)] and are not retained across server restarts.
          span(
            v-if="grafanaEnabled"
            class="tw-ml-1"
            ) A more complete history of errors and warnings is available in&nbsp;
            MTextButton(
              :to="grafanaMoreCompleteHistory"
              permission="dashboard.grafana.view"
              ) Grafana
            span .
          span(
            v-else
            class="tw-ml-1"
            ) When Grafana is configured, a more complete history of errors and warnings can be found there.

      div(class="tw-mt-6 tw-space-y-1")
        h4 About Telemetry Messages
        p(class="tw-text-sm") Telemetry messages inform you of more recent versions of components being available that are currently used by the game server.
        p(
          v-if="telemetryMessagesData"
          class="tw-text-sm"
          ) Telemetry messages were updated #[MetaTime(:date="telemetryMessagesData.updatedAt" showAs="timeago")].
        p(class="tw-text-xs+ tw-text-neutral-500") For example, when more recent versions of the Metaplay SDK, .NET runtime, or the Helm chart are published, you'll get messages about them on this page. The messages are received from the Metaplay developer portal using the server's built-in telemetry manager which reports the component versions used and gets the messages in return.

  MTwoColumnLayout
    meta-event-stream-card(
      icon="bug"
      title="Latest Game Server Errors"
      :event-stream="eventStream"
      utilitiesMode="filter"
      empty-message="No errors logged."
      no-results-message="No errors found. Try a different search."
      allow-pausing
      permission="api.system.view_error_logs"
      data-testId="game-server-errors-card"
      )
      template(#event-details="{ event }")
        MErrorCallout(:error="eventToDisplayError(event)")
          template(#buttons)
            MButton(
              :to="getGrafanaLogsUrl(event)"
              :disabled-tooltip="grafanaEnabled ? undefined : 'Grafana has not been configured for this environment.'"
              permission="dashboard.grafana.view"
              ) View in Grafana

    TelemetryMessagesCard

  MetaRawData(
    :kvPair="errorCountsData"
    name="errorCountsData"
    )
  MetaRawData(
    :kvPair="telemetryMessagesData"
    name="telemetryMessagesData"
    )
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { EventStreamItemBase, EventStreamItemEvent, MetaEventStreamCard } from '@metaplay/event-stream'
import { useStaticInfos } from '@metaplay/game-server-api'
import { MetaDuration, MetaRawData, MetaTime } from '@metaplay/meta-ui'
import {
  DisplayError,
  MButton,
  MErrorCallout,
  MTextButton,
  MPageOverviewCard,
  MViewContainer,
  type MViewContainerAlert,
  MTwoColumnLayout,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import TelemetryMessagesCard from '../components/system/TelemetryMessagesCard.vue'
import { collectorRunTimespanHumanized } from '../coreUtils'
import { makeGrafanaQueryExpression, makeGrafanaUri } from '../grafanaUtils'
import {
  getTelemetryMessagesSubscriptionOptions,
  getServerErrorsSubscriptionOptions,
} from '../subscription_options/general'
import type { LogEventInfo } from '../subscription_options/generalTypes'

const staticInfos = useStaticInfos()

const { data: errorCountsData, error: errorCountsError } = useSubscription(getServerErrorsSubscriptionOptions())

/**
 * Whether Grafana is enabled for this environment.
 */
const grafanaEnabled = computed(() => staticInfos.environmentInfo.grafanaUri)

/**
 * Link to more complete history of errors and warnings in Grafana.
 */
const grafanaMoreCompleteHistory = computed(() => {
  if (!staticInfos.environmentInfo.grafanaUri) return undefined

  return makeGrafanaUri(
    staticInfos.environmentInfo.grafanaUri,
    staticInfos.environmentInfo.kubernetesNamespace ?? undefined,
    [makeGrafanaQueryExpression('loglevel', '=~', 'ERR|WRN')],
    'now-1h',
    'now'
  )
})

/**
 * Returns a URL to the Grafana logs for the given event.
 */
function getGrafanaLogsUrl(event: EventStreamItemBase): string | undefined {
  if (!staticInfos.environmentInfo.grafanaUri) return undefined

  const timestamp = new Date(event.time)
  return makeGrafanaUri(
    staticInfos.environmentInfo.grafanaUri,
    staticInfos.environmentInfo.kubernetesNamespace ?? undefined,
    [],
    new Date(+timestamp - 30_000),
    new Date(+timestamp + 1_000)
  )
}

/**
 * Event stream data, generated from the fetched data.
 */
const eventStream = computed(() => {
  if (errorCountsData.value) {
    // Regex to trim any characters after the first newline.
    const trimRegexp = /\n.*/g

    // Create an event stream.
    const eventStream = errorCountsData.value.errors.map((entry) => {
      return new EventStreamItemEvent(
        entry.timestamp,
        entry.sourceType,
        entry.message.replace(trimRegexp, ''),
        entry.id,
        entry,
        '',
        '',
        ''
      )
    })

    return eventStream
  } else {
    return []
  }
})

/**
 * Convert an event to a displayable error.
 */
function eventToDisplayError(event: EventStreamItemBase): DisplayError {
  const sourceEvent: LogEventInfo = event.typeData.sourceData
  const displayError = new DisplayError(sourceEvent.source, sourceEvent.message)
  if (sourceEvent.exception) {
    displayError.addDetail('Exception', sourceEvent.exception)
  }
  if (sourceEvent.stackTrace) {
    displayError.addDetail('Stack', sourceEvent.stackTrace)
  }
  return displayError
}

/**
 * Show alerts based on the error counts.
 */
const pageAlerts = computed((): MViewContainerAlert[] => {
  if (errorCountsData.value?.overMaxErrorCount) {
    const maxErrorCount = errorCountsData.value.errorCount
    const restartString = collectorRunTimespanHumanized(errorCountsData.value)
    return [
      {
        title: 'Too Many Errors',
        message: `The server has generated over ${maxErrorCount} errors ${restartString}. This may indicate a problem with the server.`,
        variant: 'danger',
      },
    ]
  } else {
    return []
  }
})

// TELEMETRY ----------------------------------------------------------------------------------------------------------

const { data: telemetryMessagesData } = useSubscription(getTelemetryMessagesSubscriptionOptions())
</script>

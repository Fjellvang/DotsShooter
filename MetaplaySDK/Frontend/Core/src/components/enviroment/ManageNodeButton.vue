<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MTextButton(
  v-if="nodeData"
  @click="nodeManageModal?.open"
  :data-testid="`${dataTestid}-button`"
  ) View node
  MModal(
    ref="nodeManageModal"
    :title="`View ${camelCaseToTitleCase(nodeName)} Node`"
    :data-testid="`${dataTestid}-modal`"
    )
    div(class="tw-mb-2 tw-text-neutral-500") View a summary of the systems currently running on this node.
      span(class="tw-mx-1") For detailed insights into the performance and workload, check the
      MTextButton(
        :to="nodeData.grafanaLogUrl ? nodeData.grafanaLogUrl : undefined"
        permission="dashboard.grafana.view"
        :disabled-tooltip="!nodeData.grafanaLogUrl ? 'Grafana is not enabled for this environment.' : undefined"
        target="_blank"
        data-testid="view-grafana-logs-button"
        ) Grafana logs
      span .
    //- Node management content.
    table(class="tw-w-full tw-table-auto")
      tbody(class="tw-divide-y tw-divide-neutral-200 tw-border-t tw-border-neutral-200 *:*:tw-py-1")
        tr
          td Status
          td(:class="['tw-text-right', getStatusVariant(nodeData.nodeStatus)]") {{ getNodeStatus(nodeData.nodeStatus) }}
        tr
          td Scaling
          td(class="tw-text-right") {{ getNodeScalingState(nodeData) }}
        tr
          td Phase
          td(class="tw-text-right") {{ nodeData.rawNodeData.clusterLocalPhase || 'Unknown' }}
        tr
          td Uptime
          td(class="tw-text-right")
            meta-duration(
              v-if="!isEpochTime(nodeData.serverStartedAt)"
              :duration="DateTime.now().diff(DateTime.fromISO(nodeData.serverStartedAt))"
              hideMilliseconds
              showAs="humanizedSentenceCase"
              )
            span(v-else) Not available
        tr
          td Public IP
          td(class="tw-text-right") {{ nodeData.publicIp || 'Not available' }} #[MClipboardCopy(v-if="nodeData.publicIp" :contents="JSON.stringify(nodeData.publicIp)")]
        tr
          td Host
          td(class="tw-text-right") {{ nodeData.nodeAddress }} #[MClipboardCopy(v-if="nodeData.nodeAddress" :contents="nodeData.nodeAddress")]

    //- Hosted entity types.
    div(class="tw-mt-2 tw-font-semibold") Hosted Entities
    p(class="tw-mb-2 tw-text-neutral-500") The entities of each type currently running on this node.
    ul(v-if="nodeData.rawNodeData.liveEntityCounts")
      div(class="tw-grid tw-grid-flow-row tw-gap-x-6 @md:tw-grid-cols-2")
        div(
          v-for="(count, entity) in sortedLiveEntityList"
          :key="entity"
          )
          div(:class="['tw-flex tw-flex-wrap tw-justify-between tw-gap-x-2', { 'tw-text-neutral-400': count === 0 }]")
            span(class="tw-break-word") {{ entity }}
            span(class="text-monospace tw-flex-none") {{ count }}
    div(
      v-else
      class="tw-text-neutral-400"
      )
      span(class="tw-m-auto tw-text-xs+") No data available

    template(#right-panel)
      //- Workload data.
      div(class="tw-font-semibold") Workload Data #[MClipboardCopy(:contents="nodeData.rawNodeData.rawWorkload ? JSON.stringify(nodeData.rawNodeData.rawWorkload) : undefined")]

      div(class="tw-text-xs+ tw-text-neutral-500") The raw workload data for this node. This provides details about how the system's autoscaling is configured.
      div(class="tw-space-y-2")
        div(
          class="tw-mt-2 tw-max-h-72 tw-overflow-scroll tw-rounded-md tw-border tw-border-neutral-300 tw-bg-neutral-100 tw-p-3 tw-text-xs"
          )
          pre(
            v-if="nodeData.rawNodeData.rawWorkload"
            class="tw-font-mono tw-text-neutral-500"
            ) {{ nodeData.rawNodeData.rawWorkload }}
          div(
            v-else
            class="tw-text-neutral-500"
            )
            span(class="tw-mx-auto tw-text-xs+") No data available.
</template>
<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { MClipboardCopy, MTextButton, MModal } from '@metaplay/meta-ui-next'
import { camelCaseToTitleCase, camelCaseToSentenceCase } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import type { Node } from '../../clusterServerTypes'
import { isEpochTime } from '../../coreUtils'
import { getClusterSubscriptionOptions } from '../../subscription_options/general'

const props = defineProps<{
  /**
   * The index of the node set we are interested in.
   */
  nodeSetIndex: number
  /**
   * The name of the node. // TODO: add a unique identifier for each node.
   */
  nodeName: string
  /**
   * Data-testid attribute for testing.
   */
  dataTestid?: string | undefined
}>()

/**
 * Subscribe to the cluster data.
 */
const { data: clusterData } = useSubscription(getClusterSubscriptionOptions())

/**
 * The data for the node set of the node that is being managed.
 */
const nodeSetData = computed(() => {
  return clusterData.value?.nodeSets[props.nodeSetIndex]
})

/**
 * The data for the node that is being managed.
 */
const nodeData = computed(() => {
  return nodeSetData.value?.nodes.find((x: Node) => x.name === props.nodeName)
})

/**
 * Ref used to identify the modal.
 */
const nodeManageModal = ref<typeof MModal>()

/**
 * Alphabetically sorted list of live entities.
 */
const sortedLiveEntityList = computed(() => {
  return Object.fromEntries(
    Object.entries(nodeData.value?.rawNodeData.liveEntityCounts ?? {}).sort((a, b) => a[0].localeCompare(b[0]))
  )
})

function getStatusVariant(status: string): string | undefined {
  if (status === 'HardLimit' || status === 'NotConnected' || status === 'unknown') {
    return 'tw-text-red-500'
  } else if (status === 'ExpectedNotConnected') {
    return 'tw-text-neutral-400'
  } else {
    return undefined
  }
}

function getNodeStatus(status: string): string {
  if (status === 'NotConnected') {
    return 'Failed to connect'
  } else if (status === 'ExpectedNotConnected') {
    return 'Inactive'
  } else {
    return camelCaseToSentenceCase(status)
  }
}

function getNodeScalingState(node: Node): string {
  if (nodeSetData.value?.scalingMode === 'Static') {
    return 'None'
  } else if (node.nodeStatus === 'NotConnected' || node.nodeStatus === 'ExpectedNotConnected') {
    return 'Idle'
  } else {
    return camelCaseToSentenceCase(node.nodeScalingState)
  }
}
</script>

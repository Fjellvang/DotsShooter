<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MetaListCard(
  v-if="clusterData"
  title="All Nodes"
  :itemList="nodeSetsData"
  :searchFields="searchFields"
  :searchPlaceholder="'Search by entity type...'"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :getItemKey="getItemKey"
  emptyMessage="No nodes found."
  data-testid="node-sets"
  )
  template(#item-card="{ item: nodeSet, index }")
    //- Node set.
    MCollapse(extraMListItemMargin)
      template(#header)
        MListItem(
          noLeftPadding
          data-testid="node-set-item"
          )
          span(:class="[{ 'tw-text-neutral-400': nodeSet.mostSevereStatus === 'ExpectedNotConnected' }]") {{ camelCaseToSentenceCase(nodeSet.name) }}
          template(#bottom-left)
            div(
              v-if="nodeSet.scalingMode === 'Static'"
              class="tw-space-x-1"
              )
              span(:class="['tw-text-xs+ tw-my-1 tw-font-semibold', getStaticNodeSetStatus(nodeSet).textVariant]") {{ getStaticNodeSetStatus(nodeSet).title }}:
              span(:class="['tw-text-xs+ tw-my-1', getStaticNodeSetStatus(nodeSet).textVariant]") {{ getStaticNodeSetStatus(nodeSet).description }}
            div(
              v-else
              class="tw-space-x-1"
              )
              span(:class="['tw-text-xs+ tw-my-1 tw-font-semibold', getDynamicNodeSetStatus(nodeSet).textVariant]") {{ getDynamicNodeSetStatus(nodeSet).title }}:
              span(:class="['tw-text-xs+ tw-my-1', getDynamicNodeSetStatus(nodeSet).textVariant]") {{ getDynamicNodeSetStatus(nodeSet).description }}

          template(#bottom-right)
            div(class="tw-flex tw-flex-col tw-space-y-1 tw-text-xs tw-text-neutral-500")
              span(v-if="nodeSet.scalingMode === 'Static'") {{ maybePluralPrefixString(nodeSet.connectedNodes, 'Node', 'Nodes') }} {{ nodeSet.connectedNodes }} / {{ nodeSet.maxNodeCount }}
              span(v-else) {{ maybePluralPrefixString(nodeSet.connectedNodes, 'Node', 'Nodes') }} {{ nodeSet.connectedNodes }} / {{ nodeSet.maxNodeCount }}
              span Entity types {{ Object.keys(nodeSet.aggregatedLiveEntityCounts).length }}

      ul(v-if="nodeSet")
        div(class="bg-light rounded border tw-m3")
          li(
            class="tw-my-2 tw-px-5"
            data-testid="node-set-item-entities"
            )
            span(class="tw-text-sm tw-font-semibold") Current Entities
            div(class="tw-grid tw-gap-x-6 tw-pt-1 tw-text-xs+ @md:tw-grid-cols-2 @lg:tw-grid-cols-3")
              div(
                v-for="(count, entityKind) in sortList(nodeSet.aggregatedLiveEntityCounts)"
                :key="entityKind"
                )
                div(
                  :class="['tw-flex tw-flex-wrap tw-justify-between tw-gap-x-2', { 'tw-text-neutral-400': count === 0 }]"
                  )
                  span(class="tw-break-word") {{ entityKind }}
                  span(class="text-monospace tw-flex-none") {{ count }}

        //- Individual node.
        div(v-if="nodeSet.nodes")
          MListItem(
            v-for="node in nodeSet.nodes"
            :key="node.name"
            data-testid="node-set-item-details"
            )
            p(:class="['tw-text-sm', { 'tw-text-neutral-300': node.nodeStatus === 'ExpectedNotConnected' }]") {{ camelCaseToTitleCase(node.name) }}
              MBadge(
                v-if="getNodePhaseText(nodeSet, node)"
                class="tw-ml-1"
                ) {{ getNodePhaseText(nodeSet, node) }}
            template(#top-right)
              span(:class="[{ 'tw-text-neutral-300': node.nodeStatus === 'ExpectedNotConnected' }]") {{ nodeSet.scalingMode === 'Static' ? 'Static' : 'Dynamic' }}

            //- Node details.
            template(#bottom-left)
              div(:class="['tw-text-xs+', getNodeStatus(node).textVariant]") {{ getNodeStatus(node).message }}
              table(
                class="tw-w-full"
                :class="['tw-max-w-[37rem]', { 'tw-text-neutral-300': node.nodeStatus === 'ExpectedNotConnected' }]"
                )
                tbody(class="tw-divide-y tw-divide-neutral-200 tw-border-t tw-border-neutral-200 *:*:tw-py-1")
                  tr
                    td(class="tw-text-xs+") Uptime
                    td(class="tw-text-right")
                      meta-duration(
                        v-if="!isEpochTime(node.serverStartedAt)"
                        :duration="DateTime.now().diff(DateTime.fromISO(node.serverStartedAt))"
                        hideMilliseconds
                        showAs="humanizedSentenceCase"
                        )
                      span(v-else) Not available
                  tr
                    td(class="tw-text-xs+") Public IP
                    td(class="tw-text-right") {{ node.publicIp || 'Not available' }} #[MClipboardCopy(v-if="node.publicIp" :contents="JSON.stringify(node.publicIp)")]
                  tr
                    td(class="tw-text-xs+") Host
                    td(class="tw-text-right") {{ node.nodeAddress }} #[MClipboardCopy(v-if="node.nodeAddress" :contents="node.nodeAddress")]

            template(#bottom-right)
              //- Node management button.
              ManageNodeButton(
                v-if="node.nodeStatus !== 'ExpectedNotConnected'"
                :nodeSetIndex="index"
                :nodeName="node.name"
                data-testid="manage-node"
                )
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import {
  MetaListSortDirection,
  MetaListSortOption,
  MetaListFilterSet,
  MetaListFilterOption,
  MetaListCard,
} from '@metaplay/meta-ui'
import { MBadge, MClipboardCopy, MCollapse, MListItem } from '@metaplay/meta-ui-next'
import { camelCaseToSentenceCase, camelCaseToTitleCase, maybePluralPrefixString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import type { NodeSet, Node } from '../../clusterServerTypes'
import { isEpochTime } from '../../coreUtils'
import { getClusterSubscriptionOptions } from '../../subscription_options/general'
import ManageNodeButton from './ManageNodeButton.vue'

const { data: clusterData } = useSubscription(getClusterSubscriptionOptions())

/**
 * Node sets data to be displayed in the view.
 */
const nodeSetsData = computed(() => {
  if (!clusterData.value) return []
  return clusterData.value.nodeSets
})

function getStaticNodeSetStatus(nodeSet: NodeSet): {
  title: string
  description: string
  textVariant: string | undefined
} {
  let title: string
  let description: string
  let textVariant: string | undefined
  if (nodeSet.mostSevereStatus === 'NotConnected') {
    title = 'Failed to connect'
    description =
      'One or more nodes in this set are no longer reachable. Please investigate the issue and contact your DevOps team for assistance.'
    textVariant = 'tw-text-red-500'
  } else if (nodeSet.mostSevereStatus === 'HardLimit') {
    title = 'Hard limit reached'
    description =
      'This node set is operating as expected but one or more nodes have reached their hard limit. Consider adjusting resources or provisioning additional nodes.'
    textVariant = 'tw-text-red-500'
  } else if (nodeSet.mostSevereStatus === 'SoftLimit') {
    title = 'Soft limit reached'
    description = 'This node set is operating as expected but one or more nodes have reached their soft limit.'
    textVariant = 'tw-text-neutral-500'
  } else if (nodeSet.mostSevereStatus === 'Perfect') {
    title = 'Perfect'
    description = 'All nodes in this set are operating as expected.'
    textVariant = 'tw-text-neutral-500'
  } else if (nodeSet.mostSevereStatus === 'ExpectedNotConnected') {
    title = 'Inactive'
    description = 'This node set is currently inactive.'
    textVariant = 'tw-text-neutral-400'
  } else {
    title = 'Unknown Status'
    description =
      'This node set is in an unknown state. This is unexpected and should be investigated. Contact your DevOps team for support.'
    textVariant = 'tw-text-red-500'
  }

  return { title, description, textVariant }
}

function getDynamicNodeSetStatus(nodeSet: NodeSet): {
  title: string
  description: string
  textVariant: string | undefined
} {
  let title: string
  let description: string
  let textVariant: string | undefined
  if (nodeSet.mostSevereStatus === 'NotConnected') {
    title = 'Failed to connect'
    description =
      'One or more nodes in this set are no longer reachable. Please investigate and contact your DevOps team for assistance.'
    textVariant = 'tw-text-red-500'
  } else if (nodeSet.mostSevereStatus === 'HardLimit') {
    title = 'Hard limit reached'
    if (nodeSet.scalingState === 'ScalingUp') {
      description =
        'This node set is working as expected, but one or more nodes have reached their hard limit and are attempting to scale up. This might indicate sudden load or an issue occurred while provisioning new nodes. Please investigate this issue or contact your DevOps team for support.'
    } else if (nodeSet.scalingState === 'ScalingDown') {
      description =
        'This node set is working as expected but one or more nodes have reached their hard limit. The system is scaling down to reduce resource usage.'
    } else {
      description =
        'This node set is working as expected but one or more nodes have reached their hard limit and no more nodes can be added. Consider adjusting resources or provisioning additional nodes.'
    }
    textVariant = 'tw-text-red-500'
  } else if (nodeSet.mostSevereStatus === 'SoftLimit') {
    title = 'Soft limit reached'
    if (nodeSet.scalingState === 'ScalingUp') {
      description =
        'The node set is working as expected but one or more nodes have reached their soft limit. The system is scaling up to handle increased demand.'
    } else if (nodeSet.scalingState === 'ScalingDown') {
      description =
        'One or more nodes have reached their soft limit. The system is scaling down to reduce resource usage, this is unexpected and could cause further issues. Please investigate this issue or contact your DevOps team for support.'
      textVariant = 'tw-text-red-500'
    } else {
      description = 'This node set is working as expected but one or more nodes have reached their soft limit.'
    }
    textVariant = 'tw-text-neutral-500'
  } else if (nodeSet.mostSevereStatus === 'Perfect') {
    title = 'Perfect'
    if (nodeSet.scalingState === 'ScalingUp') {
      description =
        'All nodes in this set are working as expected and the system is scaling up to manage increased demand.'
    } else if (nodeSet.scalingState === 'ScalingDown') {
      description =
        'All nodes in this set are working as expected and the system is scaling down to reduce resource usage.'
    } else if (nodeSet.scalingState === 'AtMaxNodeCount') {
      description =
        'All nodes are working as expected, but the maximum node count has been reached. Consider adjusting resources or provisioning additional nodes.'
    } else {
      description = 'All nodes in this set are working as expected and there is additional capacity available.'
    }
    textVariant = 'tw-text-neutral-500'
  } else if (nodeSet.mostSevereStatus === 'ExpectedNotConnected') {
    title = 'Inactive'
    description = 'This node set is currently inactive.'
    textVariant = 'tw-text-neutral-300'
  } else {
    title = 'Unknown'
    description =
      'This node set is in an unknown state. This is unexpected and should be investigated. Contact your DevOps team for support.'
    textVariant = 'tw-text-red-500'
  }
  return { title, description, textVariant }
}

function getNodeStatus(node: Node): { message: string; textVariant: string | undefined } {
  const status = node.nodeStatus
  const localPhase = node.rawNodeData.clusterLocalPhase ?? undefined

  // Determine the current status of the node
  // Step 1: Check for dead states, such as NotConnected, ExpectedNotConnected, or Terminated.
  // Step 2: Check for transitional local phases like 'Starting' or 'Stopping'.
  // Step 3: If the node is running, check for operational states i.e. HardLimit, SoftLimit or Perfect.
  // If all fails, return 'Unknown' status.
  if (status === 'NotConnected') {
    return { message: 'Error: This node has failed to connect.', textVariant: 'tw-text-red-500' }
  } else if (status === 'ExpectedNotConnected') {
    return { message: 'This node is not active.', textVariant: 'tw-text-neutral-300' }
  } else if (localPhase === 'Terminated') {
    return { message: 'This node is no longer active.', textVariant: 'tw-text-neutral-300' }
  } else if (localPhase === 'Starting') {
    return { message: 'This node is starting up.', textVariant: 'tw-text-neutral-500' }
  } else if (status === 'HardLimit') {
    return { message: 'This node has reached its hard limit.', textVariant: 'tw-text-red-500' }
  } else if (status === 'SoftLimit') {
    return { message: 'This node has reached its soft limit.', textVariant: 'tw-text-neutral-500' }
  } else if (status === 'Perfect') {
    return { message: 'This node is operating as expected.', textVariant: 'tw-text-neutral-500' }
  } else {
    return { message: 'Error: This node is in an unknown state.', textVariant: 'tw-text-red-500' }
  }
}

function getNodePhaseText(nodeSet: NodeSet, node: Node): string | undefined {
  if (node.rawNodeData.clusterLocalPhase === 'Running') {
    // Expand on the `Running` state to show scaling state.
    if (nodeSet.scalingMode === 'Static') {
      // Static nodes are never scaling.
      return 'Running'
    } else {
      const scalingNodeState = node.rawNodeData.scalingNodeState
      if (scalingNodeState === 'Working') {
        // Dynamic node is running.
        return 'Running'
      } else if (scalingNodeState === 'Killing') {
        // Dynamic node is in final stages of scaling down.
        return 'Terminating'
      } else {
        // Any other state is considered as unimportant.
        return undefined
      }
    }
  } else if (node.rawNodeData.clusterLocalPhase === 'Starting') {
    return 'Starting'
  } else if (node.rawNodeData.clusterLocalPhase) {
    // The other phases can be literally displayed.
    return camelCaseToTitleCase(node.rawNodeData.clusterLocalPhase)
  } else {
    // If the node is not running (so `clusterLocalPhase` is `null`) then the state is unimportant.
    return undefined
  }
}

/**
 * Sort the list of entity kinds alphabetically.
 */
function sortList(dict: Record<string, number>): Record<string, number> {
  return Object.fromEntries(Object.entries(dict).sort((a, b) => a[0].localeCompare(b[0])))
}

/**
 * Search field options for searching node sets.
 */
const searchFields = ['entityKinds']

/**
 * Filtering options for node sets.
 */
const filterSets = [
  new MetaListFilterSet('scalingMode', [
    new MetaListFilterOption('Static', (x) => (x as NodeSet).scalingMode === 'Static'),
    new MetaListFilterOption('Dynamic', (x) => (x as NodeSet).scalingMode === 'DynamicLinear'),
  ]),
  new MetaListFilterSet('status', [
    new MetaListFilterOption('Not Connected', (x) => (x as NodeSet).mostSevereStatus === 'NotConnected'),
    new MetaListFilterOption('Inactive', (x) => (x as NodeSet).mostSevereStatus === 'ExpectedNotConnected'),
    new MetaListFilterOption('Hard Limit', (x) => (x as NodeSet).mostSevereStatus === 'HardLimit'),
    new MetaListFilterOption('Soft Limit', (x) => (x as NodeSet).mostSevereStatus === 'SoftLimit'),
    new MetaListFilterOption('Perfect', (x) => (x as NodeSet).mostSevereStatus === 'Perfect'),
  ]),
]

/**
 * Sort key for the `MetaListCard`..
 */
function getItemKey(item: NodeSet): string {
  return item.name
}

/**
 * Sorting options for node sets.
 */
const sortOptions = [
  MetaListSortOption.asUnsorted(),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Ascending),
  new MetaListSortOption('Name', 'name', MetaListSortDirection.Descending),
]
</script>
<style lang="scss" scoped>
.table th,
td {
  padding: 0.295rem;
}
</style>

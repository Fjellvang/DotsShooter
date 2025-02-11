<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<!-- A card that shows player incidents. This is used by the Global and Player incident history cards. -->

<template lang="pug">
meta-list-card(
  title="Recent Incident History"
  icon="ambulance"
  dangerous
  :itemList="incidents"
  allowPausing
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  emptyMessage="No incidents reported."
  :moreInfoUri="showMainPageLink ? '/playerIncidents' : undefined"
  :moreInfoLabel="showMainPageLink ? 'player incidents' : undefined"
  permission="api.incident_reports.view"
  :description="description"
  )
  template(#item-card="slotProps")
    MListItem {{ slotProps.item.type }}
      template(#top-right): meta-time(:date="slotProps.item.uploadedAt")
      template(#bottom-left)
        div(class="tw-text-red-500") {{ slotProps.item.reason }}
        MTextButton(
          v-if="isGlobalScope"
          permission="api.players.view"
          :to="`/players/${slotProps.item.playerId}`"
          ) {{ slotProps.item.playerId }}
      template(#bottom-right)
        MTextButton(
          permission="api.incident_reports.view"
          :to="`/players/${slotProps.item.playerId}/${slotProps.item.incidentId}`"
          ) View incident
</template>

<script lang="ts" setup>
import { computed } from 'vue'

import { MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MListItem, MTextButton } from '@metaplay/meta-ui-next'

const props = defineProps<{
  /**
   * List of incidents to show on the card.
   */
  incidents?: any[]
  /**
   * Optional: The list shows incidents that belong to the named player.
   */
  playerName?: string
  /**
   * Optional: The tooltip mentions that the list has been truncated to this many items.
   */
  isLimitedTo?: number
  /**
   * Optional: Includes a link to the main incidents page inside the card.
   */
  showMainPageLink?: boolean
  /**
   * Optional: A description of what incidents are is shown on the card.
   */
  showDescription?: boolean
}>()

/**
 * Search fields to be passed to the meta-list-card component.
 */
const searchFields = ['type', 'reason', 'incidentId']

/**
 * Sort options array to be passed to the meta-list-card component.
 */
const sortOptions = [
  new MetaListSortOption('Time', 'uploadedAt', MetaListSortDirection.Ascending),
  new MetaListSortOption('Time', 'uploadedAt', MetaListSortDirection.Descending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Ascending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Descending),
]
const defaultSortOption = 1

/**
 * Filter sets array to be passed to the meta-list-card component.
 */
const filterSets = computed(() => {
  return [MetaListFilterSet.asDynamicFilterSet(props.incidents ?? [], 'type', (x: any) => x.type)]
})

/**
 * When true the list is assumed to display incidents for all players.
 */
const isGlobalScope = computed(() => {
  return props.playerName === undefined
})

/**
 * Description text for the card.
 */
const description = computed((): string => {
  if (props.showDescription) {
    // Show a generic description on what incidents are.
    return 'Incidents are reports of situations where a player may have had the game crash or freeze. They should be investigated and fixed!'
  } else {
    // Show a detailed description of what incidents are being shown on this card.
    let description = ''
    if (props.isLimitedTo) {
      description += `The ${props.isLimitedTo} most recent`
    } else {
      description += 'Recent'
    }
    description += ' incidents (crashes, network errors, etc.) that have happened to '
    if (isGlobalScope.value) {
      description += 'any player.'
    } else {
      description += `${props.playerName}.`
    }
    return description
  }
})
</script>

<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
//- TODO: This components sister component GlobalIncidentHistoryCard has basic error handling due to use of subscriptions, this might need something similar.
meta-list-card(
  :title="limit ? `Top ${limit} Incident Types in the Last 24h` : 'Unique Incident Types in the Last 24h'"
  :itemList="limitedIncidentCounts"
  allowPausing
  dangerous
  icon="ambulance"
  :searchFields="searchFields"
  :filterSets="filterSets"
  :sortOptions="sortOptions"
  :defaultSortOption="defaultSortOption"
  emptyMessage="No incidents reported."
  permission="api.incident_reports.view"
  data-testid="global-incident-statistics-card"
  )
  template(#description)
    MCallout(
      v-if="areIncidentCountsCapped"
      title="Incident Counts Capped"
      variant="warning"
      class="tw-mx-0 tw-mb-3"
      ) There have been a very high number of incidents in the last 24h! This list does not include all incidents from that time period.

  template(#item-card="slot")
    MListItem {{ slot.item.type }}
      template(#top-right)
        MTextButton(
          permission="api.audit_logs.search"
          :to="`/playerIncidents/${slot.item.fingerprint}`"
          )
          span(v-if="slot.item.count === 1") View incident
          span(v-else) View {{ abbreviateNumber(slot.item.count) }} incidents
      template(#bottom-left)
        div(class="text-danger") {{ slot.item.reason }}
</template>

<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'

import { ApiPoller } from '@metaplay/game-server-api'
import { MetaListFilterSet, MetaListSortDirection, MetaListSortOption } from '@metaplay/meta-ui'
import { MCallout, MListItem, MTextButton, usePermissions } from '@metaplay/meta-ui-next'
import { abbreviateNumber } from '@metaplay/meta-utilities'

// Props --------------------------------------------------------------------------------------------------------------

const props = defineProps<{
  limit?: number
}>()

// Data polling -------------------------------------------------------------------------------------------------------

const permissions = usePermissions()
const statsPoller = ref<ApiPoller>()
const loading = ref(true)
const error = ref()
const incidentCounts = ref()

onMounted(() => {
  if (permissions.doesHavePermission('api.incident_reports.view')) {
    statsPoller.value = new ApiPoller(
      5000,
      'GET',
      '/incidentReports/statistics',
      undefined,
      (data) => {
        incidentCounts.value = data
        loading.value = false
        error.value = null
      },
      (err) => {
        loading.value = false
        error.value = err
      }
    )
  }
})

onUnmounted(() => {
  if (statsPoller.value) {
    statsPoller.value.stop()
  }
})

const limitedIncidentCounts = computed((): any[] | undefined => {
  if (props.limit && incidentCounts.value) {
    return incidentCounts.value.slice(0, props.limit)
  }
  return incidentCounts.value
})

/**
 * Returns `true` if the incident counts are capped. This means that the server truncated the list of incidents for
 * performance reasons.
 */
const areIncidentCountsCapped = computed((): boolean => {
  return incidentCounts.value?.some((incident: any) => incident.countIsLimitedByQuerySize) ?? false
})

// MetaListCard configuration -----------------------------------------------------------------------------------------

const searchFields = ['type', 'reason', 'incidentId']

const filterSets = computed(() => {
  return [MetaListFilterSet.asDynamicFilterSet(limitedIncidentCounts.value ?? [], 'type', (x: any) => x.type)]
})

const sortOptions = [
  new MetaListSortOption('Count', 'count', MetaListSortDirection.Ascending),
  new MetaListSortOption('Count', 'count', MetaListSortDirection.Descending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Ascending),
  new MetaListSortOption('Type', 'type', MetaListSortDirection.Descending),
]

const defaultSortOption = 1
</script>

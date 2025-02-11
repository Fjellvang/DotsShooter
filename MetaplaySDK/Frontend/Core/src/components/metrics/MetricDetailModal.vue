<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MIconButton(@click="detailsModal?.open()")
  svg(xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="tw-h-6 tw-w-4")
    path(d="M15 3.75a.75.75 0 0 1 .75-.75h4.5a.75.75 0 0 1 .75.75v4.5a.75.75 0 0 1-1.5 0V5.56l-3.97 3.97a.75.75 0 1 1-1.06-1.06l3.97-3.97h-2.69a.75.75 0 0 1-.75-.75Zm-12 0A.75.75 0 0 1 3.75 3h4.5a.75.75 0 0 1 0 1.5H5.56l3.97 3.97a.75.75 0 0 1-1.06 1.06L4.5 5.56v2.69a.75.75 0 0 1-1.5 0v-4.5Zm11.47 11.78a.75.75 0 1 1 1.06-1.06l3.97 3.97v-2.69a.75.75 0 0 1 1.5 0v4.5a.75.75 0 0 1-.75.75h-4.5a.75.75 0 0 1 0-1.5h2.69l-3.97-3.97Zm-4.94-1.06a.75.75 0 0 1 0 1.06L5.56 19.5h2.69a.75.75 0 0 1 0 1.5h-4.5a.75.75 0 0 1-.75-.75v-4.5a.75.75 0 0 1 1.5 0v2.69l3.97-3.97a.75.75 0 0 1 1.06 0Z")

//- Details modal
MModal(
  ref="detailsModal"
  :title="data?.dashboardInfo?.name ?? 'Unknown'"
  :data-testid="'metrics-detail-card'"
  :modal-size="'large'"
  :badge="getMetricTimeRangeBadge(timePreset) !== '' ? getMetricTimeRangeBadge(timePreset) : undefined"
  )
  div(v-if="getMetricTimeRangeBadge(timePreset) === ''" class="tw-flex text-neutral-500 tw-text-xs tw-mb-2")
    MetaTime(:date="startTime" showAs="datetime")
    div(class="tw-ml-2 tw-mr-2") -
    MetaTime(:date="endTime" showAs="datetime")

  p(class="tw-m-0 tw-text-sm") {{ data?.dashboardInfo?.purposeDescription ?? 'No description available' }}

  div(class="tw-mt-2")
    p(class="tw-font-semibold tw-m-0 tw-text-sm") Details
    p(class="tw-mb-2 tw-text-sm") {{ data?.dashboardInfo?.implementationDescription ?? 'No description available' }}
    p(v-if="data.id==='errorCount'" class="tw-m-0 tw-text-sm") See more details about errors on the #[MTextButton(to="/serverErrors" permission="api.system.view_error_logs") message center page].
    p(v-if="data.id==='incidentReportCount'" class="tw-m-0 tw-text-sm") See more details about player incidents on the #[MTextButton(to="/playerIncidents" permission="api.incident_reports.view") player incidents page].

  //- Container for controls
  div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50 tw-p-3" v-if="data?.dashboardInfo?.hasCohorts")
    div(class="tw-float-right tw-text-xs" v-if="data?.dashboardInfo?.hasCohorts") Show Date Picker
      MInputSwitch(
        class="tw-ml-2"
        :model-value="cohortDateControlEnabled"
        size="extraSmall"
        @update:model-value="updateCohortDateControlEnabled"
        )
    div(v-if="cohortDateControlEnabled")
      p(class="tw-font-semibold tw-m-0 tw-ml-0.5") Cohort Date
      MInputDate(
        v-if="data?.id === 'retention' || data?.id === 'arpDauPerDay'"
        :model-value="cohortDate ?? startTime.toISODate() ?? ''"
        :max-iso-date="endTime.toISODate() ?? ''"
        :min-iso-date="startTime.toISODate() ?? ''"
        size="small"
        @update:model-value="(event: string) => cohortDate = event"
        )
      p(v-if="data?.id === 'retention' || data?.id === 'arpDauPerDay'" class="tw-text-neutral-500 tw-text-xs tw-ml-0.5") Showing cohort data starting from {{ cohortDate ?? '' }}.
    p(v-else class="tw-text-neutral-500 tw-text-xs tw-m-0 tw-ml-0.5") Showing averages of daily cohorts.

  //- Detail Modal Chart
  MTimeseriesBarChart(
    v-if="data && data.values && data.dashboardInfo && !data.dashboardInfo.hasCohorts"
    v-bind="transformTimeseriesData(data.values, data.dashboardInfo, activeTimeSeriesResolution)"
    )
  MTimeseriesBarChart(
    v-else-if="data && data.cohorts && data.dashboardInfo?.hasCohorts"
    v-bind="transformCohortData(data, cohortDate)"
    )
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'
import { ref } from 'vue'

import { MetaTime } from '@metaplay/meta-ui'
import { MModal, MInputDate, MInputSwitch, MIconButton, MTimeseriesBarChart, MTextButton } from '@metaplay/meta-ui-next'

import { transformTimeseriesData, transformCohortData, getMetricTimeRangeBadge } from '../../metricsUtils'
import type { TimeSeriesData, TimeseriesResolution } from '../../subscription_options/metrics'

const props = defineProps<{
  data: TimeSeriesData
  activeTimeSeriesResolution: TimeseriesResolution
  timePreset: string
  startTime: DateTime
  endTime: DateTime
}>()

/**
 * Details modal component reference.
 */
const detailsModal = ref<typeof MModal>()

/**
 * Enable or disable cohort date control.
 * @param value Value to set.
 */
const cohortDateControlEnabled = ref(false)

/**
 * Cohort date, used in detail modal controls.
 */
const cohortDate = ref<string | undefined>(undefined)

/**
 * Update cohort date control enabled state. Clears the cohort dates if disabled.
 * @param value Value to set.
 */
function updateCohortDateControlEnabled(value: boolean): void {
  cohortDateControlEnabled.value = value
  if (!value) {
    cohortDate.value = undefined
  } else {
    cohortDate.value = props.startTime.toISODate() ?? undefined
  }
}
</script>

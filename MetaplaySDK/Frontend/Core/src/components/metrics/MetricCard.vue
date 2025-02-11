<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  :title="metricInfo.dashboardInfo?.name ?? metricInfo?.id ?? 'Unknown'"
  class="tw-min-h-[558px]"
  :badge="getMetricTimeRangeBadge(timePreset) !== '' ? getMetricTimeRangeBadge(timePreset) : undefined"
  :data-testid="'metrics-card'"
  )
  template(
    v-if="!(metricInfo.dashboardInfo?.dailyOnly && activeTimeseriesResolution !== 'Daily') && metric"
    #header-right
    )
    MetricDetailModal(
      :data="metric"
      :active-time-series-resolution="activeTimeseriesResolution"
      :time-preset="timePreset"
      :start-time="startTime"
      :end-time="endTime"
      )

  template(#subtitle)
    div(
      v-if="getMetricTimeRangeBadge(timePreset) === ''"
      class="tw-text-xs+"
      ) #[MetaTime(:date="startTime" showAs="datetime")] - #[MetaTime(:date="endTime" showAs="datetime")]
    p(class="tw-mt-2") {{ metricInfo?.dashboardInfo?.purposeDescription ?? 'No description available' }}
  div(
    v-if="!permissions.doesHavePermission('api.metrics.view')"
    class="tw-m-0 tw-mt-[160px] tw-text-center tw-italic tw-text-neutral-500"
    ) You do not have the permission to view this metric.
  p(
    v-if="metricInfo.dashboardInfo?.dailyOnly && activeTimeseriesResolution !== 'Daily'"
    class="tw-m-0 tw-mt-[160px] tw-text-center tw-italic tw-text-neutral-500"
    ) This metric is only available on a daily resolution.
  p(
    v-else-if="!metric"
    class="tw-m-0 tw-mt-[160px] tw-text-center tw-italic tw-text-neutral-500"
    ) Metric data is loading...
  div(v-else)
    MTimeseriesBarChart(
      v-if="metric && !metric.dashboardInfo?.hasCohorts && metric.dashboardInfo && metricHasData(metric)"
      v-bind="transformTimeseriesData(metric.values, metric.dashboardInfo, activeTimeseriesResolution)"
      )
    MTimeseriesBarChart(
      v-else-if="metric && metric?.dashboardInfo?.hasCohorts && metricHasData(metric)"
      v-bind="transformCohortData(metric, undefined)"
      )
    p(
      v-else
      class="tw-m-0 tw-mt-[160px] tw-text-center tw-italic tw-text-neutral-500"
      ) No data available for this metric.
</template>

<script setup lang="ts">
import { DateTime } from 'luxon'

import { MetaTime } from '@metaplay/meta-ui'
import { MCard, usePermissions, MTimeseriesBarChart } from '@metaplay/meta-ui-next'

import {
  transformTimeseriesData,
  transformCohortData,
  getMetricTimeRangeBadge,
  metricHasData,
} from '../../metricsUtils'
import type { MetricInfo, TimeSeriesData, TimeseriesResolution } from '../../subscription_options/metrics'
import MetricDetailModal from './MetricDetailModal.vue'

const props = defineProps<{
  metricInfo: MetricInfo
  metric: TimeSeriesData | undefined
  timePreset: string
  startTime: DateTime
  endTime: DateTime
  activeTimeseriesResolution: TimeseriesResolution
}>()

/**
 * Permissions to check for access to metrics and display the correct message.
 */
const permissions = usePermissions()
</script>

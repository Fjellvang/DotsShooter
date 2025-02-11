<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MViewContainer(
  full-width
  permission="api.metrics.view"
  :error="metricsInfoError"
  :is-loading="!metricsInfoData"
  )
  template(#overview)
    MPageOverviewCard(title="Game Server Metrics")
      p(class="tw-mb-1") Metrics track your game's performance over time.
      div(class="tw-text-sm tw-text-neutral-500 tw-mb-4") Expand the individual metrics to see details on how they are calculated, and use the time range and resolution controls to view the metrics for different time periods. Expand cohort metrics to view additional date controls.

      // Container for controls
      div(class="tw-rounded-md tw-border tw-border-neutral-200 tw-bg-neutral-50 tw-p-3")
        div(class="tw-flex tw-justify-between tw-mb-2")
          div(class="tw-block tw-text-sm tw-font-bold tw-mt-1") Time Range
          MInputSingleSelectRadio(
            :model-value="timePreset"
            :options="timePresetsOptions"
            size="small"
            @update:model-value="event => setTimePreset(event)"
            )

        div(class="tw-flex tw-justify-between")
          MInputDateTime(
            :disabled="timePreset !== 'custom'"
            :model-value="startTime"
            label="Start Time (UTC)"
            :max-date-time="endTime"
            :min-date-time="minDateTime"
            v-on:update:model-value="setStartTime"
            )
          MInputDateTime(
            :disabled="timePreset !== 'custom'"
            :model-value="endTime"
            label="End Time (UTC)"
            :max-date-time="DateTime.now()"
            :min-date-time="minDateTime"
            v-on:update:model-value="setEndTime"
            )
        div(class="tw-flex tw-justify-between tw-mt-2")
          div(v-if="timePreset === 'custom'" class="tw-text-sm tw-font-bold tw-mt-1") Resolution
          div(v-else class="tw-text-sm tw-font-bold tw-text-neutral-400 tw-mt-1") Resolution
          MInputSingleSelectRadio(
            :disabled-tooltip="(timePreset !== 'custom') ? 'Resolution is set by time range' : ''"
            :model-value="activeTimeseriesResolution"
            :options="timeseriesResolutionOptions"
            size="small"
            @update:model-value="setResolution"
            )
        p(v-if="invalidResolutionForTimeRangeError" class="tw-text-sm tw-font-semibold tw-text-red-500 tw-mt-1") {{ invalidResolutionForTimeRangeError }}

  template(#default)
    //- Dynamic Tab Layout.
    MTabLayout(v-if="tabOptions" :tabs="tabOptions" @onTabChanged="(event: number) => onTabChanged(event)")
      template(v-for="(item, index) in tabOptions" v-slot:[`tab-${index}`])
        MTwoColumnLayout()
          template(v-for="metric in orderedAndFilteredMetrics" :key="metric?.id")
            MetricCard(
              v-if="!metric.dashboardInfo?.isHidden"
              :metric-info="metric"
              :metric="getMetricData(metric.id)"
              :active-timeseries-resolution="activeTimeseriesResolution"
              :time-preset="timePreset"
              :start-time="startTime"
              :end-time="endTime"
            )
    p(v-else class="tw-m-0 tw-text-center tw-italic tw-text-neutral-500") Metrics categories are loading...

    MetaRawData(:kv-pair="metricsData" name="metricsData")

</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, onMounted, ref, watch } from 'vue'

import { MetaRawData } from '@metaplay/meta-ui'
import {
  MInputSingleSelectRadio,
  MViewContainer,
  MPageOverviewCard,
  MTwoColumnLayout,
  MTabLayout,
  type TabOption,
  MInputDateTime,
  usePermissions,
} from '@metaplay/meta-ui-next'
import { useSubscription } from '@metaplay/subscriptions'

import MetricCard from '../components/metrics/MetricCard.vue'
import { resolutionConfigs } from '../metricsUtils'
import {
  getCategoryMetricsSubscriptionOptions,
  type MetricsData,
  type TimeSeriesData,
  type TimeseriesResolution,
  type MetricsInfoData,
  getAllMetricsInfoSubscriptionOptions,
} from '../subscription_options/metrics'

/**
 * Permissions to check for access to metrics and display the correct message.
 */
const permissions = usePermissions()

/**
 * Metrics info subscription, includes names, descriptions, categories and other dashboard info of metrics.
 */
const { data: metricsInfoData, error: metricsInfoError } = useSubscription<MetricsInfoData>(() =>
  getAllMetricsInfoSubscriptionOptions()
)

/**
 * Active timeseries resolution for the metrics view.
 */
const activeTimeseriesResolution = ref<TimeseriesResolution>('Daily')

/**
 * Active category/tab for the metrics view. Used to fetch metrics data for the specific category.
 */
const activeCategory = ref<string | undefined>()

/**
 * Minimum date for selecting
 */
const minDateTime = ref<DateTime>(DateTime.fromSeconds(0))

/**
 * Start time for the metrics view.
 */
const startTime = ref<DateTime>(
  DateTime.fromSeconds(
    DateTime.now().toSeconds() - resolutionConfigs[activeTimeseriesResolution.value].windowSizeSeconds
  )
)

/**
 * End time for the metrics view.
 */
const endTime = ref<DateTime>(DateTime.now())

/**
 * Update active category when the tab is changed.
 * @param newTab New tab index.
 */
function onTabChanged(newTab: number): void {
  if (tabOptions.value) {
    activeCategory.value = tabOptions.value[newTab].label
  }
}

/**
 * Get metric data by id.
 * @param id Metric id.
 */
function getMetricData(id: string): TimeSeriesData | undefined {
  return metricsData?.value?.metrics?.find((m) => m.id === id)
}

/**
 * Tab options for the metrics view.
 */
const tabOptions = ref<TabOption[] | undefined>()

/**
 * Update tab options when metrics info data changes.
 */
watch(
  metricsInfoData,
  (newValue) => {
    updateTabOptions(newValue)
    if (newValue) {
      minDateTime.value = DateTime.fromISO(newValue.epochTime)
      startTime.value = DateTime.max(minDateTime.value, startTime.value)
      endTime.value = DateTime.max(minDateTime.value, endTime.value)
    }
  },
  { immediate: true }
)

/**
 * Update tab options and sort them based on the metrics info data.
 * @param info Metrics info data.
 */
function updateTabOptions(info: MetricsInfoData | undefined): void {
  if (info?.categories) {
    const sortedCategories = info.categories.toSorted((a, b) => a.orderIndex - b.orderIndex).map((c) => c.name)

    tabOptions.value = sortedCategories.map((category) => ({ label: category }))
    if (!activeCategory.value) {
      activeCategory.value = tabOptions.value[0].label
    }
  }
}

/**
 * Open the details modal with the given data.
 * @param data Data to show in the details modal.
 */
function openDetailsModal(id: string): TimeSeriesData {
  const data = metricsData?.value?.metrics?.find((m) => m.id === id)
  if (data) {
    return data
  } else {
    throw new Error('Metric not found')
  }
}

/**
 * Currently active time preset for the preset radio toggle.
 */
const timePreset = ref<string>('60d')

/**
 * Time presets for the preset radio toggle.
 */
const timePresetsOptions = [
  { label: 'Last 4 hours', value: '4h' },
  { label: 'Last 3 days', value: '3d' },
  { label: 'Last 60 days', value: '60d' },
  { label: 'Custom', value: 'custom' },
]

/**
 * Timeseries resolution options for the resolution radio toggle.
 */
let timeseriesResolutionOptions = Object.keys(resolutionConfigs).map((key) => ({
  label: resolutionConfigs[key as TimeseriesResolution].label,
  value: key as TimeseriesResolution,
  disabled: getResolutionOptionDisabled(key as TimeseriesResolution),
}))

/**
 * Update timeseries resolution options including their disabled state based on the current time range and resolution.
 */
function updateTimeseriesResolutionOptions(): void {
  timeseriesResolutionOptions = Object.keys(resolutionConfigs).map((key) => ({
    label: resolutionConfigs[key as TimeseriesResolution].label,
    value: key as TimeseriesResolution,
    disabled: getResolutionOptionDisabled(key as TimeseriesResolution),
  }))
}

/**
 * Get disabled state of a resolution option for the resolution radio toggle.
 * @param resolution Resolution to check.
 */
function getResolutionOptionDisabled(resolution: TimeseriesResolution): boolean {
  if (timePreset.value !== 'custom') {
    return true
  }
  return endTime.value.toSeconds() - startTime.value.toSeconds() > resolutionConfigs[resolution].maxWindowSizeSeconds
}

const invalidResolutionForTimeRangeError = ref<string | undefined>()

/**
 * Set the start time for the metrics view.
 * @param time Time to set as the start time.
 */
function setStartTime(time: DateTime | undefined): void {
  if (time) {
    if (endTime.value.toSeconds() - time.toSeconds() > resolutionConfigs.Daily.maxWindowSizeSeconds) {
      startTime.value = DateTime.fromSeconds(endTime.value.toSeconds() - resolutionConfigs.Daily.maxWindowSizeSeconds)
      invalidResolutionForTimeRangeError.value = `Time range is too large for ${resolutionConfigs.Daily.label} resolution. Start time has been automatically adjusted.`
    } else {
      startTime.value = time
      checkForInvalidResolution()
    }
    startTime.value = DateTime.max(startTime.value, minDateTime.value)
  }
  updateTimeseriesResolutionOptions()
}

/**
 * Set the end time for the metrics view.
 * @param time Time to set as the end time.
 */
function setEndTime(time: DateTime | undefined): void {
  // Adjust the end time if maximum daily resolution window size is reached
  if (time) {
    if (time?.toSeconds() - startTime.value.toSeconds() > resolutionConfigs.Daily.maxWindowSizeSeconds) {
      endTime.value = DateTime.fromSeconds(startTime.value.toSeconds() + resolutionConfigs.Daily.maxWindowSizeSeconds)
      invalidResolutionForTimeRangeError.value = `Time range is too large for ${resolutionConfigs.Daily.label} resolution. End time has been automatically adjusted.`
    } else {
      endTime.value = time
      checkForInvalidResolution()
    }
    endTime.value = DateTime.max(endTime.value, minDateTime.value)
  }
  updateTimeseriesResolutionOptions()
}

/**
 * Set the resolution manually for the metrics view.
 * @param resolution Resolution to set. Possible values: 'Minutely', 'Hourly', 'Daily'.
 */
function setResolution(resolution: TimeseriesResolution): void {
  activeTimeseriesResolution.value = resolution
  checkForInvalidResolution()
  updateTimeseriesResolutionOptions()
}

/**
 * Check if the resolution is invalid for the current time range. Adjust the resolution and end time if necessary.
 */
function checkForInvalidResolution(): void {
  if (
    endTime.value.toSeconds() - startTime.value.toSeconds() >
    resolutionConfigs[activeTimeseriesResolution.value].maxWindowSizeSeconds
  ) {
    invalidResolutionForTimeRangeError.value = `Time range is too large for ${resolutionConfigs[activeTimeseriesResolution.value].label} resolution. Resolution has been automatically adjusted.`
    if (
      activeTimeseriesResolution.value === 'Minutely' &&
      endTime.value.toSeconds() - startTime.value.toSeconds() < resolutionConfigs.Hourly.maxWindowSizeSeconds
    ) {
      activeTimeseriesResolution.value = 'Hourly'
    } else {
      activeTimeseriesResolution.value = 'Daily'
    }
  } else {
    invalidResolutionForTimeRangeError.value = undefined
  }
}

/**
 * Set the time preset for the metrics view. Automatically sets the start and end times, and the resolution.
 * @param preset Preset to set. Possible values: '4h', '3d', '60d', 'custom'.
 */
function setTimePreset(preset: string): void {
  const now = DateTime.now()
  switch (preset) {
    case '4h':
      startTime.value = DateTime.max(DateTime.fromSeconds(now.toSeconds() - 4 * 3600), minDateTime.value)
      endTime.value = now
      activeTimeseriesResolution.value = 'Minutely'
      break
    case '3d':
      startTime.value = DateTime.max(DateTime.fromSeconds(now.toSeconds() - 3 * 86400), minDateTime.value)
      endTime.value = now
      activeTimeseriesResolution.value = 'Hourly'
      break
    case '60d':
      startTime.value = DateTime.max(DateTime.fromSeconds(now.toSeconds() - 60 * 86400), minDateTime.value)
      endTime.value = now
      activeTimeseriesResolution.value = 'Daily'
      break
    case 'custom':
      // enable custom time range
      break
    default:
      break
  }

  metricsError.value = null
  timePreset.value = preset
  updateTimeseriesResolutionOptions()
}

/**
 * Metrics data subscription.
 */
const { data: metricsData, error: metricsError } = useSubscription<MetricsData>(() => {
  if (!activeCategory.value) {
    return
  }
  return getCategoryMetricsSubscriptionOptions(
    activeCategory.value,
    activeTimeseriesResolution.value,
    startTime.value,
    endTime.value
  )
})

/**
 * Ordered and filtered metrics based on the active category and the metrics' order index.
 */
const orderedAndFilteredMetrics = computed(() => {
  if (!metricsInfoData?.value?.metrics) {
    return []
  }
  return metricsInfoData?.value?.metrics
    .slice()
    .filter((m) => m.dashboardInfo?.category === activeCategory.value)
    .sort((a, b) => (b.dashboardInfo?.orderIndex ?? 0) - (a.dashboardInfo?.orderIndex ?? 0))
})
</script>

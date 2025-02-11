// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { DateTime } from 'luxon'

import {
  type SubscriptionOptions,
  getFetcherPolicyGet,
  getPollingPolicyTimer,
  getCacheRetentionPolicyTimed,
  getCacheRetentionPolicyKeepForever,
} from '@metaplay/subscriptions'

import type { RangeInterval } from '@observablehq/plot'

//---------------------------------------------------------------------
// Types
//---------------------------------------------------------------------

/**
 * Supported time series resolutions.
 */
export type TimeseriesResolution = 'Minutely' | 'Hourly' | 'Daily'

/**
 * Single metric time series.
 */
export interface TimeSeriesData {
  /** Id of the metric */
  id: string
  /** Dashboard info of metric */
  dashboardInfo?: MetricDashboardInfo
  /** All values in a simple time series: timestamp => value.
   * Can be undefined/null if the metric has cohorts. In that case the cohorts field is used.
   * @example { 1622505600000: 10, 1622592000000: 20 }
   */
  values?: Record<string, number>
  /** Cohort data: timestamp => (column title => value).
   * Can be undefined/null if the metric is a simple time series. In that case the values field is used.
   * @example { 1622505600000: { 'D0': 10, 'D1': 20 } }
   */
  cohorts?: Record<string, Record<string, number>>
}

/**
 * Metric dashboard info, essentially metadata used to display the metric.
 */
export interface MetricDashboardInfo {
  name: string
  purposeDescription: string
  implementationDescription: string
  category: string
  hasCohorts: boolean
  unit: string
  unitOnFront: boolean
  decimalPrecision: number
  isHidden: boolean
  yMinValue: number
  dailyOnly: boolean
  orderIndex: number
  canBeNull: boolean
}

/**
 * A group of metrics time series.
 */
export interface MetricsData {
  metrics?: TimeSeriesData[]
}

/**
 * All metrics and categories info. Does not include time series data.
 */
export interface MetricsInfoData {
  /** All metrics dashboard infos */
  metrics?: MetricInfo[]
  /** All categories with their order index */
  categories?: Array<{
    name: string
    orderIndex: number
  }>
  /** The earliest queryable time */
  epochTime: string
}

/**
 * Single metric info/metadata.
 */
export interface MetricInfo {
  /** Id */
  id: string
  /** Dashboard info of metric */
  dashboardInfo?: MetricDashboardInfo
}

/**
 * Configuration for each resolution option.
 */
export interface ResolutionConfig {
  label: string // Label to show on the radio button
  interval: RangeInterval // Chart interval to use (time between data points)
  xTickFormat: string | ((d: any) => string) | undefined // Tick format to use in chart
  windowSizeSeconds: number // Size of time window to visualize in seconds
  maxWindowSizeSeconds: number // Maximum allowed size of time window to visualize in seconds
}

export interface PlotDataPoint {
  instant: Date
  value: number
}

export interface CohortPlotDataPoint {
  instant: string
  value: number
  count: number
}

// \todo Same as MTimeseriesBarChart props -- refactor into component?
export interface PlotData {
  data: PlotDataPoint[]
  interval?: RangeInterval
  xTickFormat?: string | ((d: any) => string) | undefined // d: string | number | DateTime | or something else?
  yTickFormat?: string | ((d: number) => string) | undefined
  xDomain?: Iterable<any>
  yMinValue?: number
  tooltipContent?: (datapoint: { instant: Date; value: number }) => string
}

//---------------------------------------------------------------------
// Subscriptions
//---------------------------------------------------------------------

/**
 * Subscription options for fetching a single metric time series.
 *
 * @param metricName - Name of the metric.
 * @param resolution - Time series resolution.
 * @param startTime - Start time of the time series.
 * @param endTime - End time of the time series.
 */
export function getSingleMetricSubscriptionOptions(
  metricName: string,
  resolution: TimeseriesResolution,
  startTime: DateTime,
  endTime: DateTime
): SubscriptionOptions<TimeSeriesData> {
  return {
    permission: 'api.metrics.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(
      `/metrics/single/${metricName}?resolution=${resolution}&startTime=${startTime.toUTC().toString()}&endTime=${endTime.toUTC().toString()}`
    ),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(60000),
  }
}

/**
 * Subscription options for fetching all metrics time series in a specific category.
 * @param category - Category name.
 * @param resolution - Time series resolution.
 * @param startTime - Start time of the time series.
 * @param endTime - End time of the time series.
 */
export function getCategoryMetricsSubscriptionOptions(
  category: string | null,
  resolution: TimeseriesResolution,
  startTime: DateTime | null,
  endTime: DateTime | null
): SubscriptionOptions<MetricsData> {
  return {
    permission: 'api.metrics.view',
    pollingPolicy: getPollingPolicyTimer(10000),
    fetcherPolicy: getFetcherPolicyGet(
      `/metrics/category/${category}?resolution=${resolution}&startTime=${startTime?.toUTC().toString()}&endTime=${endTime?.toUTC().toString()}`
    ),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(60000),
  }
}

/**
 * Subscription options for fetching all metrics info.
 * @param resolution - Time series resolution.
 * @param startTime - Start time of the time series.
 * @param endTime - End time of the time series.
 */
export function getAllMetricsInfoSubscriptionOptions(): SubscriptionOptions<MetricsInfoData> {
  return {
    permission: 'api.metrics.view',
    pollingPolicy: getPollingPolicyTimer(60000),
    fetcherPolicy: getFetcherPolicyGet(`/metrics`),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

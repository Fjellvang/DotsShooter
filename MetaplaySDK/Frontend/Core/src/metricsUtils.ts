// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { forEach } from 'lodash-es'

import { abbreviateNumber } from '@metaplay/meta-utilities'

import type {
  TimeSeriesData,
  PlotData,
  PlotDataPoint,
  CohortPlotDataPoint,
  MetricDashboardInfo,
  ResolutionConfig,
  TimeseriesResolution,
} from './subscription_options/metrics'

/**
 * Resolution configurations for the metrics view.
 */
export const resolutionConfigs: Record<TimeseriesResolution, ResolutionConfig> = {
  Minutely: {
    label: 'Per Minute',
    interval: '1 minute',
    xTickFormat: '%Y-%m-%d\n%H:%M',
    windowSizeSeconds: 3600, // 1 hour
    maxWindowSizeSeconds: 86400, // 1 day
  },
  Hourly: {
    label: 'Hourly',
    interval: '1 hour',
    xTickFormat: '%Y-%m-%d\n%H:%M',
    windowSizeSeconds: 3 * 86400, // 3 days
    maxWindowSizeSeconds: 30 * 86400, // 30 days
  },
  Daily: {
    label: 'Daily',
    interval: '1 day',
    xTickFormat: (d) => d.toISOString().split('T')[0],
    windowSizeSeconds: 60 * 86400, // 60 days
    maxWindowSizeSeconds: 365 * 3 * 86400, // Three years
  },
}

export function getMetricTimeRangeBadge(timePreset: string): string {
  if (timePreset === 'custom') {
    return ''
  }
  return `Last ${timePreset}`
}

/**
 * Transform timeseries data to a format that can be used in the chart.
 * @param input Simple timeseries values.
 * @param dashboardInfo Metric's dashboard info.
 * @returns Transformed data.
 */
export function transformTimeseriesData(
  input: Record<number, number> | undefined,
  dashboardInfo: MetricDashboardInfo,
  activeTimeseriesResolution: TimeseriesResolution
): PlotData {
  if (!input) {
    return {
      data: [],
    }
  }

  // Transform data to { instant: Date, value: number }
  const data: PlotDataPoint[] = Object.entries(input).map(([ts, value]) => ({ instant: new Date(ts), value }))

  const config: ResolutionConfig = resolutionConfigs[activeTimeseriesResolution]
  // const endTime = data[data.length - 1].instant
  // const startTime = new Date(+endTime - (config.windowSize * 1000))
  // console.log('startTime:', startTime)
  // console.log('endTime:', endTime)

  const fractionDigits = dashboardInfo.decimalPrecision ?? 0
  const unitStr: string = dashboardInfo.unit ? ` ${dashboardInfo.unit}` : ''
  const yTickFormat = (v: number): string =>
    (dashboardInfo.unit && dashboardInfo.unitOnFront ? dashboardInfo.unit : '') +
    abbreviateNumber(v) +
    (dashboardInfo.unit && !dashboardInfo.unitOnFront ? dashboardInfo.unit : '')

  const tooltipContent = (d: { instant: Date; value: number }): string =>
    `${
      dashboardInfo.unitOnFront ? unitStr : ''
    }${d.value?.toFixed(fractionDigits)}${!dashboardInfo.unitOnFront ? unitStr : ''}\n${d.instant.toUTCString()}`

  return {
    data,
    interval: config.interval,
    xTickFormat: config.xTickFormat,
    yTickFormat,
    yMinValue: dashboardInfo.yMinValue !== 0 ? dashboardInfo.yMinValue : undefined,
    tooltipContent,
  }
}

/**
 * Transform cohort data to a format that can be used in the chart.
 * @param input Cohort data.
 * @param inputDate Date to show cohorts for.
 * @returns Transformed data.
 */
export function transformCohortData(input: TimeSeriesData | undefined, inputDate: string | undefined): PlotData {
  if (!input?.cohorts) {
    return {
      data: [],
    }
  }

  let data: PlotDataPoint[] = []
  if (inputDate !== undefined) {
    // Render selected day's cohorts
    const [, cohort]: [string, Record<string, number>] = Object.entries(input.cohorts ?? {}).find(
      (cohorts) => Date.parse(cohorts[0]) === (Date.parse(inputDate) ?? '') // Date.parse because the strings have different formats
    ) ?? ['', { '': 0 }]
    if (Object.entries(cohort).some(([, value]) => value !== null)) {
      data = Object.entries(cohort).map(([dayOffset, value]) => ({
        // \todo Huge kludge to put raw Dx ints in a Date
        instant: new Date(parseInt(dayOffset.slice(1), 10)),
        value,
      }))
    } else {
      data = []
    }
  } else {
    // Render average value per cohort
    const cohortData: CohortPlotDataPoint[] = new Array<CohortPlotDataPoint>()
    forEach(input.cohorts, (cohort) => {
      forEach(cohort, (value, dayOffset) => {
        const dataPoint = cohortData.find((d) => d.instant === dayOffset)
        if (dataPoint) {
          if (value !== null) {
            dataPoint.value += value
            dataPoint.count += 1
          }
        } else {
          if (value !== null) {
            cohortData.push({
              instant: dayOffset,
              value,
              count: 1,
            })
          }
        }
      })
    })

    data = cohortData.map((d) => ({
      // \todo Huge kludge to put raw Dx ints in a Date
      instant: new Date(parseInt(d.instant.slice(1), 10)),
      value: d.value / d.count,
    }))
  }

  const xDomain = Array.from(data.map((d) => d.instant))
  const yTickFormat = (v: number): string =>
    (input.dashboardInfo?.unit && input.dashboardInfo.unitOnFront ? input.dashboardInfo.unit : '') +
    abbreviateNumber(v) +
    (input.dashboardInfo?.unit && !input.dashboardInfo.unitOnFront ? input.dashboardInfo.unit : '')

  const tooltipContent = (d: { instant: Date; value: number }): string =>
    `D${+d.instant}: ${
      input.dashboardInfo?.unitOnFront ? (input.dashboardInfo.unit ?? '') : ''
    }${d.value?.toFixed(input.dashboardInfo?.decimalPrecision ?? 0)}${!input.dashboardInfo?.unitOnFront ? (input.dashboardInfo?.unit ?? '') : ''}`

  return {
    data,
    interval: 1,
    yMinValue: input.dashboardInfo?.yMinValue !== 0 ? input.dashboardInfo?.yMinValue : undefined,
    yTickFormat,
    xTickFormat: (d: Date): string => `D${+d}`,
    xDomain,
    tooltipContent,
  }
}

export function metricHasData(metric: TimeSeriesData): boolean {
  if (metric.values) {
    return Object.entries(metric.values).some((value) => value[1] !== null)
  }
  if (metric.cohorts) {
    return Object.entries(metric.cohorts).some((day) => Object.entries(day[1]).some((value) => value[1] !== null))
  }
  return false
}

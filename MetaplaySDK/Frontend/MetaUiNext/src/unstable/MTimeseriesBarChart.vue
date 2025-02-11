<template lang="pug">
MPlot(
  v-if="data.length > 0"
  :options="plotOptions"
  )
</template>

<script setup lang="ts">
import { computed } from 'vue'

import { abbreviateNumber } from '@metaplay/meta-utilities'

import { barY, pointerX, tip, type PlotOptions, type RangeInterval } from '@observablehq/plot'

import MPlot from './MPlot.vue'

const props = withDefaults(
  defineProps<{
    /**
     * Array of data points. Supports only preformatted data of JS Date and number to avoid further processing.
     */
    data: Array<{ instant: Date; value: number }>
    /**
     * Optional: Time interval for the x-axis. Default is 'minute'.
     */
    interval?: RangeInterval
    /**
     * Optional: String format to use for X-axis ticks, i.e., graph time.
     * d: number | string | DateTime | Something else?
     */
    xTickFormat?: string | ((d: any) => string)
    /**
     * Optional: String format to use for Y-axis ticks, i.e., graph value. Defaults to abbreviations.
     */
    yTickFormat?: string | ((d: number) => string)
    /**
     * Optional: Domain for the X axis values, i.e., range to visualize.
     */
    xDomain?: Iterable<string> | undefined
    /**
     * Optional: Minimum value for the Y axis (used as the minimum for Y domain maximum).
     */
    yMinValue?: number
    /**
     * Optional: Function to format the tooltip content.
     */
    tooltipContent?: (datapoint: { instant: Date; value: number }) => string
  }>(),
  {
    interval: 'minute',
    xTickFormat: '%H:%M', // Default to 24h format.
    yTickFormat: (d: number) => abbreviateNumber(Number(d)) ?? '', // Format Y axis labels to abbreviations.
    xDomain: undefined,
    yMinValue: 10.0, // Y axis shows a scale of [0..10] by default.
    tooltipContent: undefined,
  }
)

// https://observablehq.com/plot/marks/bar
const plotOptions = computed((): PlotOptions => {
  const largestValue = props.data.reduce((previousValue, currentValue) =>
    currentValue.value > previousValue.value ? currentValue : previousValue
  ).value

  return {
    x: {
      interval: props.interval, // Interval between data points.
      label: null, // Hide axis label.
      tickFormat: props.xTickFormat,
      domain: props.xDomain,
    },
    y: {
      label: null, // Hide axis label.
      nice: true, // Round Y axis scale to nice numbers.
      domain: [0, Math.max(Math.ceil(largestValue), props.yMinValue)],
      tickFormat: props.yTickFormat,
    },
    marks: [
      barY(props.data, {
        x: 'instant',
        y: 'value',
        fill: (d, ndx) => (ndx === props.data.length - 1 ? '#ffcc80' : '#2d90dc'),
        rx: '.1rem',
      }),
      tip(
        props.data,
        pointerX({
          x: 'instant',
          y: 'value',
          title:
            props.tooltipContent ??
            ((datapoint: { instant: Date; value: number }): string => {
              return `${datapoint.value}\n${datapoint.instant.toUTCString()}`
            }),
          stroke: '#d4d4d4',
          fontStyle: 'red',
          fill: '#fafafa',
          fontSize: 11,
        })
      ),
    ],
  }
})
</script>

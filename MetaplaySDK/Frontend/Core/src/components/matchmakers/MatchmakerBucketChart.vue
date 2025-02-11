<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  title="Bucket Distribution"
  subtitle="Distribution of participants in the matchmaker buckets compared to available capacity."
  :is-loading="isLoadingData"
  data-testid="matchmaker-bucket-chart"
  )
  div(
    v-if="singleMatchmakerData?.data.hasFinishedBucketUpdate"
    class="tw-grid tw-grid-cols-1 tw-gap-3 @sm:tw-grid-cols-2 @md:tw-grid-cols-3 @xl:tw-grid-cols-4"
    )
    //- If the matchmaker buckets have labels, show an input for each one of them.
    MInputSingleSelectDropdown(
      v-for="labelInfo in labelInfos"
      :key="labelInfo.$type"
      :label="labelInfo.displayName"
      :options="labelInfo.options"
      :model-value="selectedLabelOptions[labelInfo.displayName] ?? 'All'"
      @update:modelValue="(selectedValue) => onLabelSelected(labelInfo.typeName, selectedValue)"
      )

  MPlot(
    v-if="chartOptions"
    :options="chartOptions"
    )

  div(
    v-else
    class="tw-my-10 tw-text-center tw-text-neutral-400"
    ) No data to show!
</template>

<script lang="ts" setup>
import { computed, ref } from 'vue'

import { MCard, MInputSingleSelectDropdown, MPlot } from '@metaplay/meta-ui-next'
import { abbreviateNumber, maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { barY, pointerX, tip, type PlotOptions } from '@observablehq/plot'

import { getSingleMatchmakerSubscriptionOptions } from '../../subscription_options/matchmaking'

const props = defineProps<{
  matchmakerId: string
}>()

/**
 * Subscribe to the data of a single matchmaker based on its id.
 */
const { data: singleMatchmakerData } = useSubscription(getSingleMatchmakerSubscriptionOptions(props.matchmakerId))
const isLoadingData = computed(() => !singleMatchmakerData.value?.data.hasFinishedBucketUpdate)

const labelInfos = computed(
  () =>
    singleMatchmakerData.value?.data.labelInfos.map((labelInfo) => ({
      ...labelInfo,
      options: [
        { label: 'Not filtered', value: 'All' },
        ...labelInfo.options.map((option) => ({ label: option, value: option })),
      ],
    })) ?? []
)

const selectedLabelOptions = ref<Record<string, string>>({})

function onLabelSelected(labelType: string, selectedValue: string): void {
  if (selectedValue === 'All') {
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
    delete selectedLabelOptions.value[labelType]
    return
  }
  selectedLabelOptions.value[labelType] = selectedValue
}

interface ParticipantData {
  bucket: string
  value: number
  labels: Array<{
    [key: string]: unknown
    $type: string
    dashboardLabel: string
  }>
  fill: string
  fillPercentage: number
}

interface CapacityData {
  bucket: string
  value: number
  labels: Array<{
    [key: string]: unknown
    $type: string
    dashboardLabel: string
  }>
}

// https://observablehq.com/plot/marks/bar
/**
 * Chart data for the bucket distribution of the matchmaker.
 */
const chartOptions = computed((): PlotOptions | undefined => {
  if (!singleMatchmakerData.value || singleMatchmakerData.value.data.bucketInfos.length === 0) {
    return undefined
  }
  const buckets = singleMatchmakerData.value.data.bucketInfos

  let participantData: ParticipantData[] = buckets.map((bucket) => ({
    bucket: bucket.labels[0].dashboardLabel,
    value: bucket.numPlayers,
    labels: [...bucket.labels],
    fill: '#2d90dc',
    fillPercentage: bucket.fillPercentage,
  }))

  let capacityData: CapacityData[] = buckets.map((bucket) => ({
    bucket: bucket.labels[0].dashboardLabel,
    labels: [...bucket.labels],
    value: bucket.capacity,
  }))

  // If the user has selected a label, filter the data to only include buckets that matches ALL of the selected labels.
  if (Object.keys(selectedLabelOptions.value).length > 0) {
    participantData = participantData.filter((data) => {
      return Object.entries(selectedLabelOptions.value).every(([labelType, selectedValue]) => {
        return data.labels.some((label) => label.$type === labelType && label.dashboardLabel === selectedValue)
      })
    })

    capacityData = capacityData.filter((data) => {
      return Object.entries(selectedLabelOptions.value).every(([labelType, selectedValue]) => {
        return data.labels.some((label) => label.$type === labelType && label.dashboardLabel === selectedValue)
      })
    })
  } else {
    // If no label is selected, merge all buckets that have the same label.
    participantData = participantData.reduce<ParticipantData[]>((acc, current) => {
      const existing = acc.find((item) => item.bucket === current.bucket)
      if (existing) {
        existing.value += current.value
        existing.labels.concat(current.labels.slice(1))
      } else {
        acc.push(current)
      }
      return acc
    }, [])

    capacityData = capacityData.reduce<CapacityData[]>((acc, current) => {
      const existing = acc.find((item) => item.bucket === current.bucket)
      if (existing) {
        existing.value += current.value
        existing.labels.concat(current.labels.slice(1))
      } else {
        acc.push(current)
      }
      return acc
    }, [])
  }

  if (participantData.length === 0 && capacityData.length === 0) {
    return undefined
  }

  return {
    x: {
      label: null, // Hide axis label.
      tickRotate: -45, // Rotate X axis labels for better readability.
    },
    y: {
      label: null, // Hide axis label.
      nice: true, // Round Y axis scale to nice numbers.
      tickFormat: (d) => abbreviateNumber(Number(d)), // Format Y axis labels to abbreviations.
    },
    marks: [
      // Two overlapping bars. Could also do stacked bars, but this way we get rounded corners easier.
      barY(capacityData, {
        x: 'bucket',
        y: 'value',
        fill: '#d8d8d8',
        rx: '.4rem',
        sort: { x: 'y', order: null },
      }),
      barY(participantData, {
        x: 'bucket',
        y: 'value',
        fill: '#2d90dc',
        rx: '.4rem',
        sort: { x: 'y', order: null },
      }),
      tip(
        participantData,
        pointerX({
          x: 'bucket',
          y: 'value',
          title: getTooltipContent,
          stroke: '#d4d4d4',
          fontStyle: 'red',
          fill: '#fafafa',
          fontSize: 11,
        })
      ),
    ],
    marginBottom: 50, // Add some margin to the bottom of the chart to make room for the X axis labels.
  }
})

function getTooltipContent(dataPoint: ParticipantData): string {
  return `Bucket ${dataPoint.labels.map((label) => label.dashboardLabel).join('\n')}

${maybePluralString(dataPoint.value, 'participant', true)}
${Math.round(dataPoint.fillPercentage * 1000000) / 10000}% full`
}
</script>

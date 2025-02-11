import { onMounted, onUnmounted, ref } from 'vue'

import type { Meta, StoryObj } from '@storybook/vue3'

import MTimeseriesBarChart from './MTimeseriesBarChart.vue'

const meta: Meta<typeof MTimeseriesBarChart> = {
  component: MTimeseriesBarChart,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'The MTimeseriesBarChart component is a convenience wrapper around MPlot to display a timeseries bar chart. It is designed to work with timeseries data, where each data point has a timestamp and a value.',
      },
    },
  },
  render: (args) => ({
    components: { MTimeseriesBarChart },
    setup: () => ({ args }),
    template: `
    <MTimeseriesBarChart v-bind="args"></MTimeseriesBarChart>
    `,
  }),
}

function seededRandom(seed = 1): () => number {
  let value = seed

  return function () {
    value = (value * 16807) % 2147483647
    return value / 2147483647
  }
}

// Simple Perlin-noise like multi-octave noise.
function noise1D(offset: number): number {
  return (
    Math.sin(offset * 1.0 + 0.3) / 1.0 +
    Math.sin(offset * 2.0 + 0.1) / 2.0 +
    Math.sin(offset * 4.0 - 0.9) / 4.0 +
    Math.sin(offset * 8.0 + 0.7) / 8.0 +
    Math.sin(offset * 16.0 + 0.2) / 16.0
  )
}

/**
 * Helper to create a dataset with a given number of samples, starting at a given time, with a given interval in minutes.
 * @param count How many samples to generate.
 * @param startInstant The start time of the dataset.
 * @param intervalInMinutes The interval between samples.
 * @returns An array of samples, each with a JS `Date` and a value.
 */
function generateDataset(
  count: number,
  startInstant: Date,
  intervalInMinutes: number
): Array<{ instant: Date; value: number }> {
  const samples = []
  for (let i = 0; i < count; i++) {
    samples.push({
      instant: new Date(startInstant.getTime() + i * intervalInMinutes * 60000),
      value: 50 + Math.floor(30 * noise1D(i * 0.05) + 9),
    })
  }
  return samples
}

// const concurrentsDataset = generateDataset(100, new Date('2021-01-01'), 86400) // daily
const concurrentsDataset = generateDataset(40, new Date('2021-01-01T18:00:00Z'), 1) // by minute

export default meta
type Story = StoryObj<typeof MTimeseriesBarChart>

/**
 * A simple timeseries bar chart with some random data.
 */
export const Default: Story = {
  args: {
    data: concurrentsDataset,
  },
}

/**
 * An empty dataset renders nothing.
 */
export const Empty: Story = {
  args: {
    data: [],
  },
}

/**
 * A dataset with all zeros renders a chart with no bars.
 */
export const AllZeros: Story = {
  args: {
    data: concurrentsDataset.map((d) => ({ ...d, value: 0 })),
  },
}

/**
 * An example with both zero and non-zero values.
 */
export const HalfZeros: Story = {
  args: {
    data: concurrentsDataset.map((d, i) => ({ ...d, value: i % 2 === 0 ? 0 : d.value })),
  },
}

const randomData = []
const random = seededRandom()
for (let i = 0; i < 40; i++) {
  randomData.push({
    instant: new Date(new Date('2021-01-01T18:00:00Z').getTime() + i * 60000),
    value: Math.floor(random() * 4),
  })
}

/**
 * An example with very low values. Note how the Y scale always goes to at least 10 to not zoom the chart too much.
 */
export const AllValuesMax3: Story = {
  args: {
    data: randomData,
  },
}

/**
 * A dataset with values that can to billions.
 */
export const VeryLargeValues: Story = {
  args: {
    data: concurrentsDataset.map((d) => ({ ...d, value: d.value * 10000000 })),
  },
}

/**
 * An example of using a custom tooltip formatter.
 */
export const CustomTooltip: Story = {
  args: {
    data: concurrentsDataset,
    tooltipContent: (d: { instant: Date; value: number }) => `Value is ${d.value}`,
  },
}

/**
 * The chart re-renders automatically when the data changes.
 */
export const AutoUpdate: Story = {
  render: () => ({
    components: {
      MTimeseriesBarChart,
    },
    setup: () => {
      let timer: ReturnType<typeof setTimeout> | undefined

      const data = ref(generateDataset(40, new Date('2021-01-01T18:00:00Z'), 1))
      let tick = 0

      function updateData() {
        tick++

        // Remove first element and add a new one
        data.value = data.value.slice(1).concat({
          instant: new Date(data.value[data.value.length - 1].instant.getTime() + 60000),
          value: 50 + Math.floor(30 * noise1D((data.value.length + tick) * 0.05) + 9),
        })

        timer = setTimeout(() => {
          updateData()
        }, 2000)
      }

      onMounted(() => {
        updateData()
      })
      onUnmounted(() => {
        if (timer) {
          clearInterval(timer)
        }
      })

      return {
        data,
      }
    },
    template: `
    <MTimeseriesBarChart :data="data"></MTimeseriesBarChart>
    `,
  }),
}

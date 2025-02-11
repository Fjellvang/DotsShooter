import { computed, onMounted, onUnmounted, ref } from 'vue'

import { barY, rectY } from '@observablehq/plot'
import type { Meta, StoryObj } from '@storybook/vue3'

import MPlot from './MPlot.vue'

const meta: Meta<typeof MPlot> = {
  component: MPlot,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'The MPlot component is a wrapper around the Observable Plot library. It allows you to create a variety of plots and charts with a simple API.',
      },
    },
  },
  render: (args) => ({
    components: { MPlot },
    setup: () => ({ args }),
    template: `
    <MPlot v-bind="args"></MPlot>
    `,
  }),
}

export default meta
type Story = StoryObj<typeof MPlot>

/**
 * At it's simplest, the MPlot component can be used to display a chart with a single series of data. It resizes automatically to fit its container.
 */
export const Default: Story = {
  args: {
    options: {
      x: {
        type: 'band',
      },
      marks: [
        rectY(
          [
            {
              date: new Date('2021-01-01T12:00:00Z'),
              value: 10,
            },
            {
              date: new Date('2021-01-01T12:01:00Z'),
              value: 20,
            },
            {
              date: new Date('2021-01-01T12:02:00Z'),
              value: 30,
            },
            {
              date: new Date('2021-01-01T12:03:00Z'),
              value: 40,
            },
            {
              date: new Date('2021-01-01T12:04:00Z'),
              value: 50,
            },
            {
              date: new Date('2021-01-01T12:05:00Z'),
              value: 60,
            },
          ],
          {
            x: 'date',
            y: 'value',
            fill: '#2d90dc',
            rx: '.4rem',
          }
        ),
      ],
    },
  },
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
const concurrentsDataset = generateDataset(40, new Date('2021-01-01'), 1) // by minute

/**
 * MPlot is reactive and will re-render automatically when the data changes.
 */
export const AutoUpdate: Story = {
  render: () => ({
    components: {
      MPlot,
    },
    setup: () => {
      let timer: ReturnType<typeof setTimeout> | undefined

      const data = ref(concurrentsDataset)
      const random = seededRandom()

      function updateData() {
        data.value = data.value.map((d) => ({
          instant: d.instant,
          value: Math.floor(random() * 10),
        }))
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

      const options = computed(() => {
        return {
          x: {
            type: 'band',
          },
          marks: [
            barY(data.value, {
              x: 'instant',
              y: 'value',
              fill: '#2d90dc',
              rx: '.4rem',
            }),
          ],
        }
      })

      return {
        options,
      }
    },
    template: `
    <MPlot :options="options"></MPlot>
    `,
  }),
}

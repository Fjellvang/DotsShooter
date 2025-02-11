import { Settings, DateTime } from 'luxon'

import type { Preview } from '@storybook/vue3'

import '../src/assets/style.css'
import { usePermissions } from '../src/composables/usePermissions'

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/,
      },
    },
    viewport: {
      viewports: {
        threeColumn: {
          name: 'Wide Screen',
          styles: { width: '1536px', height: 'responsive' },
          type: 'desktop',
        },
        twoColumn: {
          name: 'Desktop',
          styles: { width: '1024px', height: 'responsive' },
          type: 'desktop',
        },
        singleColumn: {
          name: 'Single Column',
          styles: { width: '512px', height: 'responsive' },
          type: 'desktop',
        },
        tablet: {
          name: 'Tablet',
          styles: { width: '768px', height: '700px' },
          type: 'tablet',
        },
        mobile: {
          name: 'Mobile',
          styles: { width: '400px', height: '700px' },
          type: 'mobile',
        },
      },
    },
    backgrounds: {
      disable: true,
    },
    options: {
      storySort: {
        order: ['Welcome', 'Getting Started', 'Component Libraries', 'primitives', 'inputs', 'composites', '*'],
      },
    },
  },
}

// Mock the current date and time to make visual tests predictable.
const expectedNow = DateTime.fromISO('2000-01-01T00:00:00.000Z')
Settings.now = (): number => expectedNow.toMillis()

const { setPermissions } = usePermissions()
setPermissions([])

export default preview

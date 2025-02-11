import TailwindPlugin from '@metaplay/tailwind-plugin'

import TailwindContainerQueries from '@tailwindcss/container-queries'
import TailwindForms from '@tailwindcss/forms'

/** @type {import('tailwindcss').Config} */
export default {
  content: ['index.html', 'src/**/*.{vue,js,ts,jsx,tsx}', '.storybook/**/*.{ts,html}'],
  plugins: [TailwindForms, TailwindContainerQueries, TailwindPlugin],
}

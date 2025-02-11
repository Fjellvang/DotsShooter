import type { StorybookConfig } from '@storybook/vue3-vite'

const config: StorybookConfig = {
  stories: ['../src/**/*.mdx', '../src/**/*.stories.@(js|jsx|ts|tsx)'],
  addons: [
    '@storybook/addon-links',
    '@storybook/addon-essentials',
    '@storybook/addon-interactions',
    '@chromatic-com/storybook',
  ],
  framework: {
    name: '@storybook/vue3-vite',
    options: {},
  },
  docs: {
    defaultName: 'Overview',
  },
  async viteFinal(config) {
    // Merge custom configuration into the default config
    const { mergeConfig } = await import('vite')

    return mergeConfig(config, {
      server: {
        hmr: {
          clientPort: process.env.CODESPACES ? 443 : undefined, // <== this is the magic
        },
      },
    })
  },
  staticDirs: ['../public', '../src/assets'],
}

export default config

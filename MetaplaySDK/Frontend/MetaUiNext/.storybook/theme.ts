import type { ThemeVars } from '@storybook/theming'
import { create } from '@storybook/theming/create'

const theme: ThemeVars = create({
  // Metaplay branding
  brandTitle: 'Metaplay UI Library',
  brandImage: './metaplay_logo.png',
  brandTarget: '_self',

  // Theme base
  base: 'light',
  // Typography
  fontBase: 'ui-sans-serif, system-ui',
  fontCode: 'monospace',

  // UI Background
  appBg: '#f6f6f7',
  appContentBg: '#ffffff',

  // Colors
  colorPrimary: '#f5f5f5',
  colorSecondary: '#3f6730',

  // Text colors
  textColor: '#10162F',
  textInverseColor: '#ffffff',

  // Toolbar default and active colors
  barTextColor: '#9E9E9E',
  barSelectedColor: '#585C6D',
  barHoverColor: '#585C6D',
  barBg: '#ffffff',

  // Form colors
  inputBg: '#ffffff',
  inputBorder: '#10162F',
  inputTextColor: '#10162F',
  inputBorderRadius: 2,
})

export default theme

/**
 * @file Eslint (flat) configuration for Metaplay SDK.
 *
 * Variant of the base configs with competing formatting rules removed but no other changes.
 */

import BaseConfig from './baseConfig.js'
import EslintPluginPrettierRecommended from 'eslint-plugin-prettier/recommended'

export default [
  ...BaseConfig,
  // Use the Prettier ESLint plugin to both disable eslint formatting rules and enable Prettier formatting rules.
  EslintPluginPrettierRecommended,
  {
    rules: {
      // Set formatting rules to warnings instead of the default errors.
      'prettier/prettier': 'warn',
    },
  },
]

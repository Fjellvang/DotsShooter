/**
 * @file Eslint (flat) configuration for Metaplay SDK.
 *
 * Variant of the base configs with competing formatting rules removed but no other changes.
 */

import BaseConfig from './baseConfig.js'
import EslintConfigPrettier from 'eslint-config-prettier'

export default [
  ...BaseConfig,
  // Use the Prettier ESLint config to prevent conflicts with Prettier.
  // This basically disables all eslint formatting rules without actually enabling Prettier formatting rules.
  EslintConfigPrettier,
]

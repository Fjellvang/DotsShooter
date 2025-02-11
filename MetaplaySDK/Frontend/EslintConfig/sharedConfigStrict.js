/**
 * @file Eslint (flat) configuration for Metaplay SDK.
 *
 * Variant of the recommended config with more rules enabled for stricter linting thatn what is possible in complex Vue projects.
 * Specifically, the `any` type is not allowed.
 */

import BaseConfig from './baseConfig.js'
import EslintPluginPrettierRecommended from 'eslint-plugin-prettier/recommended'
import { parser as tsParser } from 'typescript-eslint'

export default [
  ...BaseConfig,
  // Use the Prettier ESLint plugin to both disable eslint formatting rules and enable Prettier formatting rules.
  EslintPluginPrettierRecommended,
  {
    files: ['**/*.ts'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        projectService: ['tsconfig*.json'],
      },
    },
    rules: {
      // Re-enable some rules that are disabled in the recommended config.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-unsafe-return': 'error',
      '@typescript-eslint/no-unsafe-call': 'error',
      '@typescript-eslint/no-unsafe-assignment': 'error',
      '@typescript-eslint/no-unsafe-member-access': 'error',
      '@typescript-eslint/no-unnecessary-condition': 'error',
      'eslint-comments/require-description': 'error',
      // Set formatting rules to warnings instead of the default errors.
      'prettier/prettier': 'warn',
    },
  },
]

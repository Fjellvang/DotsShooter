import jsdoc from 'eslint-plugin-jsdoc'

import MetaplayEslintConfig from '@metaplay/eslint-config/strict'

export default [
  ...MetaplayEslintConfig,
  jsdoc.configs['flat/recommended-typescript'],
  {
    rules: {
      'jsdoc/require-example': 'warn',
      'jsdoc/require-description': 'warn',
      'jsdoc/require-throws': 'warn',
    },
  },
]

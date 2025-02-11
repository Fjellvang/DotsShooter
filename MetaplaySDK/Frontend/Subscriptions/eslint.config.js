import MetaplayEslintConfig from '@metaplay/eslint-config/strict'

export default [
  ...MetaplayEslintConfig,
  {
    files: ['**/*.ts'],
    rules: {
      'eslint-comments/require-description': 'off',
    },
  },
]

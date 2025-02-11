import MetaplayEslintConfig from '@metaplay/eslint-config/strict'

export default [
  ...MetaplayEslintConfig,
  {
    files: ['**/*.stories.ts'],
    rules: {
      '@typescript-eslint/explicit-function-return-type': 'off',
      '@typescript-eslint/no-unsafe-assignment': 'off',
      '@typescript-eslint/no-unsafe-member-access': 'off',
      '@typescript-eslint/no-unsafe-return': 'off',
    },
  },
  {
    files: ['**/*.ts'],
    rules: {
      'eslint-comments/require-description': 'off',
    },
  },
]

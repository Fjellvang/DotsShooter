/**
 * @file Eslint (flat) configuration for Metaplay SDK.
 *
 * The intention is to pre-select highly opinionated rules that cover JS, TS and Vue files.
 * The rules should prevent subtle errors at the cost of verbosity to protect customer code.
 */

import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import eslintConfigLove from 'eslint-config-love'
import { parser as tsParser } from 'typescript-eslint'

/**
 * Use the recommended ESLint config for all JS files.
 */
const jsConfig = {
  files: ['**/*.js', '**/*.cjs', '**/*.mjs'],
  rules: js.configs.recommended.rules,
}

/**
 * We cherry pick the plugins and rules from the ESLint Config Love package.
 * This leaves the parser options to our control, as we want to set them for our project structure.
 */
const tsConfig = {
  files: ['**/*.vue', '**/*.ts'],
  languageOptions: {
    parser: tsParser,
    parserOptions: {
      projectService: ['tsconfig*.json'],
      extraFileExtensions: ['.vue'], // Holy shit, leaving this out makes the parser flaky on .vue files?!
    },
  },
  plugins: eslintConfigLove.plugins,
  rules: {
    ...eslintConfigLove.rules,
    // Metaplay's customization to rules coming from ESLint Config Love.
    '@typescript-eslint/init-declarations': 'off', // We have cases where it is better to assign variables within conditional logic but have the variable declared in outside scope.
    '@typescript-eslint/no-explicit-any': 'off', // We have lots of cases where we need to use any because we don't have types for all data.
    '@typescript-eslint/strict-boolean-expressions': 'off', // Non-strict nullable checks are a really common and legible pattern we use. Too much work to refactor.
    '@typescript-eslint/consistent-type-imports': 'off', // This is too noisy and doesn't take into consideration Vue templates.
    '@typescript-eslint/triple-slash-reference': 'off', // Vite client types don't seem to play ball with project references unless this is used.
    '@typescript-eslint/class-methods-use-this': 'off', // We use this pattern.
    '@typescript-eslint/no-magic-numbers': 'off', // We use magic numbers in timers and counters.
    '@typescript-eslint/explicit-function-return-type': 'warn', // Useful while developing.
    '@typescript-eslint/no-unused-vars': 'warn', // Useful while developing.
    '@typescript-eslint/no-unsafe-return': 'off', // Dynamic component loading in Vue is always unsafe. Should selectively enable this rule for non-Vue projects.
    '@typescript-eslint/no-unsafe-call': 'off', // Existing customer code will have unsafe calls. Should selectively enable this rule for non-Vue projects.
    '@typescript-eslint/no-unsafe-assignment': 'off', // Existing customer code will have unsafe assignments. Should selectively enable this rule for non-Vue projects.
    '@typescript-eslint/no-unsafe-member-access': 'off', // Existing customer code will have unsafe member access. Should selectively enable this rule for non-Vue projects.
    '@typescript-eslint/no-unnecessary-condition': 'off', // We have cases where we need to check for null or undefined. Should selectively enable this rule for non-Vue projects.
    '@typescript-eslint/prefer-destructuring': 'off', // Destructured assignments are not always as legible as direct assignments.
    'eslint-comments/require-description': 'off', // Documentation issue only, does not directly affect code quality. Too much work to fix all cases.
  },
}

/**
 * Declare that .vue files should use the TS parser.
 */
const vueConfigTsParserOverride = {
  files: ['**/*.vue'],
  languageOptions: {
    // parser: tsParser, <- BAD EXAMPLE: doing this would override the Vue parser and break Vue template parsing.
    parserOptions: {
      parser: tsParser, // <- This is the correct way to set the parser for the <script> block of Vue templates.
      extraFileExtensions: ['.vue'],
      projectService: ['tsconfig*.json'],
    },
  },
  rules: {
    '@typescript-eslint/no-unused-vars': 'off', // This does not look at Vue templates.
  },
}

/**
 * List of common ignores that should be applied to all configs. Node modules and .git are ignored by default and this list is additive.
 * https://eslint.org/docs/latest/use/configure/ignore
 */
const globalIgnores = {
  ignores: [
    'dist/', // We assume that the dist folder is always generated and should not be linted.
    '.vercel/', // We assume that the .vercel folder is always generated and should not be linted.
    '.nuxt/', // We assume that the .nuxt folder is always generated and should not be linted.
    '.nitro/', // We assume that the .nitro folder is always generated and should not be linted.
    '.output/', // We assume that the .output folder is always generated and should not be linted.
  ],
}

// Apply all our rules by putting them into a list. The order matters!
export default [
  jsConfig,
  tsConfig,
  // Use the Vue recommended ESLint config...
  ...pluginVue.configs['flat/recommended'],
  // ...and override .vue files to use the TypeScript parser because we use TS in them.
  vueConfigTsParserOverride,
  globalIgnores,
]

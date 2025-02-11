/* eslint-disable */

/**
 * @see https://prettier.io/docs/en/configuration.html
 * @type {import("prettier").Config}
 */
const config = {
  trailingComma: 'es5',
  semi: false,
  singleQuote: true,
  printWidth: 120,

  plugins: ['@prettier/plugin-pug', 'prettier-plugin-tailwindcss', '@trivago/prettier-plugin-sort-imports'],
  pugAttributeSeparator: 'none',
  pugWrapAttributesThreshold: 1,
  pugClosingBracketIndentDepth: 1,
  pugFramework: 'vue',
  pugEmptyAttributes: 'as-is',
  pugClassNotation: 'attribute',
  pugIdNotation: 'as-is',
  pugExplicitDiv: true,
  pugSingleQuote: false,
  pugSortAttributesBeginning: [
    '^v-else$',
    '^v-if$',
    '^v-else-if$',
    '^v-for$',
    '^:key$',
    '^v-on$',
    '^v-bind$',
    '^ref$',
    '^v-model',
    '^:?src$',
    // '^:model-value',
    // '^@update:model-value',
  ],
  pugSortAttributesEnd: ['^target$', '^@click', '^@', '^:?data-'],

  importOrder: ['<THIRD_PARTY_MODULES>', '^@metaplay/(.*)$', '^[@/]', '^[~/]', '^[./]'],
  importOrderSeparation: true,
}

export default config

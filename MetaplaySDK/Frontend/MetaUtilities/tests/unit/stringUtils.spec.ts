// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { describe, expect, test } from 'vitest'

import {
  camelCaseToSentenceCase,
  camelCaseToTitleCase,
  stringToSentenceCase,
  stringToTitleCase,
  maybePluralString,
  maybePluralPrefixString,
  toOrdinalString,
  sentenceCaseToKebabCase,
  camelCaseToKebabCase,
} from '../../src/stringUtils'

describe('camelCaseToSentenceCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'lowerCamelCase', expected: 'Lower camel case' },
    { input: 'UpperCamelCase', expected: 'Upper camel case' },
    {
      input: 'camelCaseWithHttpAcronym',
      expected: 'Camel case with HTTP acronym',
    },
    {
      input: 'this is not camel case',
      expectedError: new Error('Input string cannot contain spaces'),
    },
  ]

  testCases.forEach(({ input, expected, expectedError }) => {
    test(`Should return '${expected}' when input is '${input}'`, () => {
      if (expectedError) {
        expect(() => camelCaseToSentenceCase(input)).toThrowError(expectedError)
      } else {
        expect(camelCaseToSentenceCase(input)).toEqual(expected)
      }
    })
  })
})

describe('camelCaseToTitleCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'lowerCamelCase', expected: 'Lower Camel Case' },
    { input: 'UpperCamelCase', expected: 'Upper Camel Case' },
    {
      input: 'camelCaseWithHttpAcronym',
      expected: 'Camel Case with HTTP Acronym',
    },
    {
      input: 'this is not camel case',
      expectedError: new Error('Input string cannot contain spaces'),
    },
  ]

  testCases.forEach(({ input, expected, expectedError }) => {
    test(`Should return '${expected}' when input is '${input}'`, () => {
      if (expectedError) {
        expect(() => camelCaseToTitleCase(input)).toThrowError(expectedError)
      } else {
        expect(camelCaseToTitleCase(input)).toEqual(expected)
      }
    })
  })
})

describe('stringToSentenceCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'sentence case', expected: 'Sentence case' },
    { input: 'SeNtEnCe CaSe', expected: 'Sentence case' },
    {
      input: 'keep special acronyms like id and http capitalized',
      expected: 'Keep special acronyms like ID and HTTP capitalized',
    },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should return '${expected}' when input is '${input}'`, () => {
      expect(stringToSentenceCase(input)).toEqual(expected)
    })
  })
})

describe('stringToTitleCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'title case', expected: 'Title Case' },
    { input: 'TiTlE cAsE', expected: 'Title Case' },
    {
      input: 'keep the small words in lower case',
      expected: 'Keep the Small Words in Lower Case',
    },
    {
      input: 'the small word at the start stays capitalized',
      expected: 'The Small Word at the Start Stays Capitalized',
    },
    {
      input: 'small word at the end stays capitalized near',
      expected: 'Small Word at the End Stays Capitalized Near',
    },
    {
      input: 'keep special acronyms like id and http capitalized',
      expected: 'Keep Special Acronyms Like ID and HTTP Capitalized',
    },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should return '${expected}' when input is '${input}'`, () => {
      expect(stringToTitleCase(input)).toEqual(expected)
    })
  })
})

describe('maybePluralString', () => {
  const testCases = [
    { count: 0, unit: 'result', showCount: false, expected: 'results' },
    { count: 1, unit: 'result', showCount: false, expected: 'result' },
    { count: 2, unit: 'result', showCount: false, expected: 'results' },
    { count: 0, unit: 'result', showCount: true, expected: '0 results' },
    { count: 1, unit: 'result', showCount: true, expected: '1 result' },
    { count: 2, unit: 'result', showCount: true, expected: '2 results' },
  ]

  testCases.forEach(({ count, unit, showCount, expected }) => {
    test(`Should return '${expected}' when input is '${count}, ${unit}, ${showCount}', `, () => {
      expect(maybePluralString(count, unit, showCount)).toEqual(expected)
    })
  })
})

describe('maybePluralPrefixString', () => {
  const testCases = [
    {
      count: 0,
      singularText: 'result',
      pluralText: 'results',
      expected: 'results',
    },
    {
      count: 1,
      singularText: 'result',
      pluralText: 'results',
      expected: 'result',
    },
    {
      count: 2,
      singularText: 'result',
      pluralText: 'results',
      expected: 'results',
    },
  ]

  testCases.forEach(({ count, singularText, pluralText, expected }) => {
    test(`Should return '${expected}' when input is '${count}, ${singularText}, ${pluralText}', `, () => {
      expect(maybePluralPrefixString(count, singularText, pluralText)).toEqual(expected)
    })
  })
})

describe('toOrdinalString', () => {
  const testCases = [
    { input: -1, expected: '-1' },
    { input: 0, expected: '0' },
    { input: 1, expected: '1st' },
    { input: 2, expected: '2nd' },
    { input: 3, expected: '3rd' },
    { input: 4, expected: '4th' },
    { input: 10, expected: '10th' },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should return '${expected}' when input is '${input}', `, () => {
      expect(toOrdinalString(input)).toEqual(expected)
    })
  })
})

describe('sentenceCaseToKebabCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'This is a test', expected: 'this-is-a-test' },
    {
      input: 'Is This Hyphened correctly',
      expected: 'is-this-hyphened-correctly',
    },
    {
      input: 'this should ALso be kebab case',
      expected: 'this-should-also-be-kebab-case',
    },
    { input: 'ALL caps', expected: 'all-caps' },
    { input: 'ThisIsA', expected: 'thisisa' },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should return '${expected}' when input is '${input}', `, () => {
      expect(sentenceCaseToKebabCase(input)).toEqual(expected)
    })
  })
})

describe('camelCaseToKebabCase', () => {
  const testCases = [
    { input: '', expected: '' },
    { input: 'componentName', expected: 'component-name' },
    {
      input: 'this is not camel case',
      expectedError: new Error('Input string cannot contain spaces'),
    },
  ]

  testCases.forEach(({ input, expected, expectedError }) => {
    test(`Should return '${expected}' when input is '${input}', `, () => {
      if (expectedError) {
        expect(() => camelCaseToKebabCase(input)).toThrowError(expectedError)
      } else {
        expect(camelCaseToKebabCase(input)).toEqual(expected)
      }
    })
  })
})

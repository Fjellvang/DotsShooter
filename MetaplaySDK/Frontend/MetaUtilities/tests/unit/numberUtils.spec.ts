// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { describe, expect, test } from 'vitest'

import { abbreviateNumber } from '../../src/numberUtils'

describe('abbreviateNumber', () => {
  const testCases = [
    { input: 12, expected: '12' },
    { input: 123, expected: '123' },
    { input: 1234, expected: '1.23k' },
    { input: 12345, expected: '12.3k' },
    { input: 123456, expected: '123k' },
    { input: 1234567, expected: '1.23M' },
    { input: 12345678, expected: '12.3M' },
    { input: 123456789, expected: '123M' },
    { input: 1234567890, expected: '1.23B' },
    { input: 12345678901, expected: '12.3B' },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should return '${expected}' when input is '${input}'`, () => {
      expect(abbreviateNumber(input)).toEqual(expected)
    })
  })
})

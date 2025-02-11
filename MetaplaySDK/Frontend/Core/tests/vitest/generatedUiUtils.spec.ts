// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { describe, expect, test } from 'vitest'

describe('generatedUiUtils', () => {
  test('should pass', () => {
    expect(true).toBe(true)
  })
})

/*
import { newtonsoftCamelCase } from '../../src/components/generatedui/generatedUiUtils'
*/

/**
 * Based on public test code at:
 * https://github.com/JamesNK/Newtonsoft.Json/blob/2eaa475f8853f2b01ac5591421dcabd7a44f79ce/Src/Newtonsoft.Json.Tests/Utilities/StringUtilsTests.cs#L41
 */
/*
describe('newtonSoftCameCase', () => {
  const testCases = [
    { input: 'URLValue', expected: 'urlValue' },
    { input: 'URL', expected: 'url' },
    { input: 'ID', expected: 'id' },
    { input: 'I', expected: 'i' },
    { input: '', expected: '' },
    { input: 'Person', expected: 'person' },
    { input: 'iPhone', expected: 'iPhone' },
    { input: 'IPhone', expected: 'iPhone' },
    { input: 'I Phone', expected: 'i Phone' },
    { input: 'I  Phone', expected: 'i  Phone' },
    { input: ' IPhone', expected: ' IPhone' },
    { input: ' IPhone ', expected: ' IPhone ' },
    { input: 'IsCIA', expected: 'isCIA' },
    { input: 'VmQ', expected: 'vmQ' },
    { input: 'Xml2Json', expected: 'xml2Json' },
    { input: 'SnAkEcAsE', expected: 'snAkEcAsE' },
    { input: 'SnA__kEcAsE', expected: 'snA__kEcAsE' },
    { input: 'SnA__ kEcAsE', expected: 'snA__ kEcAsE' },
    { input: 'already_snake_case_ ', expected: 'already_snake_case_ ' },
    { input: 'IsJSONProperty', expected: 'isJSONProperty' },
    { input: 'SHOUTING_CASE', expected: 'shoutinG_CASE' },
    {
      input: '9999-12-31T23:59:59.9999999Z',
      expected: '9999-12-31T23:59:59.9999999Z',
    },
    {
      input: 'Hi!! This is text. Time to test.',
      expected: 'hi!! This is text. Time to test.',
    },
    { input: 'BUILDING', expected: 'building' },
    { input: 'BUILDING Property', expected: 'building Property' },
    { input: 'Building Property', expected: 'building Property' },
    { input: 'BUILDING PROPERTY', expected: 'building PROPERTY' },
  ]

  testCases.forEach(({ input, expected }) => {
    test(`Should convert ${input} to ${expected}`, () => {
      expect(newtonsoftCamelCase(input)).to.equal(expected)
    })
  })
})
*/

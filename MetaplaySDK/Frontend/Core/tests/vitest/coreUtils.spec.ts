// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { DateTime } from 'luxon'
import { describe, it, expect, test } from 'vitest'

import {
  isEpochTime,
  isValidEntityId,
  isValidPlayerId,
  extractMultipleValuesFromQueryString,
  extractSingleValueFromQueryStringOrUndefined,
  extractSingleValueFromQueryStringOrDefault,
  durationToMilliseconds,
  isNullOrUndefined,
  parseDotnetTimeSpanToLuxon,
} from '../../src/coreUtils'

describe('isValidEntityId', () => {
  it('should allow valid id', async ({ expect }) => {
    expect(isValidEntityId('EntityX:0000000000')).to.equal(true)
  })
  it('should not allow too long id', async ({ expect }) => {
    expect(isValidEntityId('EntityX:0000000000x')).to.equal(false)
  })
  it('should not allow too short id', async ({ expect }) => {
    expect(isValidEntityId('EntityX:000000000')).to.equal(false)
  })
  it('should not allow invalid characters', async ({ expect }) => {
    expect(isValidEntityId('EntityX:000000000I')).to.equal(false)
    expect(isValidEntityId('EntityX:0000000001')).to.equal(false)
    expect(isValidEntityId('EntityX:000000000l')).to.equal(false)
  })
  it('should not allow invalid id value', async ({ expect }) => {
    expect(isValidEntityId('EntityX:ZZZZZZZZZZ')).to.equal(false)
  })
})

describe('isValidPlayerId', () => {
  it('should allow valid player id', async ({ expect }) => {
    expect(isValidPlayerId('Player:0000000000')).to.equal(true)
  })
  it('should not allow valid guild id', async ({ expect }) => {
    expect(isValidPlayerId('Guild:0000000000')).to.equal(false)
  })
})
describe('extractMultipleValuesFromQueryString', () => {
  it('returns empty array when no values present', async ({ expect }) => {
    expect(extractMultipleValuesFromQueryString({}, 'key')).to.eql([])
  })
  it('returns empty array when no values does not exist', async ({ expect }) => {
    expect(extractMultipleValuesFromQueryString({ notKey: 'x' }, 'key')).to.eql([])
  })
  it('returns single value array value exists once', async ({ expect }) => {
    expect(extractMultipleValuesFromQueryString({ key: 'once' }, 'key')).to.eql(['once'])
  })
  it('returns multiple value array value exists more than once', async ({ expect }) => {
    expect(extractMultipleValuesFromQueryString({ key: ['once', 'twice'] }, 'key')).to.eql(['once', 'twice'])
  })
})

describe('extractSingleValueFromQueryStringOrUndefined', () => {
  it('returns undefined when value not present', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrUndefined({}, 'key')).to.equal(undefined)
  })
  it('returns value when when value present once', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrUndefined({ key: 'once' }, 'key')).to.equal('once')
  })
  it('returns first value when when value present more than once', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrUndefined({ key: ['once', 'twice'] }, 'key')).to.equal('once')
  })
})

describe('extractSingleValueFromQueryStringOrDefault', () => {
  it('returns default when value not present', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrDefault({}, 'key', 'default')).to.equal('default')
  })
  it('returns value when when value present once', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrDefault({ key: 'once' }, 'key', 'default')).to.equal('once')
  })
  it('returns first value when when value present more than once', async ({ expect }) => {
    expect(extractSingleValueFromQueryStringOrDefault({ key: ['once', 'twice'] }, 'key', 'default')).to.equal('once')
  })
})

describe('durationToMilliseconds', () => {
  it('should return the correct number of milliseconds for a given ISO duration', ({ expect }) => {
    const isoDuration = 'PT1M' // 1 Minute
    const expected = 60000 // 1 Minute = 60000 milliseconds
    expect(durationToMilliseconds(isoDuration)).to.equal(expected)
  })
})

describe('isEpochTime', () => {
  it('works with ISO time strings', async ({ expect }) => {
    expect(isEpochTime('1970-01-01T00:00:00Z')).to.deep.equal(true)
    expect(isEpochTime('2023-01-01T00:00:00Z')).to.deep.equal(false)
  })
  it('works with DateTime objects', async ({ expect }) => {
    expect(isEpochTime(DateTime.fromISO('1970-01-01T00:00:00Z'))).to.deep.equal(true)
    expect(isEpochTime(DateTime.fromISO('2023-01-01T00:00:00Z'))).to.deep.equal(false)
  })
  it('throws on bad data', async ({ expect }) => {
    expect(() => isEpochTime('bad time')).to.throw()
    // @ts-expect-error - Testing bad data.
    expect(() => isEpochTime({})).to.throw()
    // @ts-expect-error - Testing bad data.
    expect(() => isEpochTime(undefined)).to.throw()
    // @ts-expect-error - Testing bad data.
    expect(() => isEpochTime(12345)).to.throw()
  })
})

describe('isNullOrUndefined', () => {
  it('should return false for null', ({ expect }) => {
    const result = isNullOrUndefined(null)
    expect(result).to.equal(false)
  })

  it('should return false for undefined', ({ expect }) => {
    const result = isNullOrUndefined(undefined)
    expect(result).to.equal(false)
  })

  it('should return true for a non-null/undefined value', ({ expect }) => {
    const result = isNullOrUndefined('Hello')
    expect(result).to.equal(true)
  })

  it('should return true for 0', ({ expect }) => {
    const result = isNullOrUndefined(0)
    expect(result).to.equal(true)
  })
})

describe('parse faulty dotnet invariant culture format time spans to a Luxon Duration', () => {
  const testCases = [
    { input: '', expectedError: 'Input string cannot be empty' },
    {
      input: '00:00:00.12',
      expectedError: 'Milliseconds must be 7 digits long',
    },
    // { input: '::', expectedError: 'Invalid input format' }, this gets parsed as 00:00:00, does not throw error even when it should
    { input: 'asd', expectedError: 'Invalid unit value NaN' },
    { input: '00:00:a0', expectedError: 'Invalid unit value NaN' },
    { input: 'q1:w2:e3', expectedError: 'Invalid unit value NaN' },
    { input: '00:00:0a', expectedError: 'Invalid unit value NaN' },
    { input: '1q:2w:3e', expectedError: 'Invalid unit value NaN' },
    {
      input: '00:00:-1',
      expectedError: 'Negative integers are only supported at the beginning of the string',
    },
    {
      input: '-0:00:-1',
      expectedError: 'Negative integers are only supported at the beginning of the string',
    },
    {
      input: '1.01:01:-1',
      expectedError: 'Negative integers are only supported at the beginning of the string',
    },
  ]

  testCases.forEach(({ input, expectedError }) => {
    test(`should throw Error: "${expectedError}" when input is "${input}"`, () => {
      expect(() => parseDotnetTimeSpanToLuxon(input)).toThrow(expectedError)
    })
  })
})

describe('parse omitted date and millisecond dotnet invariant culture format time spans to a Luxon Duration', () => {
  const testCases = [
    { input: '00:00:01', expectedMillis: 1000 },
    { input: '-00:00:01', expectedMillis: -1000 },
    { input: '10:00:00', expectedMillis: 36000000 },
    { input: '00:01:00', expectedMillis: 60000 },
    { input: '00:00:00', expectedMillis: 0 },
  ]

  testCases.forEach(({ input, expectedMillis }) => {
    test(`should convert ${input} to a Luxon Duration of ${expectedMillis} milliseconds`, () => {
      const luxonDuration = parseDotnetTimeSpanToLuxon(input)
      expect(luxonDuration.toMillis()).to.equal(expectedMillis)
    })
  })
})

describe('parse omitted millisecond dotnet invariant culture format time spans to a Luxon Duration', () => {
  const testCases = [
    { input: '-1.00:00:00', expectedMillis: -86400000 },
    { input: '-1.00:00:01', expectedMillis: -86401000 },
    { input: '1.01:01:01', expectedMillis: 90061000 },
    { input: '7.00:00:00', expectedMillis: 604800000 },
    { input: '2.02:02:02', expectedMillis: 180122000 },
    { input: '3.03:03:03', expectedMillis: 270183000 },
  ]

  testCases.forEach(({ input, expectedMillis }) => {
    test(`should convert ${input} to a Luxon Duration of ${expectedMillis} milliseconds`, () => {
      const luxonDuration = parseDotnetTimeSpanToLuxon(input)
      expect(luxonDuration.toMillis()).to.equal(expectedMillis)
    })
  })
})

describe('parse omitted date dotnet invariant culture format time spans to a Luxon Duration', () => {
  const testCases = [
    { input: '10:10:10.1000000', expectedMillis: 36610100 },
    { input: '00:00:00.1230000', expectedMillis: 123 },
    { input: '00:00:00.1000000', expectedMillis: 100 },
    { input: '11:11:11.1110000', expectedMillis: 40271111 },
    { input: '12:12:12.1200000', expectedMillis: 43932120 },
  ]

  testCases.forEach(({ input, expectedMillis }) => {
    test(`should convert ${input} to a Luxon Duration of ${expectedMillis} milliseconds`, () => {
      const luxonDuration = parseDotnetTimeSpanToLuxon(input)
      expect(luxonDuration.toMillis()).to.equal(expectedMillis)
    })
  })
})

describe('parse full dotnet invariant culture format time span to a Luxon Duration', () => {
  const testCases = [
    { input: '0.00:00:00.1230000', expectedMillis: 123 },
    { input: '0.00:00:00.1000000', expectedMillis: 100 },
    { input: '7.00:00:00.1000000', expectedMillis: 604800100 },
    { input: '1.01:01:01.1000000', expectedMillis: 90061100 },
    { input: '2.02:02:02.2000000', expectedMillis: 180122200 },
  ]

  testCases.forEach(({ input, expectedMillis }) => {
    test(`should convert ${input} to a Luxon Duration of ${expectedMillis} milliseconds`, () => {
      const luxonDuration = parseDotnetTimeSpanToLuxon(input)
      expect(luxonDuration.toMillis()).to.equal(expectedMillis)
    })
  })
})

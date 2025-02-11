import { describe, expect, test } from 'vitest'

import { constructExactTimeCompactString, constructExactTimeVerboseString } from '../../src/utils/metaDurationUtils'

describe('Multiple units of time', () => {
  const testCases = [
    {
      input: 'P1Y1M1DT1H1M1.001S',
      expectedCompactString: '1y 1mo 1d 1h 1min 1s 1ms',
      expectedCompactStringHideMs: '1y 1mo 1d 1h 1min 1s',
      expectedVerboseString: '1 year 1 month 1 day 1 hour 1 minute 1 second and 1 millisecond',
      expectedVerboseStringHideMs: '1 year 1 month 1 day 1 hour 1 minute and 1 second',
    },
    {
      input: 'P1Y1M1DT1H1M1.001S',
      expectedCompactString: '1y 1mo 1d 1h 1min 1s 1ms',
      expectedCompactStringHideMs: '1y 1mo 1d 1h 1min 1s',
      expectedVerboseString: '1 year 1 month 1 day 1 hour 1 minute 1 second and 1 millisecond',
      expectedVerboseStringHideMs: '1 year 1 month 1 day 1 hour 1 minute and 1 second',
    },
    {
      input: 'P0Y1M1DT1H1M1.001S',
      expectedCompactString: '1mo 1d 1h 1min 1s 1ms',
      expectedCompactStringHideMs: '1mo 1d 1h 1min 1s',
      expectedVerboseString: '1 month 1 day 1 hour 1 minute 1 second and 1 millisecond',
      expectedVerboseStringHideMs: '1 month 1 day 1 hour 1 minute and 1 second',
    },
    {
      input: 'P0Y1M1DT1H1M1S',
      expectedCompactString: '1mo 1d 1h 1min 1s',
      expectedCompactStringHideMs: '1mo 1d 1h 1min 1s',
      expectedVerboseString: '1 month 1 day 1 hour 1 minute and 1 second',
      expectedVerboseStringHideMs: '1 month 1 day 1 hour 1 minute and 1 second',
    },
    {
      input: 'P0Y1M1DT1H1MS',
      expectedCompactString: '1mo 1d 1h 1min',
      expectedCompactStringHideMs: '1mo 1d 1h 1min',
      expectedVerboseString: '1 month 1 day 1 hour and 1 minute',
      expectedVerboseStringHideMs: '1 month 1 day 1 hour and 1 minute',
    },
  ]

  testCases.forEach(
    ({
      input,
      expectedCompactString,
      expectedCompactStringHideMs,
      expectedVerboseString,
      expectedVerboseStringHideMs,
    }) => {
      test(`constructExactTimeCompactString should return: ${expectedCompactString}`, () => {
        const result = constructExactTimeCompactString(input, false)
        expect(result).toBe(expectedCompactString)
      })

      test(`constructExactTimeCompactString with 'hideMilliseconds' should return: ${expectedCompactString}`, () => {
        const result = constructExactTimeCompactString(input, true)
        expect(result).toBe(expectedCompactStringHideMs)
      })

      test(`constructExactTimeVerboseString should return: ${expectedVerboseString}`, () => {
        const result = constructExactTimeVerboseString(input, false)
        expect(result).toBe(expectedVerboseString)
      })

      test(`constructExactTimeVerboseString with 'hideMilliseconds' should return: ${expectedVerboseString}`, () => {
        const result = constructExactTimeVerboseString(input, true)
        expect(result).toBe(expectedVerboseStringHideMs)
      })
    }
  )
})

describe('Singular unit of time', () => {
  const testCases = [
    {
      input: 'P1Y0M0DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '1 year',
      expectedVerboseString: '1 year 0 months',
    },
    {
      input: 'P0Y1M0DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '1 month',
      expectedVerboseString: '1 month 0 days',
    },
    {
      input: 'P0Y0M1DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '1 day',
      expectedVerboseString: '1 day 0 hours',
    },
    {
      input: 'P0Y0M0DT1H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '1 hour',
      expectedVerboseString: '1 hour 0 minutes',
    },
    {
      input: 'P0Y0M0DT0H1M0S',
      hideMilliseconds: false,
      expectedCompactString: '1 minute',
      expectedVerboseString: '1 minute 0 seconds',
    },
    {
      input: 'P0Y0M0DT0H0M1S',
      hideMilliseconds: false,
      expectedCompactString: '1 second',
      expectedVerboseString: '1 second 0 milliseconds',
    },
    {
      input: 'P0Y0M0DT0H0M1S',
      hideMilliseconds: true,
      expectedCompactString: '1 second',
      expectedVerboseString: '1 second',
    },
    {
      input: 'P0Y0M0DT0H0M0.001S',
      hideMilliseconds: false,
      expectedCompactString: '1 millisecond',
      expectedVerboseString: '1 millisecond',
    },
    {
      input: 'P0Y0M0DT0H0M0.001S',
      hideMilliseconds: true,
      expectedCompactString: '< 1 second',
      expectedVerboseString: '< 1 second',
    },
  ]

  testCases.forEach(({ input, hideMilliseconds, expectedCompactString, expectedVerboseString }) => {
    test(`constructExactTimeCompactString should return: ${expectedCompactString}`, () => {
      const result = constructExactTimeCompactString(input, hideMilliseconds)
      expect(result).toBe(expectedCompactString)
    })

    test(`constructExactTimeVerboseString should return: ${expectedVerboseString}`, () => {
      const result = constructExactTimeVerboseString(input, hideMilliseconds)
      expect(result).toBe(expectedVerboseString)
    })
  })
})

describe('Plural unit of time', () => {
  const testCases = [
    {
      input: 'P2Y0M0DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '2 years',
      expectedVerboseString: '2 years 0 months',
    },
    {
      input: 'P0Y11M0DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '11 months',
      expectedVerboseString: '11 months 0 days',
    },
    {
      input: 'P0Y0M30DT0H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '30 days',
      expectedVerboseString: '30 days 0 hours',
    },
    {
      input: 'P0Y0M0DT23H0M0S',
      hideMilliseconds: false,
      expectedCompactString: '23 hours',
      expectedVerboseString: '23 hours 0 minutes',
    },
    {
      input: 'P0Y0M0DT0H59M0S',
      hideMilliseconds: false,
      expectedCompactString: '59 minutes',
      expectedVerboseString: '59 minutes 0 seconds',
    },
    {
      input: 'P0Y0M0DT0H0M59S',
      hideMilliseconds: false,
      expectedCompactString: '59 seconds',
      expectedVerboseString: '59 seconds 0 milliseconds',
    },
    {
      input: 'P0Y0M0DT0H0M59S',
      hideMilliseconds: true,
      expectedCompactString: '59 seconds',
      expectedVerboseString: '59 seconds',
    },
  ]

  testCases.forEach(({ input, hideMilliseconds, expectedCompactString, expectedVerboseString }) => {
    test(`constructExactTimeCompactString should return: ${expectedCompactString}`, () => {
      const result = constructExactTimeCompactString(input, hideMilliseconds)
      expect(result).toBe(expectedCompactString)
    })

    test(`constructExactTimeVerboseString should return: ${expectedVerboseString}`, () => {
      const result = constructExactTimeVerboseString(input, hideMilliseconds)
      expect(result).toBe(expectedVerboseString)
    })
  })
})

// The extended format below comes from C# and when using Luxon they are by default converted to directly to only seconds in luxon.
describe('Plural unit of time in extended format that comes from backend C#', () => {
  const testCases = [
    {
      input: 'P1Y-1M0DT0H0M0S',
      expectedCompactString: '11 months',
      expectedVerboseString: '11 months 0 days',
    },
    {
      input: 'P0Y1M-1DT0H0M0S',
      expectedCompactString: '30 days',
      expectedVerboseString: '30 days 0 hours',
    },
    {
      input: 'P0Y0M1DT-1H0M0S',
      expectedCompactString: '23 hours',
      expectedVerboseString: '23 hours 0 minutes',
    },
    {
      input: 'P0Y0M0DT1H-1M0S',
      expectedCompactString: '59 minutes',
      expectedVerboseString: '59 minutes 0 seconds',
    },
    {
      input: 'P0Y0M0DT0H1M-1S',
      expectedCompactString: '59 seconds',
      expectedVerboseString: '59 seconds 0 milliseconds',
    },
  ]

  testCases.forEach(({ input, expectedCompactString, expectedVerboseString }) => {
    test(`constructExactTimeCompactString should return: ${expectedCompactString}`, () => {
      const result = constructExactTimeCompactString(input, false)
      expect(result).toBe(expectedCompactString)
    })

    test(`constructExactTimeVerboseString should return: ${expectedVerboseString}`, () => {
      const result = constructExactTimeVerboseString(input, false)
      expect(result).toBe(expectedVerboseString)
    })
  })
})

describe('Empty duration', () => {
  const testCases = [
    {
      input: 'P0Y0M0DT0H0M0S',
      expectedCompactString: '0 seconds',
      expectedVerboseString: '0 seconds',
    },
  ]

  testCases.forEach(({ input, expectedCompactString, expectedVerboseString }) => {
    test(`constructExactTimeCompactString should return: ${expectedCompactString}`, () => {
      const result = constructExactTimeCompactString(input, true)
      expect(result).toBe(expectedCompactString)
    })

    test(`constructExactTimeVerboseString should return: ${expectedVerboseString}`, () => {
      const result = constructExactTimeVerboseString(input, true)
      expect(result).toBe(expectedVerboseString)
    })
  })
})

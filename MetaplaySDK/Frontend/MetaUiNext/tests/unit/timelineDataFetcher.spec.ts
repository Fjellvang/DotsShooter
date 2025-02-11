// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { DateTime } from 'luxon'
import { beforeAll, describe, expect, test, vi } from 'vitest'

import type { TimelineItemDetails } from '../../src/unstable/timeline/MEventTimelineTypes'
import { TimelineDataFetcher, TimelineDataFetchHandler } from '../../src/unstable/timeline/timelineDataFetcher'

/**
 * A mock implementation of the `TimelineDataFetchHandler` that simply returns data with the provided range and no contents.
 */
class TimelineDataFetchHandlerTestMock extends TimelineDataFetchHandler {
  private readonly itemDetailsFetchDelayInMs: number

  constructor(itemDetailsFetchDelayInMs = 0) {
    super()
    this.itemDetailsFetchDelayInMs = itemDetailsFetchDelayInMs
  }
  requestTimelineData(firstInstant: DateTime, lastInstant: DateTime): void {
    this.setTimelineData({
      startInstantIsoString: firstInstant.toISO() ?? '??',
      endInstantIsoString: lastInstant.toISO() ?? '??',
      items: {},
    })
  }

  requestItemDetails(itemIds: string[]): void {
    const itemDetails: Record<string, TimelineItemDetails | undefined> = Object.fromEntries(
      itemIds.map((id) => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return [id, {} as any as TimelineItemDetails]
      })
    )

    if (this.itemDetailsFetchDelayInMs === 0) {
      this.updateItemDetails(itemDetails)
    } else {
      setTimeout(() => {
        this.updateItemDetails(itemDetails)
      }, this.itemDetailsFetchDelayInMs)
    }
  }
}

describe('TimelineDataFetcher', () => {
  beforeAll(() => {
    // We need to fake timers to be able to control time in tests.
    vi.useFakeTimers()
  })

  test('Constructor', () => {
    expect(() => {
      // eslint-disable-next-line no-new
      new TimelineDataFetcher(
        new TimelineDataFetchHandlerTestMock(),
        () => {
          // Empty.
        },
        () => {
          // Empty
        }
      )
    }).to.not.throw()
  })

  test('First data request is implicitly immediate', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    // Not setting the `immediate`` flag but this should result in an immediate request.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 1)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
  })

  test('Request data immediately obeys immediate request', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 1)
    fetcher.requestTimelineData(DateTime.fromISO('2020-02-01'), DateTime.fromISO('2020-02-02'), 1, true)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(2)
  })

  test('Request data correctly obeys debounce timeout', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 1)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    fetcher.requestTimelineData(DateTime.fromISO('2020-02-01'), DateTime.fromISO('2020-02-02'), 1)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    vi.advanceTimersByTime(1_000)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(2)
  })

  test('Request data correctly obeys multiple requests with debounce timeout', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 0)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    vi.advanceTimersByTime(500)
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-02'), DateTime.fromISO('2020-01-03'), 0)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    vi.advanceTimersByTime(500)
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-03'), DateTime.fromISO('2020-01-04'), 0)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    vi.advanceTimersByTime(500)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)
    vi.advanceTimersByTime(500)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(2)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-04'))
  })

  test('Request data obeys multiple, non-overlapping requests', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 0)
    fetcher.requestTimelineData(DateTime.fromISO('2020-02-01'), DateTime.fromISO('2020-02-02'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(2)
  })

  test('Does not fetch new data if window inside fetched buffer', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    // Request initial data - one week with a one week buffer either side.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-07'), 7)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)

    // Move request window by one day. Fetcher should not request new data. The window is still within the buffer.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-02'), DateTime.fromISO('2020-01-08'), 7, true)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(1)

    // Jump forward by one month. Fetcher should request new data. The window is outside the buffer.
    fetcher.requestTimelineData(DateTime.fromISO('2020-02-02'), DateTime.fromISO('2020-02-08'), 7, true)
    expect(spyRequestTimelineData).toHaveBeenCalledTimes(2)
  })

  test('Requested data includes previously fetched window', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    // Request initial data.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-15'), 0)

    // Request newer data to stretch the window forwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-15'), DateTime.fromISO('2020-01-20'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-20'))

    // Request older data to stretch the window backwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-10'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-20'))
  })

  test('Requested data includes previously fetched window within limits', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { largestAllowedTotalWindowInDays: 5, debounceTimeInMs: 1_000 }
    )

    // Request initial data.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-12'), 0)

    // Request newer data to stretch the window forwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-15'), DateTime.fromISO('2020-01-17'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-12'), DateTime.fromISO('2020-01-17'))

    // Request older data to stretch the window backwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-06'))
  })

  test('Fetched all request data if largestAllowedTotalWindowInDays too small', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const spyRequestTimelineData = vi.spyOn(fetchDataHandler, 'requestTimelineData')

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        // Empty.
      },
      () => {
        // Empty.
      },
      { largestAllowedTotalWindowInDays: 2, debounceTimeInMs: 1_000 }
    )

    // Request initial data.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-12'), 0)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-12'))

    // Request newer data to stretch the window forwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-15'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-10'), DateTime.fromISO('2020-01-15'))

    // // Request older data to stretch the window backwards.
    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-06'), 0, true)
    expect(spyRequestTimelineData).toHaveBeenCalledWith(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-06'))
  })
})

describe('TimelineDataFetchHandler', () => {
  beforeAll(() => {
    // We need to fake timers to be able to control time in tests.
    vi.useFakeTimers()
  })

  test('Data fetch handler updates data', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock()
    const fetchHandlerUpdateDataSpy: () => void = vi.fn()

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      fetchHandlerUpdateDataSpy,
      () => {
        // Empty.
      },
      { debounceTimeInMs: 1_000 }
    )

    fetcher.requestTimelineData(DateTime.fromISO('2020-01-01'), DateTime.fromISO('2020-01-02'), 1, true)
    expect(fetchHandlerUpdateDataSpy).toHaveBeenCalledTimes(1)
  })

  test('Data fetch handler immediately returned undefined item data', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock(1_000)
    const fetchHandlerUpdateItemDetailsSpy: () => void = vi.fn()

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        /* Empty. */
      },
      fetchHandlerUpdateItemDetailsSpy
    )

    fetcher.requestItemDetails(['item-id'])
    expect(fetchHandlerUpdateItemDetailsSpy).toHaveBeenCalledWith({ 'item-id': undefined })
  })

  test('Data fetch handler eventually fetches item details', () => {
    const fetchDataHandler = new TimelineDataFetchHandlerTestMock(1_000)
    const fetchHandlerUpdateItemDetailsSpy: () => void = vi.fn()

    const fetcher = new TimelineDataFetcher(
      fetchDataHandler,
      () => {
        /* Empty. */
      },
      fetchHandlerUpdateItemDetailsSpy
    )

    fetcher.requestItemDetails(['item-id'])
    vi.advanceTimersByTime(1_000)
    expect(fetchHandlerUpdateItemDetailsSpy).toHaveBeenCalledWith({ 'item-id': {} })
  })
})

/*
test for swapped start/end
*/

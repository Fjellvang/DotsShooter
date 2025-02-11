// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { beforeAll, beforeEach, describe, it, vi } from 'vitest'

import { type ActionDebouncer, makeActionDebouncer, type ActionHandler } from '../src/actionDebouncer'

/**
 * Creates a test/mock action handler that resolves or rejects based on the action data.
 * @param simulatedActionTime How long the simulated action takes to complete.
 * @returns ActionHandler
 */
function makeTestHandler(
  simulatedActionTime: number
): ActionHandler<TestHandlerRequestData, string, string, NodeJS.Timeout | 'broken'> {
  const actionHandler: ActionHandler<TestHandlerRequestData, string, string, ReturnType<typeof setTimeout> | 'broken'> =
    {
      setup: (requestData, response, error) => {
        // We'll simulate an action that takes some time to complete.
        const timeout = setTimeout(() => {
          // On completion, call `response` or `error` based on the action data.
          if (requestData.result === 'succeed') response(requestData.message)
          else error(requestData.message)
        }, simulatedActionTime)
        if (requestData.brokenCancel === true) return 'broken'
        else return timeout
      },
      cancel: (token) => {
        // Clear the timeout to cancel the action.
        if (token !== 'broken') {
          clearInterval(token)
        }
      },
    }
  return actionHandler
}

// Request data for the test handler.
interface TestHandlerRequestData {
  // This is what the action will return.
  message: string

  // Whether the action should succeed or fail.
  result: 'succeed' | 'fail'

  // If `true` then cancel will do nothing, simulating a handler with an incorrect cancel implementation.
  brokenCancel?: boolean
}

describe('actionDebouncer', () => {
  beforeAll(() => {
    // We need to fake timers to be able to control time in tests.
    vi.useFakeTimers()
  })

  // Debounce time.
  const ACTION_DEBOUNCE_TIME = 500

  // Arbitrarily defined time it takes for the action to complete.
  const ACTION_PROCESS_TIME = 100

  // Time to debounce and complete an action.
  const ACTION_DEBOUNCE_AND_PROCESS_TIME = ACTION_DEBOUNCE_TIME + ACTION_PROCESS_TIME

  // Extra time to allow tests to settle.
  const ONE_MINUTE_TIME = 60 * 1000

  // Spies, fresh for each test.
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let spyHandlerSetup: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let spyHandlerCancel: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let spyResponse: any
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  let spyError: any

  // A debouncer instance, freshly created for each test.
  let debouncer: ActionDebouncer<TestHandlerRequestData>

  beforeEach(() => {
    // Make a test handler for the test.
    const testHandler = makeTestHandler(ACTION_PROCESS_TIME)

    // Set up spies for the handler's internal methods.
    spyHandlerSetup = vi.spyOn(testHandler, 'setup')
    spyHandlerCancel = vi.spyOn(testHandler, 'cancel')

    // Set up spies for the response and error callbacks.
    spyResponse = vi.fn()
    spyError = vi.fn()

    // Make a fresh debouncer for the test.
    debouncer = makeActionDebouncer(
      testHandler,
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      spyResponse,
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      spyError,
      ACTION_DEBOUNCE_TIME
    )
  })

  it('Single success call does not complete if not enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME - 1)
    expect(spyResponse).toHaveBeenCalledTimes(0)
  })

  it('Single error call does not complete if not enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'fail' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME - 1)
    expect(spyResponse).toHaveBeenCalledTimes(0)
  })

  it('Single success call does complete if enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + ONE_MINUTE_TIME)
    expect(spyResponse).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledWith('Call 1')
  })

  it('Single error call does complete if enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'fail' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + ONE_MINUTE_TIME)
    expect(spyError).toHaveBeenCalledTimes(1)
    expect(spyError).toHaveBeenCalledWith('Call 1')
  })

  it('Single call does not call setup if not enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME - 1)
    expect(spyHandlerSetup).toHaveBeenCalledTimes(0)
  })

  it('Single call does call setup if enough time', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME + 1)
    expect(spyHandlerSetup).toHaveBeenCalledTimes(1)
  })

  it('Cancelled request does not call cancel if not setup yet', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME - 1)
    debouncer.cancel()
    expect(spyHandlerCancel).toHaveBeenCalledTimes(0)
  })

  it('Cancelled request calls cancel if setup', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME)
    debouncer.cancel()
    expect(spyHandlerCancel).toHaveBeenCalledTimes(1)
  })

  it('Multiple requests after first has not been setup only last completes', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    debouncer.requestAction({ message: 'Call 2', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME - 1)
    debouncer.requestAction({ message: 'Call 3', result: 'succeed' })
    debouncer.requestAction({ message: 'Call 4', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + 1)
    expect(spyHandlerSetup).toHaveBeenCalledTimes(1)
    expect(spyHandlerCancel).toHaveBeenCalledTimes(0)
    expect(spyResponse).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledWith('Call 4')
  })

  it('Multiple requests after first has been setup only last completes', async ({ expect }) => {
    debouncer.requestAction({ message: 'Call 1', result: 'succeed' })
    debouncer.requestAction({ message: 'Call 2', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME)
    debouncer.requestAction({ message: 'Call 3', result: 'succeed' })
    debouncer.requestAction({ message: 'Call 4', result: 'succeed' })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + 1)
    expect(spyHandlerSetup).toHaveBeenCalledTimes(2)
    expect(spyHandlerCancel).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledWith('Call 4')
  })

  it('Handles incorrectly implemented cancel function by ignoring unexpected responses', async ({ expect }) => {
    debouncer.requestAction({
      message: 'Call 1',
      result: 'succeed',
      brokenCancel: true,
    })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_TIME)
    debouncer.requestAction({
      message: 'Call 2',
      result: 'succeed',
      brokenCancel: true,
    })
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + 1)
    expect(spyHandlerSetup).toHaveBeenCalledTimes(2)
    expect(spyHandlerCancel).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledTimes(1)
    expect(spyResponse).toHaveBeenCalledWith('Call 2')
  })

  it('Correctly determines when a request is ongoing', async ({ expect }) => {
    expect(debouncer.isRequestOngoing()).toBe(false)
    debouncer.requestAction({
      message: 'Call 1',
      result: 'succeed',
      brokenCancel: true,
    })
    expect(debouncer.isRequestOngoing()).toBe(true)
    vi.advanceTimersByTime(ACTION_DEBOUNCE_AND_PROCESS_TIME + 1)
    expect(debouncer.isRequestOngoing()).toBe(false)
  })

  it('Correctly determines when a request is ongoing after a cancel', async ({ expect }) => {
    debouncer.requestAction({
      message: 'Call 1',
      result: 'succeed',
      brokenCancel: true,
    })
    debouncer.cancel()
    expect(debouncer.isRequestOngoing()).toBe(false)
  })
})

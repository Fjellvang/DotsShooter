// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * An action handler that can make, cancel and complete actions.
 */
export interface ActionHandler<TActionData, TResponse, TError, TActionToken> {
  setup: (
    actionData: TActionData,
    response: (response: TResponse) => void,
    error: (error: TError) => void
  ) => TActionToken
  cancel: (actionToken: TActionToken) => void
}

/**
 * A debouncer that can be used to debounce actions as well as ensuring that only a single action is in-flight at a time.
 */
export interface ActionDebouncer<TActionData> {
  /**
   * Request a new action. The action will be delayed by the debounce time. The action may or may not complete
   * immediately, depending on how the `ActionHandler` handles the action. If any new action is made before this one
   * completes, it will be cancelled.
   * @param actionData
   */
  requestAction: (actionData: TActionData) => void

  /**
   * Cancel any pending action. Safe to call even if no action is pending.
   */
  cancel: () => void

  /**
   * Queries whether a request is currently either queued or in-flight.
   */
  isRequestOngoing: () => boolean
}

/**
 * Create a new `ActionDebouncer`.
 * @param handler Support object that handles creating, cancelling and completing actions.
 * @param response Callback to call when the action completes successfully.
 * @param error Callback to call when the action fails.
 * @param delay The debounce time in milliseconds.
 * @returns A new `ActionDebouncer` object.
 */

export function makeActionDebouncer<TActionData, TResponse, TError, TActionToken>(
  handler: ActionHandler<TActionData, TResponse, TError, TActionToken>,
  response: (responseData: TResponse) => void,
  error: (errorData: TError) => void,
  delay: number
): ActionDebouncer<TActionData> {
  // The id of the timeout that will trigger the action.
  let timeoutId: ReturnType<typeof setTimeout> | undefined

  // Opaque action token from the handler. Handler should be able to cancel the action with this token.
  let actionToken: TActionToken | undefined

  // Each action has a unique id. This is used to ensure that only the most recent action's result is processed.
  let actionCount = 0

  // Indicates that a request is either queued or in-flight.
  let requestIsOngoing = false

  /**
   * Implementation of the `requestAction` function.
   * @param actionData Opaque action data.
   */
  const requestAction = (actionData: TActionData): void => {
    // This is the most recent request.
    const thisActionCount = ++actionCount

    // Cancel any pending or in-flight action.
    cancel()

    // Remember that a request is now queued.
    requestIsOngoing = true

    // Create a new action inside a timeout. This will be delayed by the debounce time.
    timeoutId = setTimeout(() => {
      // Timeout has fired so we no longer need to track it.
      timeoutId = undefined

      // Setup the action through the handler. Remember the opaque token so we can cancel the action if needed.
      actionToken = handler.setup(
        actionData,
        (responseData) => {
          // Action completed successfully. We only want to process the most recent action.
          if (thisActionCount === actionCount) {
            // Clear the action token, clear the request-in-flight flag and call the response callback.
            actionToken = undefined
            requestIsOngoing = false
            response(responseData)
          }
        },
        (errorData) => {
          // Action failed. We only want to process the most recent action.
          if (thisActionCount === actionCount) {
            // Clear the action token, clear the request-in-flight flag and call the error callback.
            actionToken = undefined
            requestIsOngoing = false
            error(errorData)
          }
        }
      )
    }, delay)
  }

  /**
   * Implementation of the `cancel` function.
   */
  const cancel = (): void => {
    // If we have a pending debounce timer, cancel it.
    if (timeoutId !== undefined) {
      clearInterval(timeoutId)
      timeoutId = undefined
    }

    // If we have a pending action, cancel it.
    if (actionToken !== undefined) {
      handler.cancel(actionToken)
      actionToken = undefined
    }

    requestIsOngoing = false
  }

  /**
   * Implementation of the `isRequestOngoing` function.
   * @returns True is a request is enqueued or in-flight.
   */
  const isRequestOngoing = (): boolean => {
    return requestIsOngoing
  }

  // Return client API.
  return {
    requestAction,
    cancel,
    isRequestOngoing,
  }
}

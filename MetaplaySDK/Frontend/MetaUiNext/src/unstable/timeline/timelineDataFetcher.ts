// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { cloneDeep } from 'lodash-es'
import type { DateTime } from 'luxon'

import type { TimelineData, TimelineItem, TimelineItemDetails } from './MEventTimelineTypes'

// TimelineDataFetchHandler -------------------------------------------------------------------------------------------

/**
 * Abstract base class for handling async fetching of timeline data.
 * Concrete classes will need to provide implementations for `requestTimelineData` and `requestItemDetails`.
 */
export abstract class TimelineDataFetchHandler {
  /** Called by the `TimelineDataFetcher` when the handler should fetch a window of data. The handler should call
   * `updateData` when the data is ready.
   * @param firstInstant The first instant in the window to fetch.
   * @param lastInstant The last instant in the window to fetch.
   */
  abstract requestTimelineData(firstInstant: DateTime, lastInstant: DateTime): void

  /**
   * Called by the `TimelineDataFetcher` when the handler should fetch details for a list of items. The handler should
   * call `updateItemDetails` when the details are ready.
   * @param itemIds List of item IDs to fetch details for.
   */
  abstract requestItemDetails(itemIds: string[]): void

  /**
   * Called by the `TimelineDataFetcher` to register the callbacks that the handler should call when data is ready.
   * @param updateDataCallback Function to call when new timeline data is ready.
   * @param updateItemDetailsCallback Function to call when new item details are ready.
   */
  register(
    updateDataCallback: (newTimelineData: TimelineData) => void, // types??
    updateItemDetailsCallback: (newItemDetails: Record<string, TimelineItemDetails | undefined>) => void
  ): void {
    if (this.updateDataCallback ?? this.updateItemDetailsCallback) {
      throw new Error('Register called twice')
    }
    this.updateDataCallback = updateDataCallback
    this.updateItemDetailsCallback = updateItemDetailsCallback
  }

  /**
   * The update data callback.
   */
  private updateDataCallback: UpdateDataCallback | undefined

  /**
   * The update item details callback.
   */
  private updateItemDetailsCallback: UpdateItemDetailsCallback | undefined

  /**
   * Called from the handler to set the timeline data.
   * @param newTimelineData New timeline data.
   */
  protected setTimelineData(newTimelineData: TimelineData): void {
    if (this.updateDataCallback) {
      // Take a local copy of the data.
      this.cachedTimelineData = cloneDeep(newTimelineData)

      // Call the registered callback so that the client can update its state.
      this.updateDataCallback(this.cachedTimelineData)
    } else {
      throw new Error('No updateData callback was registered.')
    }
  }

  /**
   * Called from the handler to update a number of items without needing to replace the entire timeline data.
   * @param itemData List of items to update/remove. A value of `null` indicates that the item should be removed.
   */
  protected updateItemData(itemData: Record<string, TimelineItem | undefined>): void {
    console.assert(!!this.cachedTimelineData, 'No timeline data exists to update!')

    if (Object.keys(itemData).length > 0) {
      for (const [id, item] of Object.entries(itemData)) {
        if (item) {
          // Update existing item.
          // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
          this.cachedTimelineData!.items[id] = cloneDeep(item)
        } else {
          // Remove item.
          // eslint-disable-next-line @typescript-eslint/no-dynamic-delete, @typescript-eslint/no-non-null-assertion
          delete this.cachedTimelineData!.items[id]
        }
      }

      // Call the registered callback so that the client can update its state.
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      this.setTimelineData(this.cachedTimelineData!)
    }
  }

  /**
   * Called from the handler to update the item details.
   * @param itemDetails New item details.
   */
  protected updateItemDetails(itemDetails: Record<string, TimelineItemDetails | undefined>): void {
    if (this.updateItemDetailsCallback) {
      this.updateItemDetailsCallback(itemDetails)
    } else {
      throw new Error('No updateItemDetails callback was registered.')
    }
  }

  /**
   * The current timeline data.
   */
  cachedTimelineData: TimelineData | undefined
}

/**
 * Type for the `TimelineDataFetchHandler`'s update data callback.
 * @param newTimelineData New timeline data.
 */
type UpdateDataCallback = (newTimelineData: TimelineData) => void

/**
 * Type for the `TimelineDataFetchHandler`'s update item details callback.
 * @param itemDetails New item details. A value of `undefined` for an ID indicates that the details have not yet been
 * fetched.
 */
type UpdateItemDetailsCallback = (itemDetails: Record<string, TimelineItemDetails | undefined>) => void

// TimelineDataFetcher ------------------------------------------------------------------------------------------------

/**
 * Class for coordinating the fetching timeline data. Delegates the actual fetching of data to a
 * `TimelineDataFetchHandler` object.
 */
export class TimelineDataFetcher {
  /**
   * Constructor.
   * @param fetchHandler The fetch handler to use.
   * @param onDataUpdate Client callback to call when new timeline data is ready.
   * @param onItemDetailsUpdate Client callback to call when new item details are ready.
   * @param timelineDataFetcherOptions Optional: Configuration options.
   */
  constructor(
    fetchHandler: TimelineDataFetchHandler,
    onDataUpdate: UpdateDataCallback,
    onItemDetailsUpdate: UpdateItemDetailsCallback,
    timelineDataFetcherOptions?: TimelineDataFetcherOptions
  ) {
    this.hasReceivedFirstRequest = false

    // Apply default values and remember the options.
    this.options = {
      largestAllowedTotalWindowInDays: undefined,
      debounceTimeInMs: 1_000,
      ...timelineDataFetcherOptions,
    }

    // Initialise the fetch handler and remember it.
    fetchHandler.register(onDataUpdate, onItemDetailsUpdate)
    this.fetchHandler = fetchHandler

    // Remember the callbacks for our internal use.
    this.onUpdateItemDetails = onItemDetailsUpdate
  }

  private readonly options: TimelineDataFetcherOptions
  private readonly fetchHandler: TimelineDataFetchHandler
  private hasReceivedFirstRequest: boolean
  private readonly onUpdateItemDetails: UpdateItemDetailsCallback

  private pollerId: ReturnType<typeof setTimeout> | undefined

  private nextRequestWindow:
    | {
        firstVisibleInstant: DateTime
        lastVisibleInstant: DateTime
        bufferInDays: number
      }
    | undefined
  private fetchedWindow:
    | {
        startInstant: DateTime
        endInstant: DateTime
      }
    | undefined
  private totalFetchedWindow:
    | {
        startInstant: DateTime
        endInstant: DateTime
      }
    | undefined

  /**
   * Request a window of timeline data. Note that the actual request is (possibly) debounced, and the actual fetching
   * is performed by `_requestTimelineData`.
   * @param firstVisibleInstant First visible instant of the window.
   * @param lastVisibleInstant Last visible instant of the window.
   * @param bufferInDays Amount of days of buffer to add either side of the visible range.
   * @param immediate Optional: Fetch immediately or debounce the request.
   */
  requestTimelineData(
    firstVisibleInstant: DateTime,
    lastVisibleInstant: DateTime,
    bufferInDays: number,
    immediate?: boolean
  ): void {
    // Make first request immediate by default. Subsequent requests are debounced by default.
    immediate = immediate ?? !this.hasReceivedFirstRequest
    this.hasReceivedFirstRequest = true

    // Construct the request window.
    this.nextRequestWindow = {
      firstVisibleInstant,
      lastVisibleInstant,
      bufferInDays,
    }

    // Stop any existing poll timeouts.
    clearTimeout(this.pollerId)

    if (!immediate) {
      // If the request is not immediate then debounce it using a timeout.
      this.pollerId = setTimeout(() => {
        this._requestTimelineData()
      }, this.options.debounceTimeInMs)
    } else {
      // If the request is immediate then just poll immediately.
      this._requestTimelineData()
    }
  }

  /**
   * Re-fetch the last requested window of timeline data.
   * @param immediate Optional: Fetch immediately or debounce the request.
   */
  refreshTimelineData(immediate?: boolean): void {
    if (this.totalFetchedWindow) {
      this.requestTimelineData(this.totalFetchedWindow.startInstant, this.totalFetchedWindow.endInstant, 0, immediate)
    }
  }

  /**
   * Request details for a list of items.
   * @param itemIds List of item IDs to fetch details for.
   */
  requestItemDetails(itemIds: string[]): void {
    // Immediately update the item details to show that we're fetching.
    this.onUpdateItemDetails(
      Object.fromEntries(
        itemIds.map((id) => {
          return [id, undefined]
        })
      )
    )

    // Now make the actual request.
    this.fetchHandler.requestItemDetails(itemIds)
  }

  /**
   * Actually request timeline data. This call is made from `requestTimelineData` after debouncing.
   */
  private _requestTimelineData(): void {
    // We should never get here without a `nextRequest`.
    if (!this.nextRequestWindow) {
      throw new Error('Expected nextRequestWindow to be set.')
    }

    // Extend the request window by the requested buffer size.
    const requestStartInstant = this.nextRequestWindow.firstVisibleInstant.minus({
      days: this.nextRequestWindow.bufferInDays,
    })
    const requestEndInstant = this.nextRequestWindow.lastVisibleInstant.plus({
      days: this.nextRequestWindow.bufferInDays,
    })

    // Do we need to fetch new data?
    const doFetch =
      // If we have no fetch window (ie: we have not fetched any data yet) then we need to fetch.
      !this.fetchedWindow ||
      // Otherwise we need to fetch if the requested window is outside the fetched window.
      !TimelineDataFetcher.doesWindowContainQuery(
        this.fetchedWindow.startInstant,
        this.fetchedWindow.endInstant,
        requestStartInstant,
        requestEndInstant
      )

    if (doFetch) {
      // The fetch window is the requested window extended by the buffer size.
      const newFetchWindowStartInstant = requestStartInstant.minus({ days: this.nextRequestWindow.bufferInDays })
      const newFetchWindowEndInstant = requestEndInstant.plus({ days: this.nextRequestWindow.bufferInDays })
      const newFetchWindowLengthInDays = newFetchWindowEndInstant.diff(newFetchWindowStartInstant).as('days')

      // Extend the total fetched window to include this new fetch window.
      const currentTotalFetchedWindowStartInstant = this.totalFetchedWindow?.startInstant ?? newFetchWindowStartInstant
      const currentTotalFetchedWindowEndInstant = this.totalFetchedWindow?.endInstant ?? newFetchWindowEndInstant
      let newTotalFetchedWindowStartInstant =
        newFetchWindowStartInstant < currentTotalFetchedWindowStartInstant
          ? newFetchWindowStartInstant
          : currentTotalFetchedWindowStartInstant
      let newTotalFetchedWindowEndInstant =
        newFetchWindowEndInstant > currentTotalFetchedWindowEndInstant
          ? newFetchWindowEndInstant
          : currentTotalFetchedWindowEndInstant

      // How long is the total fetch window?
      if (this.options.largestAllowedTotalWindowInDays !== undefined) {
        const newTotalFetchedWindowLengthInDays = newTotalFetchedWindowEndInstant
          .diff(newTotalFetchedWindowStartInstant)
          .as('days')
        if (newTotalFetchedWindowLengthInDays > this.options.largestAllowedTotalWindowInDays) {
          // The total fetch window is too large.
          if (newFetchWindowLengthInDays > this.options.largestAllowedTotalWindowInDays) {
            // Total allowed is just not big enough. We must honour the request.
            newTotalFetchedWindowStartInstant = newFetchWindowStartInstant
            newTotalFetchedWindowEndInstant = newFetchWindowEndInstant
            console.warn(
              '"largestAllowedTotalWindowInDays" is not big enough to honour the request. Forcing request to be honoured.'
            )
          } else {
            // Shrink the total window so that it still covers the request.
            let fixed = false
            if (newFetchWindowStartInstant < currentTotalFetchedWindowStartInstant) {
              // Window is rolling backwards. Remove the newest.
              newTotalFetchedWindowEndInstant = newTotalFetchedWindowStartInstant.plus({
                days: this.options.largestAllowedTotalWindowInDays,
              })
              fixed = true
            }
            // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
            if (newFetchWindowEndInstant) {
              // Window is rolling forwards. Remove the oldest.
              newTotalFetchedWindowStartInstant = newTotalFetchedWindowEndInstant.minus({
                days: this.options.largestAllowedTotalWindowInDays,
              })
              fixed = true
            }
            if (!fixed) {
              throw new Error('Not sure how we got here..')
            }
          }
        }
      }

      // Remember what we're about to request.
      this.fetchedWindow = {
        startInstant: newFetchWindowStartInstant,
        endInstant: newFetchWindowEndInstant,
      }
      this.totalFetchedWindow = {
        startInstant: newTotalFetchedWindowStartInstant,
        endInstant: newTotalFetchedWindowEndInstant,
      }

      // Request the fetch handler to fetch the data.
      this.fetchHandler.requestTimelineData(newTotalFetchedWindowStartInstant, newTotalFetchedWindowEndInstant)
    }

    // Clear the request window.
    this.nextRequestWindow = undefined
  }

  /**
   * Helper function to determine if a window contains a query window.
   * @param windowStartInstant Start of the window.
   * @param windowEndInstant End of the window.
   * @param queryStartInstant Start of the query.
   * @param queryEndInstant End of the query.
   * @returns True if the window entirely contains the query.
   */
  private static doesWindowContainQuery(
    windowStartInstant: DateTime,
    windowEndInstant: DateTime,
    queryStartInstant: DateTime,
    queryEndInstant: DateTime
  ): boolean {
    return queryStartInstant > windowStartInstant && queryEndInstant < windowEndInstant
  }
}

/**
 * Configuration options for `TimelineDataFetcher`.
 */
interface TimelineDataFetcherOptions {
  largestAllowedTotalWindowInDays?: number
  debounceTimeInMs?: number
}

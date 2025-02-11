// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * @fileoverview An `EventStream` is built up of `EventStreamItem`-derived types.
 */
import { DateTime } from 'luxon'

/**
 * Base class for a single event item.
 */
export abstract class EventStreamItemBase {
  /**
   * Optional Id of this event.
   */
  public id: string

  /**
   * Optional dot separated path of this event.
   */
  public path: string

  /**
   * Time that this event occurred at as ISO string.
   */
  public time: string

  /**
   * Type of the event, used to decide how to render it.
   */
  public type: string

  /**
   * Type-specific data.
   */
  public typeData: any

  /**
   * @param id Optional Id of this event.
   * @param path Optional dot separated path of this event.
   * @param time Time that this event occurred at as ISO string.
   * @param type Type of the event, used to decide how to render it.
   * @param typeData Type-specific data.
   */
  // eslint-disable-next-line @typescript-eslint/max-params
  public constructor(id: string, path: string, time: string, type: string, typeData: any) {
    this.id = id
    this.path = path
    this.time = time
    this.type = type
    this.typeData = typeData
  }

  /**
   * Test if the event contains the search string. Because what "contains" means depends on the event type, this is
   * over-loaded by the derived event type classes.
   * @param lowerSearchString String to check for as lower case.
   * @returns True if the event matches the search string.
   */
  public search(lowerSearchString: string): boolean {
    return this.id.toLowerCase().includes(lowerSearchString)
  }

  /**
   * Get the type of this event to display in the UI.
   * @returns The display name of the event type. Defaults to undefined.
   */
  public getEventDisplayType(): string | undefined {
    return undefined
  }

  /**
   * Get the keywords of this event to display in the UI.
   * @returns A string array of keywords of the event. Defaults to undefined.
   */
  public getEventKeywords(): string[] | undefined {
    return undefined
  }
}

/**
 * Represent a single event.
 */
export class EventStreamItemEvent extends EventStreamItemBase {
  /**
   * @param time Time that this event occurred at as ISO string.
   * @param title Display title for the event.
   * @param description Display description for the event.
   * @param id Id of this event.
   * @param sourceData Raw source data that was used to create this event.
   * @param author Author of the event.
   * @param viewMore Title of the view more information link.
   * @param viewMoreLink URL to jump to when clicking on view more information link.
   */
  // eslint-disable-next-line @typescript-eslint/max-params
  public constructor(
    time: string,
    title: string | undefined,
    description: string | undefined,
    id: string,
    sourceData: unknown,
    author: string,
    viewMore: string,
    viewMoreLink: string
  ) {
    const typeData = {
      title: title ?? 'No title.',
      description: description ?? 'No description.',
      sourceData,
      author,
      viewMore,
      viewMoreLink,
      terminatorStyle: null,
    }
    super(id, '', time, 'Event', typeData)
  }

  public search(lowerSearchString: string): boolean {
    return (
      this.typeData.title.toLowerCase().includes(lowerSearchString) ||
      this.typeData.description.toLowerCase().includes(lowerSearchString) ||
      this.typeData.author?.toLowerCase().includes(lowerSearchString) ||
      super.search(lowerSearchString)
    )
  }

  public getEventDisplayType(): string {
    return this.typeData.title
  }

  public getEventKeywords(): string[] | undefined {
    return this.typeData.sourceData.payload?.keywords
  }
}

/**
 * Represents that a session of events is about to follow.
 */
export class EventStreamItemSession extends EventStreamItemBase {
  /**
   * @param path Optional dot separated path of this session.
   * @param startEvent First (ie: oldest) event of the session.
   * @param endEvent Last (ie: newest) event of the session.
   * @param numEvents Number of events in the session.
   */

  constructor(path: string, startEvent: EventStreamItemBase, endEvent: EventStreamItemBase, numEvents: number) {
    const id = startEvent.typeData.sourceData?.payload.sessionToken || 'Unknown'
    const startTime = startEvent.time
    const endTime = endEvent.time
    const duration = DateTime.fromISO(endTime).diff(DateTime.fromISO(startTime))
    const typeData = {
      startTime,
      duration,
      sessionNumber: startEvent.typeData.sourceData?.payload.sessionNumber || 'Unknown',
      numEvents,
      deviceName: startEvent.typeData.sourceData?.payload.deviceModel || 'Unknown',
    }
    // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
    super(id, path, endTime, 'Session', typeData)
  }

  /**
   * The device name of the connect event is a good choice for a pre-defined search string.
   * @returns List of search strings.
   */
  search(lowerSearchString: string): boolean {
    return this.typeData.deviceName.toLowerCase().includes(lowerSearchString) || super.search(lowerSearchString)
  }
}

/**
 * Represents that a group of repeated events is about to follow.
 */
export class EventStreamItemRepeatedEvents extends EventStreamItemBase {
  /**
   * @param path Optional dot separated path of this set of repeated events.
   * @param startEvent First (ie: oldest) event of the repeated events.
   * @param endEvent Last (ie: newest) event of the repeated events.
   * @param numEvents Number of repeated events.
   */

  constructor(path: string, startEvent: EventStreamItemBase, endEvent: EventStreamItemBase, numEvents: number) {
    const startTime = startEvent.time
    const endTime = endEvent.time
    const duration = DateTime.fromISO(endTime).diff(DateTime.fromISO(startTime))
    const typeData = {
      numEvents,
      repeatedTitle: startEvent.typeData.title,
      duration,
    }
    super('', path, endTime, 'RepeatedEvents', typeData)
  }

  /**
   * The title of all of these repeated events is a good choice for a pre-defined search string.
   * @returns List of search strings.
   */
  search(lowerSearchString: string): boolean {
    return this.typeData.repeatedTitle.toLowerCase().includes(lowerSearchString) || super.search(lowerSearchString)
  }
}

/**
 * Represents that a repeated set of events from a single day is about to follow.
 */
export class EventStreamItemDay extends EventStreamItemBase {
  /**
   * @param path Optional dot separated path of this set of day events.
   * @param startEvent First (ie: oldest) event of the set of day events.
   * @param endEvent Last (ie: newest) event of day events.
   * @param numEvents Number of events in the set of day events.
   */

  constructor(path: string, startEvent: EventStreamItemBase, endEvent: EventStreamItemBase, numEvents: number) {
    const id = DateTime.fromISO(startEvent.time).toISODate() ?? 'Unknown'
    const typeData = {
      date: startEvent.time,
      numEvents,
      deviceName: startEvent.typeData.sourceData?.payload.deviceModel || '',
    }
    super(id, path, endEvent.time, 'Day', typeData)
  }

  /**
   * The device name of the connect event is a good choice for a pre-defined search string.
   * @returns List of search strings.
   */
  search(lowerSearchString: string): boolean {
    return this.typeData.deviceName.toLowerCase().includes(lowerSearchString) || super.search(lowerSearchString)
  }
}

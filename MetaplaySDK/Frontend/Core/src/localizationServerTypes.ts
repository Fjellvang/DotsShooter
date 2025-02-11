// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { GameDataStatus } from './gameConfigServerTypes'

/**
 * Describes an item in a library.
 */
export interface LocalizationTableItem {
  /**
   * C# type.
   */
  type: string
  /**
   * Title to display for this item. Usually the member's name.
   */
  title: string
  /**
   * Object containing key-value pairs for different language translations.
   * TODO: instead of a string, this probably should be a type that can more efficiently tell us if the value is missing. Now we do a lot of string content checks in downstream code.
   */
  values: Record<string, string>
}

export interface LocalizationTable {
  info: LocalizationInfo
  table: LocalizationTableItem[]
  languageIds: string[]
}

export interface LocalizationContent {
  info: LocalizationInfo
  locs: Record<
    string,
    {
      languageId: string
      contentHash: string
      translations: Record<string, string>
    }
  >
}

export interface LocalizationInfo {
  id: string
  name: string
  description: string
  bestEffortStatus: GameDataStatus
  isActive: boolean
  isArchived: boolean

  persistedAt: string
  lastModifiedAt: string
  archiveBuiltAt: string
  source: string

  failureInfo?: string
}

// --------

export enum LocalizationErrorType {
  // An exception occurred.
  Exception,

  /// An exception occurred that is already transformed to a string (likely from building, and stored in the database).
  StringException,

  /// The build task mysteriously disappeared, likely due to the server stopping or crashing.
  TaskDisappeared,

  /// The build messages or validation messages contain one or more messages that are treated as errors.
  BlockingMessages,
}

export type LocalizationError =
  | LocalizationErrorException
  | LocalizationErrorStringException
  | LocalizationErrorTaskDisappeared
  | LocalizationErrorBlockingMessages

export interface LocalizationErrorException {
  errorType: 'Exception'
}

export interface LocalizationErrorStringException {
  errorType: 'StringException'
  fullException: string
}

export interface LocalizationErrorTaskDisappeared {
  errorType: 'TaskDisappeared'
}

export interface LocalizationErrorBlockingMessages {
  errorType: 'BlockingMessages'
}

export interface MinimalLocalizationInfo {
  id: string
  bestEffortStatus: GameDataStatus

  name: string
  description: string
  source: string

  isActive: boolean
  isArchived: boolean
  lastModifiedAt: string
  publishedAt: string
  unpublishedAt: string

  publishBlockingErrors: LocalizationError[]

  versionHash: string
  taskId: string
  buildStartedAt: string
}

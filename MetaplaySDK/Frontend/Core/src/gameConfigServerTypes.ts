// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

// Definitions of various data types that go to make up the server responses. ---------------------

export type GameDataStatus = 'Building' | 'Success' | 'Failed'

/**
 * Base class for game configs data that the server returns.
 */
interface GameConfigInfoBase {
  id: string
  fullConfigVersion: string
  cdnVersion: string
  status: GameDataStatus

  name: string
  description: string
  source: string

  isActive: boolean
  isArchived: boolean
  publishedAt: string | null
  unpublishedAt: string | null

  buildStartedAt: string
  lastModifiedAt: string

  blockingGameConfigMessageCount: number
  publishBlockingErrors: GameConfigError[]
}

export interface BuildReportSummary {
  buildLogLogLevelCounts: Record<string, number>
  isBuildMessageTrimmed: boolean
  validationResultsLogLevelCounts: Record<string, number>
  isValidationMessagesTrimmed: boolean
}

export interface DashboardBuildReportSummary extends BuildReportSummary {
  totalLogLevelCounts: Record<string, number>
}

export type GameConfigLogLevel = 'NotSet' | 'Verbose' | 'Debug' | 'Information' | 'Warning' | 'Error'

export interface GameConfigBuildMessage {
  sourceInfo: string | null
  sourceLocation: string | null
  locationUrl: string | null
  itemId: string | null
  variantId: string | null
  level: GameConfigLogLevel
  message: string
  exception: string | null
  callerFileName: string
  callerMemberName: string
  callerLineNumber: number
  shortSource: string
}

export interface GameConfigValidationMessage {
  sheetName: string
  configKey: string
  message: string
  columnHint: string
  variants: string[]
  url: string
  sourcePath: string
  sourceMember: string
  sourceLineNumber: number
  messageLevel: GameConfigLogLevel
  count: string
  additionalData: Record<string, any>
}

export interface GameConfigBuildReport {
  highestMessageLevel: GameConfigLogLevel
  buildMessages: GameConfigBuildMessage[]
  validationMessages: GameConfigValidationMessage[]
}

export interface GameConfigMetaData {
  buildParams: Record<string, any> // GameConfigBuildParameters - game specific
  buildSourceMetadata: Record<string, any> // GameConfigBuildParameters - game specific
  buildDescription: string
  buildReport: GameConfigBuildReport | null

  parentConfigId: string
  parentConfigHash: string
}

export interface ExperimentData {
  id: string
  displayName: string
  patchedLibraries: string[]
  variants: string[]
}

export enum GameConfigErrorType {
  // An exception occurred which can't be attributed to a specific library
  exception = 'Exception',
  // Importing a library threw an exception, see <see cref="LibraryCountGameConfigInfo.LibraryImportErrors"/> for more info
  libraryException = 'LibraryException',
  // An exception occurred that is already transformed to a string (likely from building, and stored in the database)
  // With the changes to GameConfigMetaData, this is often `Game Config build failed, see log for errors` but can still be a stringified exception in rare cases.
  stringException = 'StringException',
  // The build task mysteriously disappeared, likely due to the server stopping or crashing.
  taskDisappeared = 'TaskDisappeared',
  // The build messages or validation messages contain one or more messages that are treated as errors.
  blockingMessages = 'BlockingMessages',
}

export type GameConfigError =
  | GameConfigErrorException
  | GameConfigErrorLibraryException
  | GameConfigErrorStringException
  | GameConfigErrorTaskDisappeared
  | GameConfigErrorBlockingMessages

export interface GameConfigErrorException {
  phaseType: 'Build' | 'Import'
  errorType: 'Exception'
  exceptionType: string
  fullException: string
  message: string
}

export interface GameConfigErrorLibraryException {
  phaseType: 'Build' | 'Import'
  errorType: 'LibraryImport'
  exceptionType: string
  fullException: string
  message: string
}

export interface GameConfigErrorStringException {
  phaseType: 'Build' | 'Import'
  errorType: 'StringException'
  fullException: string
}

export interface GameConfigErrorTaskDisappeared {
  phaseType: 'Build' | 'Import'
  errorType: 'TaskDisappeared'
}

export interface GameConfigErrorBlockingMessages {
  phaseType: 'Build' | 'Import'
  errorType: 'BlockingMessages'
}

/**
 * Describes an item in a library.
 */
export interface LibraryConfigItem {
  /**
   * C# type.
   */
  type: string
  /**
   * Title to display for this item. Usually the member's name.
   */
  title: string
  /**
   * Optional subtitle that contains extra information about the item.
   */
  subtitle?: string
  /**
   * Optional sparse list of values for this item. The list contains entries for each variant of the selected
   * experiment. If there are no changes in any of the variants then this list will just include a single entry under
   * the key `Baseline`. This field is optional because some items (such as arrays) don't themselves have any values.
   */
  values?: Record<string, string>
  /**
   * True if the item has any differences.
   */
  differences?: boolean
  /**
   * Optional list of child items, ie: for arrays, dictionaries and objects.
   */
  children?: LibraryConfigItem[]
}

// These are the responses from the various server endpoints. -------------------------------------

/**
 * Game config data as returned by the `/gameConfig/{configIdStr}` endpoint.
 * This is legacy data and should preferably not be used anymore. This only still exists for use in the diff page.
 */
export interface StaticGameConfigInfo extends GameConfigInfoBase {
  contents: {
    metaData: GameConfigMetaData
    sharedConfig: Record<string, number>
    serverConfig: Record<string, number>
  }
}

/**
 * Game config data as returned by the `/gameConfig/{configIdStr}/count` endpoint.
 */
export interface LibraryCountGameConfigInfo extends GameConfigInfoBase {
  contents: {
    metaData: GameConfigMetaData
    sharedLibraries: Record<string, number>
    serverLibraries: Record<string, number>
  }
  experiments: ExperimentData
  libraryImportErrors?: Record<string, GameConfigError>
  buildReportSummary: DashboardBuildReportSummary | null
}

/**
 * Game config details as returned by the `/gameConfig/{configIdStr}/details` endpoint.
 */
export interface GameConfigLibraryContent {
  config: Record<string, LibraryConfigItem>
  libraryImportErrors?: Record<string, GameConfigError>
}

export interface MinimalGameConfigInfo {
  id: string
  fullConfigVersion: string
  bestEffortStatus: GameDataStatus

  name: string
  description: string | null
  source: string

  isActive: boolean
  isArchived: boolean
  publishedAt: string | null
  unpublishedAt: string | null

  buildStartedAt: string

  blockingGameConfigMessageCount: number
  publishBlockingErrors: GameConfigError[]

  buildReportSummary: DashboardBuildReportSummary | null
}

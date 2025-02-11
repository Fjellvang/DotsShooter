// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Base type definition of a selected option for the MetaInputSelect component.
 */
export interface MetaInputSelectOption<T> {
  id: string
  value: T
  disabled?: boolean
}

/**
 * Deletion status of a player.
 */
export type PlayerDeletionStatus =
  | 'None'
  | 'DeletedByUnknown'
  | 'DeletedByUnknownLegacy'
  | 'ScheduledByAdmin'
  | 'DeletedByAdmin'
  | 'ScheduledByUser'
  | 'DeletedByUser'
  | 'ScheduledBySystem'
  | 'DeletedBySystem'

/**
 * Abbreviated player details as returned from various server API endpoints.
 */
export interface PlayerListItem {
  $type: string
  id: string
  deserializedSuccessfully: boolean
  name: string
  level: number
  createdAt: string
  lastLoginAt: string
  deletionStatus: PlayerDeletionStatus
  isBanned: boolean
  totalIapSpend: number
  isDeveloper: boolean
  isInitialized: boolean
  deserializationException: string | null
}

/**
 * Return data from `/players/activePlayers` API endpoint.
 */
export interface ActivePlayerInfo {
  activityAt: string
  createdAt: string
  deletionStatus: PlayerDeletionStatus
  displayName: string
  entityId: string
  isDeveloper: boolean
  level: number
  totalIapSend: number
}

/**
 * Result data from the `players/bulkValidate` API endpoint.
 */
export interface BulkListInfo {
  $type: string
  playerIdQuery: string
  validId: boolean
  playerData: PlayerListItem
}

/**
 * Result data from the `MetaInputGuildSelect` component.
 */
export interface GuildSearchResult {
  $type: string
  entityId: string
  displayName: string
  createdAt: string
  lastLoginAt: string
  phase: string
  numMembers: number
  maxNumMembers: number
}

/**
 * Detailed info for entries in `PlayerRawInfo`.
 */
export interface PlayerRawInfoResult {
  title: string
  error?: {
    title: string
    details: string
  }
  data?: Record<string, any>
}

/**
 * Return from the `players/{playerId}/raw` API endpoint.
 */
export type PlayerRawInfo = Record<string, PlayerRawInfoResult>

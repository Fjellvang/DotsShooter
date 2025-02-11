// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { ActivePlayerInfo } from '@metaplay/meta-ui'
import {
  type SubscriptionOptions,
  getFetcherPolicyGet,
  getCacheRetentionPolicyKeepForever,
  getCacheRetentionPolicyTimed,
  getPollingPolicyTimer,
} from '@metaplay/subscriptions'

/**
 * The options for a list of all players.
 */
export function getAllPlayersSubscriptionOptions(): SubscriptionOptions {
  return {
    permission: 'api.players.view',
    pollingPolicy: getPollingPolicyTimer(10000),
    fetcherPolicy: getFetcherPolicyGet('/players'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/**
 * The options for a list of all active players.
 */
export function getAllActivePlayersSubscriptionOptions(): SubscriptionOptions<ActivePlayerInfo[]> {
  return {
    permission: 'api.players.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet('/players/activePlayers'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/**
 * The options for a single player.
 * @param playerId The ID of the player to get the subscription options for.
 */
export function getSinglePlayerSubscriptionOptions(playerId: string): SubscriptionOptions {
  return {
    permission: 'api.players.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(`/players/${playerId}`),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(10000),
  }
}

interface DeveloperPlayerInfo {
  id: string
  name: string
  lastLoginAt: string
}

/**
 * The options for a list of all players who have `isDeveloper` set as true.
 */
export function getDeveloperPlayersSubscriptionOptions(): SubscriptionOptions<DeveloperPlayerInfo[]> {
  return {
    permission: 'api.players.view_developers',
    pollingPolicy: getPollingPolicyTimer(10_000),
    fetcherPolicy: getFetcherPolicyGet('/players/developers'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

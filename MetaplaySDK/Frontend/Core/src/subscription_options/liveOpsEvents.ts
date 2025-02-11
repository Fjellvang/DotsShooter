// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import {
  type SubscriptionOptions,
  getFetcherPolicyGet,
  getCacheRetentionPolicyKeepForever,
  getPollingPolicyTimer,
  getPollingPolicyOnceOnly,
  getCacheRetentionPolicyTimed,
} from '@metaplay/subscriptions'

import type {
  GetLiveOpsEventsListApiResult,
  LiveOpsEventDetailsInfo,
  LiveOpsEventTypeInfo,
  PlayerLiveOpsEventsInfo,
} from '../liveOpsEventServerTypes'

/**
 * The options to get all Live Ops Events in the game.
 */
export function getAllLiveOpsEventsSubscriptionOptions(): SubscriptionOptions<GetLiveOpsEventsListApiResult> {
  return {
    permission: 'api.liveops_events.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet('/liveOpsEvents'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/**
 * The options to get all Live Ops Events in the game.
 */
export function getLiveOpsEventTypesSubscriptionOptions(): SubscriptionOptions<LiveOpsEventTypeInfo[]> {
  return {
    permission: 'api.liveops_events.view',
    pollingPolicy: getPollingPolicyOnceOnly(),
    fetcherPolicy: getFetcherPolicyGet('/liveOpsEventTypes'),
    cacheRetentionPolicy: getCacheRetentionPolicyKeepForever(),
  }
}

/**
 * The options to get all Live Ops Events in the game.
 */
export function getSingleLiveOpsEventsSubscriptionOptions(
  eventId: string
): SubscriptionOptions<LiveOpsEventDetailsInfo> {
  return {
    permission: 'api.liveops_events.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(`/liveOpsEvent/${eventId}`),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(10000),
  }
}

/**
 * The options to get the LiveOps Events a player is participating in or eligible for.
 */
export function getLiveOpsEventsForPlayerSubscriptionOptions(
  playerId: string
): SubscriptionOptions<PlayerLiveOpsEventsInfo> {
  return {
    permission: 'api.players.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(`/players/${playerId}/liveOpsEvents`),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(10000),
  }
}

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import {
  type SubscriptionOptions,
  getFetcherPolicyGet,
  getCacheRetentionPolicyTimed,
  getPollingPolicyTimer,
  getCacheRetentionPolicyDeleteImmediately,
} from '@metaplay/subscriptions'

/**
 * The options for a list of all audit log events.
 * @param targetType Type of audit log events to retrieve.
 * @param targetId Id of target type to retrieve or undefined to retrieve all events of `targetType`.
 * @param limit Optional: Maximum number of events to return. Defaults to 50.
 */
export function getAllAuditLogEventsSubscriptionOptions(
  targetType: string,
  targetId: string | undefined = undefined,
  limit = 50
): SubscriptionOptions {
  const queryString = `targetType=${targetType}` + (targetId ? `&targetId=${targetId}` : '') + `&limit=${limit}`

  return {
    permission: 'api.audit_logs.view',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(`/auditLog/search?${queryString}`),
    cacheRetentionPolicy: getCacheRetentionPolicyTimed(10000),
  }
}

interface AuditLogEventsSearchOptions {
  /**
   * Optional: Type of audit log events to retrieve.
   */
  targetType?: string
  /**
   * Optional: ID of target type to retrieve.
   */
  targetId?: string
  /**
   * Optional: ID of event source to retrieve.
   */
  sourceId?: string
  /**
   * Optional: IP address of event source to retrieve.
   */
  sourceIpAddress?: string
  /**
   * Optional: ISO code of event source to retrieve.
   */
  sourceCountryIsoCode?: string
  /**
   * Optional: Maximum number of events to return. Defaults to 50.
   */
  limit?: number
}

/**
 * The options to query audit log events with advanced search.
 */
export function getAuditLogEventsSearchSubscriptionOptions(options?: AuditLogEventsSearchOptions): SubscriptionOptions {
  options = {
    limit: 50, // Default to 50.
    ...options,
  }

  let url = '/auditLog/advancedSearch?'
  if (options.targetType) url += `targetType=${options.targetType}&`
  if (options.targetId) url += `targetId=${options.targetId}&`
  if (options.sourceId) url += `source=${options.sourceId}&`
  if (options.sourceIpAddress) {
    url += `sourceIpAddress=${options.sourceIpAddress}&`
  }
  if (options.sourceCountryIsoCode) {
    url += `sourceCountryIsoCode=${options.sourceCountryIsoCode}&`
  }
  url += `limit=${options.limit}&`

  return {
    permission: 'api.audit_logs.search',
    pollingPolicy: getPollingPolicyTimer(5000),
    fetcherPolicy: getFetcherPolicyGet(url),
    cacheRetentionPolicy: getCacheRetentionPolicyDeleteImmediately(),
  }
}

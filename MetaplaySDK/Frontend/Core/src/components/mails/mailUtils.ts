// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

/**
 * Server definition of localized content
 */
export type LocalizedContent = Record<string, { title: string; body: string }>

/**
 * Server definition of a target condition context.
 */
export interface TargetConditionContent {
  $type: 'Metaplay.Core.Player.PlayerSegmentBasicCondition'
  requireAllSegments?: string[]
  requireAnySegment?: string[]
}

/**
 * Server side definition of target audience.
 */
export interface TargetingOptions {
  targetPlayers: string[]
  targetCondition: TargetConditionContent | null
  valid: boolean
}

/**
 * Server side definition of an event.
 */
export interface EventInfo {
  type: string
  typeCode: number
  eventType: string
  displayName: string
  categoryName: string
  schemaVersion: number
  docString: string
  includeInEventLog: boolean
  sendToAnalytics: boolean
  canTrigger: boolean
  parameters: string[]
}

/**
 * Sever side definition of trigger.
 */
export interface TriggerInfo {
  eventTypeCode: number
  $type: string
}

/**
 * Server side definition of a trigger condition.
 */
export interface ConditionOption {
  id: string
  value: TriggerInfo
  disabled?: boolean
}

/**
 * Server definition of a broadcast.
 */
export interface BroadcastInfo {
  id?: string
  name: string
  startAt: string | null
  endAt: string | null
  triggerCondition: any
  contents: object
  targetPlayers: string[]
  targetCondition: TargetConditionContent | null
}

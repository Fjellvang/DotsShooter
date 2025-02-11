// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { DateTime } from 'luxon'

import { MetaListFilterSet, MetaListFilterOption } from '@metaplay/meta-ui'
import type { Variant } from '@metaplay/meta-ui-next'

import type { LiveOpsEventBriefInfo, LiveOpsEventPhase } from './liveOpsEventServerTypes'

interface PhaseInfo {
  displayString: string
  badgeVariant: Variant
}

export const liveOpsEventPhaseInfos: Record<LiveOpsEventPhase, PhaseInfo> = {
  NotYetStarted: {
    displayString: 'Scheduled',
    badgeVariant: 'neutral',
  },
  InPreview: {
    displayString: 'In Preview',
    badgeVariant: 'primary',
  },
  Active: {
    displayString: 'Active',
    badgeVariant: 'success',
  },
  EndingSoon: {
    displayString: 'Ending Soon',
    badgeVariant: 'success',
  },
  InReview: {
    displayString: 'In Review',
    badgeVariant: 'primary',
  },
  Ended: {
    displayString: 'Concluded',
    badgeVariant: 'neutral',
  },
}

/**
 * Utility function to make filter sets for the live ops event list view.
 * @param eventTypeNames List of all the event type names to include in the filter set.
 * @param phases What phases to include in the filter set.
 * @param includedScheduledTimeMode Whether to include the scheduled time mode filter.
 */
export function makeListViewFilterSets(
  eventTypeNames: string[],
  phases: LiveOpsEventPhase[],
  includedScheduledTimeMode: boolean
): MetaListFilterSet[] {
  const timeModeFilterOptions = [
    new MetaListFilterOption('UTC', (event) => (event as LiveOpsEventBriefInfo).schedule?.isPlayerLocalTime === false),
    new MetaListFilterOption(
      'Local time',
      (event) => (event as LiveOpsEventBriefInfo).schedule?.isPlayerLocalTime === true
    ),
    ...(includedScheduledTimeMode
      ? [new MetaListFilterOption('Unscheduled', (event) => (event as LiveOpsEventBriefInfo).schedule === null)]
      : []),
  ]

  return [
    new MetaListFilterSet(
      'phases',
      phases.map(
        (phaseKey) =>
          new MetaListFilterOption(
            liveOpsEventPhaseInfos[phaseKey].displayString,
            (event) => (event as LiveOpsEventBriefInfo).currentPhase === phaseKey
          )
      )
    ),
    new MetaListFilterSet(
      'eventType',
      eventTypeNames.map(
        (eventTypeName) =>
          new MetaListFilterOption(
            eventTypeName,
            (event) => (event as LiveOpsEventBriefInfo).eventTypeName === eventTypeName
          )
      )
    ),
    new MetaListFilterSet('timeMode', timeModeFilterOptions),
  ]
}

/**
 * Determines the format to return based on an event's timestamp relative to current time and a breakpoint in hours.
 * @param eventTimestamp The time of the event. This is a timestamp in ISO string format that can be either in the past or future.
 * @param breakpointHours The number of hours at which the duration format should switch. Defaults to 24 hours.
 * @returns `exactDuration` or `humanized`, a `showAs` parameter type of `MetaDuration`.
 * TODO: Collapse this functionality into future `MDuration` component.
 */
export function getDurationFormat(eventTimestamp: string, breakpointHours = 24): 'exactDuration' | 'humanized' {
  if (Math.abs(DateTime.fromISO(eventTimestamp).diffNow().as('hours')) < breakpointHours) {
    return 'exactDuration'
  } else {
    return 'humanized'
  }
}

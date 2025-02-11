<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
meta-list-card(
  :title="listMode === 'preview' ? 'Import Preview' : 'Import Results'"
  :dangerous="!isImportValid"
  :item-list="decoratedImportResults"
  :page-size="listMode === 'preview' ? 4 : 6"
  empty-message="No results available to preview yet."
  )
  template(#item-card="{ item: importedEvent }")
    MCollapse(
      extra-m-list-item-margin
      :hide-collapse="listMode === 'result'"
      )
      template(#header)
        MListItem(no-left-padding)
          span(v-if="importedEvent?.eventInfo?.displayName") {{ importedEvent?.eventInfo?.displayName }}
          span(
            v-else
            class="tw-italic tw-text-neutral-500"
            ) No name available
          template(#bottom-left) {{ importedEvent.eventId }}
          template(#top-right)
            MBadge(:variant="importedEvent.badgeVariant") {{ importedEvent.badgeText }}
          //- Only display links to event detail page in the result list if import was successful.
          template(
            v-if="listMode === 'result' && isImportValid"
            #bottom-right
            )
            MTextButton(:to="`/liveOpsEvents/${importedEvent.eventId}`") View event

      //- Collapse content
      MErrorCallout(
        v-if="importedEvent.displayError"
        :error="importedEvent.displayError"
        class="tw-mb-3"
        )
      MList(
        v-if="importedEvent.eventInfo"
        show-border
        striped
        )
        MListItem(condensed) Created At
          template(#top-right) #[MDateTime(:instant="DateTime.fromISO(importedEvent.eventInfo.createdAt)")]
        MListItem(condensed) Event Type
          template(#top-right) {{ importedEvent.eventInfo.eventTypeName }}
        MListItem(
          v-if="!importedEvent.eventInfo.useSchedule"
          condensed
          ) Schedule
          template(#top-right)
            span(class="tw-italic tw-text-neutral-500") None
        MListItem(
          v-if="importedEvent.eventInfo.useSchedule"
          condensed
          ) Time Mode
          template(#top-right)
            MBadge {{ importedEvent.eventInfo.schedule?.isPlayerLocalTime ? 'Player Local' : 'UTC' }}
        MListItem(
          v-if="importedEvent.eventInfo.useSchedule"
          condensed
          ) Start Time
          template(#top-right)
            MDateTime(
              v-if="importedEvent.eventInfo.schedule?.enabledStartTime"
              :instant="DateTime.fromISO(importedEvent.eventInfo.schedule.enabledStartTime)"
              :disable-tooltip="importedEvent.eventInfo.schedule?.isPlayerLocalTime"
              )
        MListItem(
          v-if="importedEvent.eventInfo.useSchedule"
          condensed
          ) End Time
          template(#top-right)
            MDateTime(
              v-if="importedEvent.eventInfo.schedule?.enabledEndTime"
              :instant="DateTime.fromISO(importedEvent.eventInfo.schedule.enabledEndTime)"
              :disable-tooltip="importedEvent.eventInfo.schedule?.isPlayerLocalTime"
              )
        MListItem(
          v-if="importedEvent.eventInfo.useSchedule"
          condensed
          ) Preview duration
          template(#top-right)
            span(v-if="importedEvent.eventInfo.schedule?.previewDuration !== 'P0Y0M0DT0H0M0S'") #[meta-duration(:duration="importedEvent.eventInfo.schedule?.previewDuration")]
            span(
              v-else
              class="tw-italic tw-text-neutral-500"
              ) None
        MListItem(
          v-if="importedEvent.eventInfo?.useSchedule"
          condensed
          ) Ending Soon Duration
          template(#top-right)
            span(v-if="importedEvent.eventInfo.schedule?.endingSoonDuration !== 'P0Y0M0DT0H0M0S'") #[meta-duration(:duration="importedEvent.eventInfo.schedule?.endingSoonDuration")]
            span(
              v-else
              class="tw-italic tw-text-neutral-500"
              ) None
        MListItem(
          v-if="importedEvent.eventInfo.useSchedule"
          condensed
          ) Review Duration
          template(#top-right)
            span(v-if="importedEvent.eventInfo.schedule?.reviewDuration !== 'P0Y0M0DT0H0M0S'") #[meta-duration(:duration="importedEvent.eventInfo?.schedule?.reviewDuration")]
            span(
              v-else
              class="tw-italic tw-text-neutral-500"
              ) None
      MList(
        v-else
        show-border
        striped
        )
        MListItem(condensed)
          span(class="tw-italic tw-text-neutral-500") No event info available
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed } from 'vue'

import { MetaListCard } from '@metaplay/meta-ui'
import {
  MList,
  MListItem,
  MBadge,
  MCollapse,
  MDateTime,
  MTextButton,
  MErrorCallout,
  type Variant,
  DisplayError,
} from '@metaplay/meta-ui-next'

import type {
  LiveOpsEventImportResult,
  LiveOpsEventDiagnostic,
  LiveOpsEventDiagnosticLevel,
} from '../../liveOpsEventServerTypes'
import { LiveOpsEventImportOutcome } from '../../liveOpsEventServerTypes'

const props = defineProps<{
  /**
   * The mode in which the list card is displayed.
   */
  listMode: 'preview' | 'result'
  /**
   * Indicates whether the import validation was successful.
   */
  isImportValid: boolean
  /**
   * Import results to display in this card.
   */
  importResults: LiveOpsEventImportResult[] | undefined
}>()

/**
 * Decorate `LiveOpsEventImportResult` with additional user facing information.
 */
type DecoratedLiveOpsEventImportResult = LiveOpsEventImportResult & {
  badgeVariant: Variant
  badgeText: string
  displayError: DisplayError | undefined
}

/**
 * Additional user facing information is appended for better understanding event import results.
 * Also the list is sorted to show errors and warnings first at the top.
 */
const decoratedImportResults = computed((): DecoratedLiveOpsEventImportResult[] | undefined => {
  if (props.importResults === undefined) {
    // When result list is undefined, the import preview shows a loading skeleton.
    return undefined
  } else if (props.importResults.length === 0) {
    // When file contents is cleared we set array to empty.
    return []
  }

  const decoratedImportResults: DecoratedLiveOpsEventImportResult[] = props.importResults.map((eventResult) => {
    // Create a decorated list of all diagnostics.
    const diagnostics = flattenDiagnosticsToArray(eventResult.diagnostics)
    return {
      ...eventResult,
      badgeVariant: getBadgeVariant(eventResult.outcome, diagnostics),
      badgeText: outcomeToOutcomeInfoMap[eventResult.outcome].badgeName,
      displayError: createDisplayError(eventResult.outcome, diagnostics),
    }
  })

  // The outcome for each of the five `LiveOpsEventImportOutcome`s has a variant which gets assigned a priority to use
  // for sorting.
  const outcomeSortOrder: Record<Variant, number> = {
    danger: 0,
    warning: 1,
    success: 2,
    // Note: primary and neutral are not relevant, but exist for `Variant` type to not break.
    primary: 3,
    neutral: 4,
  }

  // Sort the list so that errors appear first.
  const sortedDecoratedImportResults = decoratedImportResults.sort((a, b) => {
    return outcomeSortOrder[a.badgeVariant] - outcomeSortOrder[b.badgeVariant]
  })

  return sortedDecoratedImportResults
})

/**
 * The data structure that the diagnostics come as from the server cannot be used directly, it needs to be flattened
 * into a single array for easier handling.
 * @param diagnostics Potentially a list of lists in the form of dictionary that comes as the response the server.
 */
function flattenDiagnosticsToArray(diagnostics: Record<string, LiveOpsEventDiagnostic[]>): LiveOpsEventDiagnostic[] {
  return Object.values(diagnostics).flatMap((diagnostic) => diagnostic)
}

/**
 * User-facing information for a `LiveOpsEventImportOutcome`.
 */
interface EventImportOutcomeInfo {
  message: string
  variant: Variant
  badgeName: string
}

/**
 * Maps the server's `LiveOpsEventImportOutcome` to the user-facing `EventImportOutcomeInfo`.
 */
const outcomeToOutcomeInfoMap: Record<LiveOpsEventImportOutcome, EventImportOutcomeInfo> = {
  ConflictError: {
    message: 'The event cannot be imported because an event already exists with the same ID.',
    variant: 'danger',
    badgeName: 'Conflict',
  },
  GeneralError: {
    message: 'The event cannot be imported due to an error. More details can be found below.',
    variant: 'danger',
    badgeName: 'Error',
  },
  CreateNew: {
    message:
      'The import will cause a new event to be created in this environment, but there are warnings that you might want to take into consideration.',
    variant: 'success',
    badgeName: 'Create New',
  },
  OverwriteExisting: {
    message: 'The import will overwrite the existing event in this environment.',
    variant: 'warning',
    badgeName: 'Overwrite',
  },
  IgnoreDueToExisting: {
    message: 'The import will be ignored because the event already exists in this environment.',
    variant: 'warning',
    badgeName: 'Keep Old',
  },
}

/**
 * Get the badge variant based on the outcome from `outcomeToOutcomeInfoMap`.
 * There is a special situation where `CreateNew` contains warnings in the diagnostics, in this case we need to change
 * its badge color from success to danger.
 * @param eventResultOutcome The outcome of a specific event import result.
 * @param diagnostics The flattened diagnostic list of a specific event.
 */
function getBadgeVariant(
  eventResultOutcome: LiveOpsEventImportOutcome,
  diagnostics: LiveOpsEventDiagnostic[]
): Variant {
  if (eventResultOutcome === LiveOpsEventImportOutcome.CreateNew) {
    const eventHasWarning = diagnostics.some((diagnostic) => diagnostic.level === 'Warning')
    if (eventHasWarning) {
      return 'warning'
    }
  }

  return outcomeToOutcomeInfoMap[eventResultOutcome].variant
}

/**
 * Error to be displayed in the collapse of each item in the "Import Preview" list.
 * @param eventResultOutcome The outcome of a specific event import result.
 * @param diagnostics The flattened diagnostic list of a specific event.
 */
function createDisplayError(
  eventResultOutcome: LiveOpsEventImportOutcome,
  diagnostics: LiveOpsEventDiagnostic[]
): DisplayError | undefined {
  if (diagnostics.length > 0) {
    const displayError = new DisplayError(
      outcomeToOutcomeInfoMap[eventResultOutcome].badgeName,
      outcomeToOutcomeInfoMap[eventResultOutcome].message
    )

    // Sort order for the diagnostic levels, so that errors appear first.
    const diagnosticSortOrder: Record<LiveOpsEventDiagnosticLevel, number> = {
      Error: 0,
      Warning: 1,
      // Note: Info and Uneditable are not relevant, but exists for `LiveOpsEventDiagnosticLevel` type to not break.
      Info: 2,
      Uneditable: 3,
    }

    // Sort the diagnostics by level, then add them to the error object.
    diagnostics
      .sort((a, b) => diagnosticSortOrder[a.level] - diagnosticSortOrder[b.level])
      .forEach((diagnostic) => {
        displayError.addDetail(diagnostic.level, diagnostic.message ?? 'No reason provided.')
      })

    return displayError
  } else {
    // If there are no diagnostics, return undefined to indicate that there are no errors or warnings.
    return undefined
  }
}
</script>

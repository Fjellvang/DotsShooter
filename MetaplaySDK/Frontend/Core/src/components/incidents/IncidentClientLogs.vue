<!-- This file is part of Metaplay SDK which is released under the Metaplay SDK License. -->

<template lang="pug">
MCard(
  v-if="playerIncidentData.clientLogEntries"
  :variant="!playerIncidentData.clientLogEntries ? 'neutral' : 'primary'"
  title="Client Logs"
  :badge="errorCount > 0 ? maybePluralString(errorCount, 'error') : warningCount > 0 ? maybePluralString(warningCount, 'warning') : undefined"
  :badge-variant="errorCount > 0 ? 'danger' : warningCount > 0 ? 'warning' : undefined"
  )
  template(#header-right)
    MInputSwitch(
      :model-value="enableRichTextStyling"
      class="tw-relative tw-mr-1.5"
      name="richTextStylingEnabled"
      size="extraSmall"
      @update:model-value="enableRichTextStyling = $event"
      )
    span(class="tw-mr-1 tw-text-xs+") Rich Text Styling
    MTooltip(
      content="Unity logs contain styling elements. These can either be interpreted or displayed raw."
      noUnderline
      ): MBadge(
      shape="pill"
      ) ?

  p(
    v-if="!playerIncidentData.clientLogEntries || playerIncidentData.clientLogEntries.length === 0"
    class="tw-m-0 tw-text-center tw-italic tw-text-neutral-500"
    ) Client logs not included in this incident report.

  pre(
    v-else
    style="max-height: 30rem"
    class="log border rounded bg-light tw-w-full"
    )
    div(
      v-for="(entry, index) in playerIncidentData.clientLogEntries"
      :key="index"
      class="tw-m-0 tw-space-x-1"
      )
      span(class="text-muted") #[meta-time(:date="entry.timestamp" showAs="time")]
      span(:class="getRowStyle(entry)") {{ entry.level }}:

      span(v-if="enableRichTextStyling")
        //- \note enableColor=false because some colors (e.g. bright yellow) are hard to see in this UI.
        //-       Relying on :getRowStyle(entry) for useful coloring instead.
        span(
          v-for="part in parseRichText(entry.message, { enableColor: false })"
          :style="part.style"
          :class="getRowStyle(entry)"
          ) {{ part.text }}
      span(
        v-else
        :class="getRowStyle(entry)"
        ) {{ entry.message }}

      div(
        v-if="entry.stackTrace"
        class="ml-5"
        )
        span {{ entry.stackTrace }}
</template>

<script lang="ts" setup>
import { DateTime } from 'luxon'
import { computed, ref } from 'vue'

import { MTooltip, MBadge, MClipboardCopy, MInputSwitch, MCard } from '@metaplay/meta-ui-next'
import { maybePluralString } from '@metaplay/meta-utilities'
import { useSubscription } from '@metaplay/subscriptions'

import { getPlayerIncidentSubscriptionOptions } from '../../subscription_options/incidents'

const props = defineProps<{
  /**
   * ID of the incident to show.
   */
  incidentId: string
  /**
   * ID of the player to show.
   */
  playerId: string
}>()

const { data: playerIncidentData } = useSubscription(
  getPlayerIncidentSubscriptionOptions(props.playerId, props.incidentId)
)

const enableRichTextStyling = ref(true)

const formatClientLogsForClipboard = computed(() => {
  // Format the log entries in a similar manner as in the UI.
  return playerIncidentData.value?.clientLogEntries
    .map((incidentLine: any) => {
      // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
      const time = DateTime.fromISO(incidentLine.timestamp).toFormat('HH:mm:ss')
      const level = incidentLine.level

      let message
      if (enableRichTextStyling.value) {
        // Parse the message into styled runs, but ignore the actual styling (since this is for clipboard).
        // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
        const messageTextRuns = parseRichText(incidentLine.message, {
          enableColor: false,
        })
        message = messageTextRuns.map((run) => run.text).join('')
      } else {
        message = incidentLine.message
      }

      const stackTrace = incidentLine.stackTrace ? '\n' + incidentLine.stackTrace : ''

      // Construct a string that matches the UI rendering.
      return `${time} ${level}: ${message}${stackTrace}`.trimEnd()
    })
    .join('\n')
})

const warningCount = computed(() => {
  const logEntries = playerIncidentData.value?.clientLogEntries || []
  return logEntries.reduce((count: number, logEntry: any) => {
    return count + (logEntry.level === 'Warning' ? 1 : 0)
  }, 0)
})

const errorCount = computed(() => {
  const logEntries = playerIncidentData.value?.clientLogEntries || []
  return logEntries.reduce((count: number, logEntry: any) => {
    return count + (logEntry.level === 'Exception' || logEntry.level === 'Error' ? 1 : 0)
  }, 0)
})

function getRowStyle(entry: any): string {
  if (entry.level === 'Warning') {
    return 'text-warning'
  } else if (entry.level === 'Exception' || entry.level === 'Error') {
    return 'text-danger'
  }
  return ''
}

// Parsing of Unity's "rich text" logs.
// Only parses some prominent subset. Ignores things like <size=...> (see https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/StyledText.html).

interface RichTextStyle {
  color?: string
  'font-weight'?: string
  'font-style'?: string
}

interface RichTextRun {
  text: string
  style: any // Note: There is some oddness with types going on here. We should probably be using StyleValue.
}

interface RichTextElementSpec {
  open: RegExp
  close: RegExp
  getStyle: (matchResult: RegExpExecArray) => RichTextStyle
}

const richTextElementSpecs: RichTextElementSpec[] = [
  {
    open: /<color=(#[0-9a-fA-F]+|[a-zA-Z]+)>/,
    close: /<\/color>/,
    getStyle: (matchResult) => ({ color: matchResult[1] }),
  },
  {
    open: /<b>/,
    close: /<\/b>/,
    getStyle: (_) => ({ 'font-weight': 'bold' }),
  },
  {
    open: /<i>/,
    close: /<\/i>/,
    getStyle: (_) => ({ 'font-style': 'italic' }),
  },
]

function parseRichText(text: string, options: { enableColor: boolean }): RichTextRun[] {
  // Keep a stack of the current open elements.
  // Along with each element, we keep the style that applies to the text directly inside that element.
  // An element inside another element will augment (and possibly override parts of) the style of the
  // outer element  (for example, an inner color element will override an outer color element's color).
  const elementStack: Array<{
    elementSpec: RichTextElementSpec
    accumulatedStyle: RichTextStyle
  }> = []

  // Top-level style when not inside any element.
  const baseStyle: RichTextStyle = {}

  // Resulting text runs.
  const runs: RichTextRun[] = []

  // Suffix of the input text, containing the current remaining input.
  let remainingText = text

  // Loop until the end of the input is reached:
  // there's a break in the loop when no more interesting tags are found in the remaining input.
  while (true) {
    // Find both the next opening tag, and the closing tag of the current innermost open element.

    // Find next opening tag (if any).
    let newOpenTag = null
    for (const elementSpec of richTextElementSpecs) {
      const match = elementSpec.open.exec(remainingText)
      if (match && (!newOpenTag || match.index < newOpenTag.match.index)) {
        newOpenTag = {
          match,
          elementSpec,
        }
      }
    }

    // Find closing tag (if any) of the current innermost open element (i.e. top of the elementStack) (if any).
    let topCloseTag = null
    if (elementStack.length !== 0) {
      const elementSpec = elementStack[elementStack.length - 1].elementSpec
      const match = elementSpec.close.exec(remainingText)
      if (match) {
        topCloseTag = {
          match,
          elementSpec,
        }
      }
    }

    // Figure out which is nearer (i.e. lower position in remainingText),
    // the next opening tag or the closing tag.
    // If either is null, use the other; if both are null, that means this will be the last run.
    let nearestTag
    if (newOpenTag && topCloseTag) {
      nearestTag = newOpenTag.match.index < topCloseTag.match.index ? newOpenTag : topCloseTag
    } else {
      nearestTag = newOpenTag ?? topCloseTag
    }

    // Ending index of the current run.
    // If there is a tag, the run ends just before that tag;
    // otherwise, this is the last run and ends at the end of the remaining input.
    const runEndIndex = nearestTag ? nearestTag.match.index : remainingText.length

    // Current run's style is either the base style (if no elements are open),
    // or the total style accumulated from all the current open elements.
    const currentStyle = elementStack.length === 0 ? baseStyle : elementStack[elementStack.length - 1].accumulatedStyle

    // Add the run to the result, but only if the text is nonempty.
    if (runEndIndex !== 0) {
      runs.push({
        text: remainingText.slice(0, runEndIndex),
        style: currentStyle,
      })
    }

    // If there is no tag, this is the last run, and the result is complete.
    if (!nearestTag) {
      break
    }

    // Update the element stack according to the tag we found.
    if (nearestTag === newOpenTag) {
      // Opening tag: compute accumulated style and push to element stack.

      const newAccumulatedStyle = {
        ...currentStyle,
        ...newOpenTag.elementSpec.getStyle(newOpenTag.match),
      }
      if (!options.enableColor) {
        delete newAccumulatedStyle.color
      }

      elementStack.push({
        elementSpec: newOpenTag.elementSpec,
        accumulatedStyle: newAccumulatedStyle,
      })
    } else {
      // Closing tag: remove topmost element from stack.
      elementStack.pop()
    }

    // Continue from just after the opening tag.
    remainingText = remainingText.slice(nearestTag.match.index + nearestTag.match[0].length)
  }

  return runs
}
</script>

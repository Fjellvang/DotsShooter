<template lang="pug">
div(
  class="tw-relative tw-z-50 tw-w-64 tw-shrink-0 tw-basis-64 tw-border-r tw-border-neutral-300 tw-shadow-md"
  data-testid="timeline-navigator"
  )
  //- Month name.
  div(class="tw-flex tw-h-14 tw-items-center tw-justify-between tw-border-b tw-border-neutral-300 tw-bg-white tw-px-4")
    h2(
      class="tw-font-bold tw-text-neutral-600"
      data-testid="current-month-label"
      ) {{ timelineFirstVisibleInstant?.toFormat('LLLL yyyy') }}

    //- Today button.
    //- MTooltip(noUnderline)
      template(#content)
        p Go to today
        p Hotkey: CTRL + T

      div(
        class="tw-rounded tw-fill-neutral-700 tw-p-1 hover:tw-bg-neutral-200 hover:tw-fill-neutral-800 active:tw-bg-neutral-300 active:tw-fill-neutral-900"
        @click="$emit('todayButtonClicked')"
        data-testid="today-button"
        )
        //- Icon from Font Awesome.
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512" class="tw-size-3.5">
          <path d="M128 0c17.7 0 32 14.3 32 32V64H288V32c0-17.7 14.3-32 32-32s32 14.3 32 32V64h48c26.5 0 48 21.5 48 48v48H0V112C0 85.5 21.5 64 48 64H96V32c0-17.7 14.3-32 32-32zM0 192H448V464c0 26.5-21.5 48-48 48H48c-26.5 0-48-21.5-48-48V192zm80 64c-8.8 0-16 7.2-16 16v96c0 8.8 7.2 16 16 16h96c8.8 0 16-7.2 16-16V272c0-8.8-7.2-16-16-16H80z"/>
        </svg>

  //- Sections.
  span(v-if="visibleTimelineData && timelineRoot")
    div(
      v-for="[sectionId, section] in calculateSectionsFromRoot(visibleTimelineData.items, timelineRoot)"
      class="tw-relative"
      )
      //- Section header row.
      div(class="tw-flex tw-items-center tw-justify-between tw-space-x-2 tw-pr-1 tw-group")
        div(class="tw-whitespace-nowrap tw-grow tw-content-center tw-pl-3 tw-font-mono tw-text-xs tw-text-neutral-400 tw-overflow-hidden tw-overflow-ellipsis"
            :style="{ height: `${sectionHeightInRem}rem` }"
            ) // {{ section.metadata.displayName }}
        MEventTimelineNavigatorToolbar(
          class="tw-shrink-0"
          :isLocked="!!visibleTimelineData.items[sectionId].isImmutable"
          :id="sectionId"
          allow-inspect
          :allow-add="allowAddGroupToSection(sectionId)"
          inspect-target-name="section"
          add-target-name="group"
          :isSelected="isItemSelected(sectionId)"
          @itemInspected="(event: string) => $emit('itemInspected', event)"
          @close="$emit('close')"
          @add="() => onAddGroupToSection(sectionId)"
          )

      //- Groups.
      div(
        v-for="[groupId, group] in calculateGroupsFromSection(visibleTimelineData.items, section)"
        class="tw-relative tw-select-none tw-text-nowrap tw-border-t tw-border-neutral-300 odd:tw-bg-neutral-100 even:tw-bg-neutral-50"
        :class="{ 'tw-border-l-4': !!group.metadata.color }"
        :style="{ borderLeftColor: group.metadata.color, backgroundColor: group.metadata.color ? getWashedColor(group.metadata.color, 0.8) : undefined }"
        )
        //- Group header row.
        div(
          class="tw-flex tw-cursor-pointer tw-items-center tw-space-x-2 tw-pr-1 hover:tw-backdrop-brightness-95 active:tw-backdrop-brightness-90 tw-group"
          :class="{ 'tw-pl-3': !!group.metadata.color, 'tw-pl-4': !group.metadata.color }"
          @click="$emit('groupClicked', groupId)"
          )
          div(
            class="tw-grow tw-inline-flex tw-items-center tw-space-x-2 tw-text-xs tw-whitespace-nowrap tw-overflow-x-hidden"
            )
            div(
              :class="{ 'tw-rotate-90': isGroupExpanded(groupId) }"
              class="tw-shrink-0"
              )
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 512" class="tw-size-3">
                <path d="M278.6 233.4c12.5 12.5 12.5 32.8 0 45.3l-160 160c-12.5 12.5-32.8 12.5-45.3 0s-12.5-32.8 0-45.3L210.7 256 73.4 118.6c-12.5-12.5-12.5-32.8 0-45.3s32.8-12.5 45.3 0l160 160z"/>
              </svg>
            span(class="tw-overflow-x-hidden tw-text-ellipsis tw-grow tw-content-center"
              :style="{ height: `${groupHeightInRem}rem` }"
              ) {{ group.metadata.displayName }}

          MEventTimelineNavigatorToolbar(
            class="tw-shrink-0"
            :isLocked="!!visibleTimelineData.items[groupId].isImmutable"
            :id="groupId"
            allow-inspect
            :allow-add="allowAddRowToGroup(groupId)"
            :allow-remove="allowRemoveGroup(groupId)"
            inspect-target-name="group"
            add-target-name="row"
            remove-target-name="group"
            :isSelected="isItemSelected(groupId)"
            @itemInspected="(event: string) => $emit('itemInspected', event)"
            @close="$emit('close')"
            @add="() => onAddRowToGroup(groupId)"
            @remove="() => onRemoveGroup(groupId)"
            )

        //- Group rows.
        template(v-if="isGroupExpanded(groupId)")
          div(
            v-for="[rowId, row] in calculateRowsFromGroup(visibleTimelineData.items, group)"
            class="tw-flex tw-items-center tw-justify-between tw-space-x-2 tw-pr-1 tw-group"
            :style="{ height: `${rowHeightInRem}rem` }"
            )
            div(class="tw-grow tw-overflow-x-hidden tw-text-ellipsis tw-pl-3") {{ row.metadata.displayName }}

            MEventTimelineNavigatorToolbar(
              class="tw-shrink-0"
              :isLocked="!!visibleTimelineData.items[rowId].isImmutable"
              :id="rowId"
              allow-inspect
              :allow-remove="allowRemoveRow(rowId)"
              inspect-target-name="row"
              remove-target-name="row"
              :isSelected="isItemSelected(rowId)"
              @itemInspected="(event: string) => $emit('itemInspected', event)"
              @close="$emit('close')"
              @remove="() => onRemoveRow(rowId)"
              )
</template>

<script setup lang="ts">
import type { DateTime } from 'luxon'
import { computed } from 'vue'

import MEventTimelineNavigatorToolbar from './MEventTimelineNavigatorToolbar.vue'
import {
  timelineItemGroupHelper,
  timelineItemRowHelper,
  timelineItemSectionHelper,
  type TimelineData,
} from './MEventTimelineTypes'
import {
  findRoot,
  calculateSectionsFromRoot,
  calculateGroupsFromSection,
  calculateRowsFromGroup,
} from './MEventTimelineUtils'
import { getWashedColor, groupHeightInRem, rowHeightInRem, sectionHeightInRem } from './MEventTimelineVisibleDataUtils'
import type { ToServerCommand, ToServerCommandCreateNewItem, ToServerCommandDeleteItems } from './timelineCommands'

const emit = defineEmits({
  todayButtonClicked: () => ({ type: 'todayButtonClicked' as const }),
  groupClicked: (id: string) => ({ type: 'groupClicked', id }),
  invokeCommand: (command: ToServerCommand) => ({ type: 'command', command }),
  itemInspected: (id: string) => ({ type: 'itemInspected', id }),
  close: () => ({ type: 'close' as const }),
})

const props = defineProps<{
  visibleTimelineData?: TimelineData
  timelineFirstVisibleInstant: DateTime
  expandedGroups: string[]
  selectedItemIds: string[]
}>()

/**
 * Helper to find the root item from visibleTimelineData.
 * Undefined if visibleTimelineData is undefined.
 */
const timelineRoot = computed(() =>
  props.visibleTimelineData === undefined ? undefined : findRoot(props.visibleTimelineData.items)
)

/**
 * Checks if a group is currently expanded.
 * @param groupId ID of group to check.
 */
function isGroupExpanded(groupId: string): boolean {
  return props.expandedGroups.includes(groupId)
}

/**
 * Checks if an item is currently selected.
 * @param itemId ID of item to check.
 */
function isItemSelected(itemId: string): boolean {
  return props.selectedItemIds.includes(itemId)
}

/**
 * Check if a group is removable.
 * @param groupId ID of group to check.
 * @returns True if group is removable, reason as a string otherwise.
 */
function allowRemoveGroup(groupId: string): true | string {
  const group = timelineItemGroupHelper.getAs(groupId, props.visibleTimelineData?.items)
  return group.renderData.cannotRemoveReason ?? true
}

/**
 * Check if a row is removable.
 * @param rowId ID of row to check.
 * @returns True if row is removable, reason as a string otherwise.
 */
function allowRemoveRow(rowId: string): true | string {
  const row = timelineItemRowHelper.getAs(rowId, props.visibleTimelineData?.items)
  return row.renderData.cannotRemoveReason ?? true
}

/**
 * Check if a group can be added to a section.
 * @param sectionId ID of section to check.
 * @returns True if group can be added, reason as a string otherwise.
 */
function allowAddGroupToSection(sectionId: string): true | string {
  return (
    !timelineItemSectionHelper.getAs(sectionId, props.visibleTimelineData?.items)?.isImmutable ||
    'Cannot add to immutable section.'
  )
}

/**
 * Add a new group to a section.
 * @param sectionId ID of section to add group to.
 */
function onAddGroupToSection(sectionId: string): void {
  const command: ToServerCommandCreateNewItem = {
    commandType: 'createNewItem',
    itemType: 'group',
    itemConfig: {},
    parentId: sectionId,
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    parentVersion: props.visibleTimelineData!.items[sectionId].version,
  }
  emit('invokeCommand', command)
}

/**
 * Remove a group.
 * @param groupId ID of group to remove.
 */
function onRemoveGroup(groupId: string): void {
  const { parentId, parentItem } = timelineItemSectionHelper.getParentAs(groupId, props.visibleTimelineData?.items)
  const command: ToServerCommandDeleteItems = {
    commandType: 'deleteItems',
    items: [
      {
        targetId: groupId,
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
        currentVersion: props.visibleTimelineData!.items[groupId].version,
        parentId,
        parentVersion: parentItem.version,
      },
    ],
  }
  emit('invokeCommand', command)
}

/**
 * Check if a row can be added to a group.
 * @param groupId ID of group to check.
 * @returns True if group can be added, reason as a string otherwise.
 */
function allowAddRowToGroup(groupId: string): true | string {
  return (
    !timelineItemGroupHelper.getAs(groupId, props.visibleTimelineData?.items)?.isImmutable ||
    'Cannot add to immutable group.'
  )
}

/**
 * Add a new row to a group.
 * @param groupId ID of group to add row to.
 */
function onAddRowToGroup(groupId: string): void {
  const command: ToServerCommandCreateNewItem = {
    commandType: 'createNewItem',
    itemType: 'row',
    itemConfig: {},
    parentId: groupId,
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    parentVersion: props.visibleTimelineData!.items[groupId].version,
  }
  emit('invokeCommand', command)
}

/**
 * Remove a row.
 * @param rowId ID of row to remove.
 */
function onRemoveRow(rowId: string): void {
  const { parentId, parentItem } = timelineItemGroupHelper.getParentAs(rowId, props.visibleTimelineData?.items)
  const command: ToServerCommandDeleteItems = {
    commandType: 'deleteItems',
    items: [
      {
        targetId: rowId,
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
        currentVersion: props.visibleTimelineData!.items[rowId].version,
        parentId,
        parentVersion: parentItem.version,
      },
    ],
  }
  emit('invokeCommand', command)
}
</script>

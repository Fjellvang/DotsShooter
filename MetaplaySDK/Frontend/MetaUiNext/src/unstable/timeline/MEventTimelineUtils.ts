// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type {
  TimelineItem,
  TimelineItemGroup,
  TimelineItemRoot,
  TimelineItemRow,
  TimelineItemSection,
} from './MEventTimelineTypes'

/**
 * Finds the root item in the timeline data.
 * @param timelineItems List of timeline items.
 * @returns Root item.
 * @throws Error if no root item is found.
 */
export function findRoot(timelineItems: Record<string, TimelineItem>): TimelineItemRoot {
  for (const item of Object.values(timelineItems)) {
    if (item.itemType === 'root') {
      return item as TimelineItemRoot
    }
  }

  throw new Error('No root element found in timeline data')
}

/**
 * Returns a list of `TimelineItemSection` from the given root `TimelineItemRoot`.
 * @param timelineItems List of timeline items.
 * @param root Root item.
 * @returns List of `TimelineItemSection`.
 */
export function calculateSectionsFromRoot(
  timelineItems: Record<string, TimelineItem>,
  root: TimelineItemRoot
): Array<[string, TimelineItemSection]> {
  console.assert(!!(root as unknown) && (root as TimelineItem).itemType === 'root')

  return root.hierarchy.childIds.map((sectionId) => {
    const possibleSection = timelineItems[sectionId]
    console.assert(!!possibleSection)
    console.assert(possibleSection.itemType === 'section')
    return [sectionId, possibleSection as TimelineItemSection]
  })
}

/**
 * Returns a list of `TimelineItemGroup`s from the given section `TimelineItemSection`.
 * @param timelineItems List of timeline items.
 * @param section Section item.
 * @returns List of `TimelineItemGroup`.
 */
export function calculateGroupsFromSection(
  timelineItems: Record<string, TimelineItem>,
  section: TimelineItemSection
): Array<[string, TimelineItemGroup]> {
  console.assert(!!(section as unknown) && (section as TimelineItem).itemType === 'section')

  return section.hierarchy.childIds.map((groupId) => {
    const possibleGroup = timelineItems[groupId]
    console.assert(!!possibleGroup)
    console.assert(possibleGroup.itemType === 'group')
    return [groupId, possibleGroup as TimelineItemGroup]
  })
}

/**
 * Returns a list of `TimelineItemRow`s from the given group `TimelineItemGroup`.
 * @param timelineItems List of timeline items.
 * @param section Group item.
 * @returns List of `TimelineItemRow`.
 */
export function calculateRowsFromGroup(
  timelineItems: Record<string, TimelineItem>,
  group: TimelineItemGroup
): Array<[string, TimelineItemRow]> {
  console.assert(!!(group as unknown) && (group as TimelineItem).itemType === 'group')

  return group.hierarchy.childIds.map((rowId) => {
    const possibleRow = timelineItems[rowId]
    console.assert(!!possibleRow)
    console.assert(possibleRow.itemType === 'row')
    return [rowId, possibleRow as TimelineItemRow]
  })
}

/**
 * Returns a list of `TimelineItem` from the given row `TimelineItemRow`.
 * @param timelineItems List of timeline items.
 * @param section Row item.
 * @returns List of `TimelineItem`.
 */
export function calculateItemsFromRow(
  timelineItems: Record<string, TimelineItem>,
  row: TimelineItemRow
): Array<[string, TimelineItem]> {
  console.assert(!!(row as unknown) && (row as TimelineItem).itemType === 'row')

  return row.hierarchy.childIds.map((itemId) => {
    const possibleItem = timelineItems[itemId]
    console.assert(!!possibleItem)
    return [itemId, possibleItem]
  })
}

/**
 * Helper class for working with timeline items. Intended to make it easy to get items and their parents/children while
 * casting them to the correct types and checking for errors. There are a bunch of `eslint-disable`s in here, which is
 * nicer than having them pollute other code.
 * @example TimelineItemHelper<TimelineItemSection>('section').getAs('section:0', timelineItems) -> TimelineItemSection
 */
export class TimelineItemHelper<Type extends TimelineItem> {
  typeName: string
  constructor(typeName: string) {
    this.typeName = typeName
  }

  /**
   * Get the item as the expected type.
   * @param itemId ID of the item.
   * @param timelineItems List of timeline items.
   * @returns Item cast to the expected type.
   * @throws If item cannot be found, is not of the expected type, or no items are supplied.
   */
  getAs(itemId: string, timelineItems?: Record<string, TimelineItem | undefined>): Type {
    console.assert(!!timelineItems, 'No items supplied')
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    const item = timelineItems![itemId] as Type
    console.assert(!!item, 'Item not found')
    console.assert(this.typeName === '' || item.itemType === this.typeName, 'Item is not of the expected type')
    return item
  }

  /**
   * Get the item's parent as the expected type.
   * @param itemId ID of the item.
   * @param timelineItems List of timeline items.
   * @returns Parent item cast to the expected type.
   * @throws If item cannot be found, if the parent cannot be found or is not of the expected type, or no items are supplied.
   */
  getParentAs(
    itemId: string,
    timelineItems?: Record<string, TimelineItem>
  ): { parentId: string; parentItem: Type; childIndex: number } {
    console.assert(!!timelineItems, 'No items supplied')

    const item = this.getRaw(itemId, timelineItems)

    // eslint-disable-next-line @typescript-eslint/no-unnecessary-condition
    console.assert(!!item.hierarchy?.parentId, 'Item has no parent')
    // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment, @typescript-eslint/no-explicit-any, @typescript-eslint/no-unsafe-member-access
    const parentId: string = (item as any).hierarchy.parentId

    const parentItem = this.getAs(parentId, timelineItems)

    console.assert(!!parentItem.hierarchy.childIds, 'Parent has no children')
    // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment, @typescript-eslint/no-unsafe-call, @typescript-eslint/no-unsafe-member-access, @typescript-eslint/no-explicit-any
    const childIndex: number = (parentItem as any).hierarchy.childIds.indexOf(itemId)
    console.assert(childIndex >= 0, 'Item not found in parent')

    return { parentId, parentItem, childIndex }
  }

  /**
   * Gets the child at the given index as the expected type.
   * @param itemId ID of the item.
   * @param childIndex Index of the child.
   * @param timelineItems List of timeline items.
   * @returns Child item cast to the expected type.
   * @throws If item cannot be found, if the child cannot be found or is not of the expected type, or no items are supplied.
   */
  getChildAs(
    itemId: string,
    childIndex: number,
    timelineItems?: Record<string, TimelineItem>
  ): { childId: string; childItem: Type } {
    console.assert(!!timelineItems, 'No items supplied')

    const item = this.getRaw(itemId, timelineItems)
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    const childIds: string[] = item.hierarchy.childIds! as []
    console.assert(!!childIds, 'Item has no children')

    console.assert(childIndex >= 0 && childIndex < childIds.length, 'Child index out of bounds')
    const childId: string = childIds[childIndex]
    const childItem = this.getAs(childId, timelineItems)

    return { childId, childItem }
  }

  /**
   * Private helper to get item.
   * @param itemId ID of the item.
   * @param timelineItems List of timeline items.
   * @returns Item.
   * @throws If item cannot be found or no items are supplied.
   */
  private getRaw(itemId: string, timelineItems?: Record<string, TimelineItem>): TimelineItem {
    console.assert(!!timelineItems, 'No items supplied')
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    const item = timelineItems![itemId]
    console.assert(!!item, 'Item not found')
    return item
  }
}

/**
 * Figure out if an item's parent is immutable.
 * @param itemId Item to query.
 * @param timelineItems List of timeline items.
 * @returns True if the parent is immutable.
 * @throws Error if the item's parent is not found.
 */
export function isParentImmutable(itemId: string, timelineItems?: Record<string, TimelineItem>): boolean {
  const timelineItemHelper = new TimelineItemHelper<TimelineItem>('')
  const { parentItem } = timelineItemHelper.getParentAs(itemId, timelineItems)
  return !!parentItem.isImmutable
}

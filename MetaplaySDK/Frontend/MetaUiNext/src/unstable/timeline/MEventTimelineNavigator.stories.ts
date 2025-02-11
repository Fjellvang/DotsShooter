import type { Meta, StoryObj } from '@storybook/vue3'

import { usePermissions } from '../../composables/usePermissions'
import MEventTimeline from './MEventTimeline.vue'
import { ColorPickerPalette } from './MEventTimelineColorUtils'
import type { TimelineItemGroup, TimelineItemRoot, TimelineItemRow, TimelineItemSection } from './MEventTimelineTypes'

const meta: Meta<typeof MEventTimeline> = {
  component: MEventTimeline,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component: 'A subcomponent of the timeline that displays the left panel with the timeline sections and groups.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof MEventTimeline>

const defaultTimelineData = {
  startInstantIsoString: '1999-12-21T00:00:00.000Z',
  endInstantIsoString: '2000-01-29T00:00:00.000Z',
  items: {
    'root:0': {
      itemType: 'root',
      version: 0,
      hierarchy: {
        childIds: ['section:1'],
      },
    } satisfies TimelineItemRoot,
    'section:1': {
      itemType: 'section',
      version: 0,
      metadata: {
        displayName: 'Top level section',
      },
      hierarchy: {
        parentId: 'root:0',
        childIds: ['group:1', 'group:2', 'group:3', 'group:4'],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemSection,
    'group:1': {
      itemType: 'group',
      version: 0,
      metadata: {
        displayName: 'Group 1',
      },
      hierarchy: {
        parentId: 'section:1',
        childIds: ['row:1', 'row:2'],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemGroup,
    'row:1': {
      itemType: 'row',
      version: 0,
      metadata: {
        displayName: 'Row 1 of group 1',
      },
      hierarchy: {
        parentId: 'group:1',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemRow,
    'row:2': {
      itemType: 'row',
      version: 0,
      metadata: {
        displayName: 'Row 2 of group 1',
      },
      hierarchy: {
        parentId: 'group:1',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemRow,
    'group:2': {
      itemType: 'group',
      version: 0,
      metadata: {
        displayName: 'Group 2',
      },
      hierarchy: {
        parentId: 'section:1',
        childIds: ['row:3', 'row:4'],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemGroup,
    'row:3': {
      itemType: 'row',
      version: 0,
      metadata: {
        displayName: 'Row 1 of group 2',
      },
      hierarchy: {
        parentId: 'group:2',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemRow,
    'row:4': {
      itemType: 'row',
      version: 0,
      metadata: {
        displayName: 'Row 2 of group 2',
      },
      hierarchy: {
        parentId: 'group:2',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemRow,
    'group:3': {
      itemType: 'group',
      version: 0,
      metadata: {
        displayName: 'Empty group',
      },
      hierarchy: {
        parentId: 'section:1',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemGroup,
    // Immutable group
    'group:4': {
      itemType: 'group',
      version: 0,
      metadata: {
        displayName: 'Immutable group',
      },
      isImmutable: true,
      hierarchy: {
        parentId: 'section:1',
        childIds: ['row:5'],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemGroup,
    // Immutable row
    'row:5': {
      itemType: 'row',
      version: 0,
      metadata: {
        displayName: 'Immutable row',
      },
      isImmutable: true,
      hierarchy: {
        parentId: 'group:4',
        childIds: [],
      },
      renderData: {
        cannotRemoveReason: null,
      },
    } satisfies TimelineItemRow,
  },
}

/**
 * Navigator starts collapsed by default. It has three levels of hierarchy: sections, groups, and rows.
 */
export const Default: Story = {
  args: {
    selectedItemDetails: {},
    timelineData: defaultTimelineData,
  },
}

export const StartExpanded: Story = {
  args: {
    startExpanded: true,
    selectedItemDetails: {},
    timelineData: defaultTimelineData,
  },
}

export const WithPermissions: Story = {
  render: (args) => ({
    components: { MEventTimeline },
    setup: () => {
      usePermissions().setPermissions(['api.liveops_events.edit', 'api.liveops_events.view'])

      return { args }
    },
    template: `<MEventTimeline v-bind="args"></MEventTimeline>`,
  }),
  args: {
    startExpanded: true,
    selectedItemDetails: {},
    timelineData: defaultTimelineData,
  },
}

export const WithColor: Story = {
  args: {
    startExpanded: true,
    selectedItemDetails: {},
    timelineData: {
      startInstantIsoString: '1999-12-21T00:00:00.000Z',
      endInstantIsoString: '2000-01-29T00:00:00.000Z',
      items: {
        'root:0': {
          itemType: 'root',
          version: 0,
          hierarchy: {
            childIds: ['section:1'],
          },
        } satisfies TimelineItemRoot,
        'section:1': {
          itemType: 'section',
          version: 0,
          metadata: {
            displayName: 'Top level section',
          },
          hierarchy: {
            parentId: 'root:0',
            childIds: Object.keys(ColorPickerPalette).map((colorName) => `group:${colorName}`),
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemSection,
        ...Object.fromEntries(
          Object.entries(ColorPickerPalette).map(([colorName, hexCode]) => [
            `group:${colorName}`,
            {
              itemType: 'group',
              version: 0,
              metadata: {
                displayName: `Group for ${colorName}`,
                color: hexCode,
              },
              hierarchy: {
                parentId: 'section:1',
                childIds: [`row:${colorName}`],
              },
              renderData: {
                cannotRemoveReason: null,
              },
            } satisfies TimelineItemGroup,
          ])
        ),
        ...Object.fromEntries(
          Object.keys(ColorPickerPalette).map((colorName) => [
            `row:${colorName}`,
            {
              itemType: 'row',
              version: 0,
              metadata: {
                displayName: `Row for ${colorName}`,
              },
              hierarchy: {
                parentId: `group:${colorName}`,
                childIds: [],
              },
              renderData: {
                cannotRemoveReason: null,
              },
            } satisfies TimelineItemRow,
          ])
        ),
      },
    },
  },
}

export const Overflows: Story = {
  args: {
    startExpanded: true,
    selectedItemDetails: {},
    timelineData: {
      startInstantIsoString: '1999-12-21T00:00:00.000Z',
      endInstantIsoString: '2000-01-29T00:00:00.000Z',
      items: {
        'root:0': {
          itemType: 'root',
          version: 0,
          hierarchy: {
            childIds: ['section:1', 'section:2'],
          },
        } satisfies TimelineItemRoot,
        'section:1': {
          itemType: 'section',
          version: 0,
          metadata: {
            displayName: 'Section with a long name that overflows',
          },
          hierarchy: {
            parentId: 'root:0',
            childIds: ['group:1'],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemSection,
        'group:1': {
          itemType: 'group',
          version: 0,
          metadata: {
            displayName: 'A group with a long name that overflows',
          },
          hierarchy: {
            parentId: 'section:1',
            childIds: ['row:1'],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemGroup,
        'row:1': {
          itemType: 'row',
          version: 0,
          metadata: {
            displayName: 'A row with a long name that overflows',
          },
          hierarchy: {
            parentId: 'group:1',
            childIds: [],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemRow,
        'section:2': {
          itemType: 'section',
          version: 0,
          metadata: {
            displayName: 'Sectionwithalongnamethatoverflowsforreal',
          },
          hierarchy: {
            parentId: 'root:0',
            childIds: ['group:2'],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemSection,
        'group:2': {
          itemType: 'group',
          version: 0,
          metadata: {
            displayName: 'Groupwithalongnamethatoverflowsforreal',
          },
          hierarchy: {
            parentId: 'section:2',
            childIds: ['row:2'],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemGroup,
        'row:2': {
          itemType: 'row',
          version: 0,
          metadata: {
            displayName: 'Rowwithalongnamethatoverflowsforreal',
          },
          hierarchy: {
            parentId: 'group:2',
            childIds: [],
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemRow,
      },
    },
  },
}

export const LargeAmountOfData: Story = {
  args: {
    startExpanded: false,
    selectedItemDetails: {},
    timelineData: {
      startInstantIsoString: '1999-12-21T00:00:00.000Z',
      endInstantIsoString: '2000-01-29T00:00:00.000Z',
      items: {
        'root:0': {
          itemType: 'root',
          version: 0,
          hierarchy: {
            childIds: ['section:1'],
          },
        } satisfies TimelineItemRoot,
        'section:1': {
          itemType: 'section',
          version: 0,
          metadata: {
            displayName: 'Top level section',
          },
          hierarchy: {
            parentId: 'root:0',
            childIds: Array.from({ length: 100 }, (_, i) => `group:${i}`),
          },
          renderData: {
            cannotRemoveReason: null,
          },
        } satisfies TimelineItemSection,
        ...Object.fromEntries(
          Array.from({ length: 100 }, (_, i) => [
            `group:${i}`,
            {
              itemType: 'group',
              version: 0,
              metadata: {
                displayName: `Group ${i}`,
              },
              hierarchy: {
                parentId: 'section:1',
                childIds: Array.from({ length: 20 }, (_, j) => `row:${i}-${j}`),
              },
              renderData: {
                cannotRemoveReason: null,
              },
            } satisfies TimelineItemGroup,
          ])
        ),
        ...Object.fromEntries(
          Array.from({ length: 100 }, (_, i) =>
            Array.from({ length: 20 }, (_, j) => [
              `row:${i}-${j}`,
              {
                itemType: 'row',
                version: 0,
                metadata: {
                  displayName: `Row ${i}-${j}`,
                },
                hierarchy: {
                  parentId: `group:${i}`,
                  childIds: [],
                },
                renderData: {
                  cannotRemoveReason: null,
                },
              } satisfies TimelineItemRow,
            ])
          ).flat()
        ),
      },
    },
  },
}

// TODO: keyboard navigation story, etc.

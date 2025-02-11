// import type { TimelineData } from './MEventTimelineTypes'

/*
export const liveOpsScenario1: TimelineData = {
  startInstantIsoString: '1999-12-01T00:00:00.000Z',
  endInstantIsoString: '2000-03-01T00:00:00.000Z',
  sections: [
    {
      displayName: 'Section 1',
      id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
      order: 1,
      isImmutable: true,
      isLocked: true,
      groups: [
        {
          displayName: 'Group 1',
          id: '9c858901-8a57-4791-81fe-4c455b099bc9',
          order: 1,
          isImmutable: true,
          isLocked: true,
          colorInHex: '#FF5733',
          rows: [
            {
              displayName: 'Row 1',
              id: '7c9e6679-7425-40de-944b-e07fc1f90ae7',
              order: 1,
              colorInHex: '#D3D3D3',
              isImmutable: true,
              isLocked: true,
              items: [
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 1',
                  id: '123e4567-e89b-12d3-a456-426614174000',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'concluded',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-01T00:00:00.000Z',
                    endInstantIsoString: '1999-12-08T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '1999-12-01T00:00:00.000Z',
                    concludeInstantIsoString: '1999-12-08T00:00:00.000Z',
                  },
                  participantCount: 3098674,
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 2',
                  id: '123e4567-e89b-12d3-a456-426614174001',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'concluded',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-08T00:00:00.000Z',
                    endInstantIsoString: '1999-12-18T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '1999-12-08T00:00:00.000Z',
                    concludeInstantIsoString: '1999-12-18T00:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 3',
                  id: '123e4567-e89b-12d3-a456-426614174002',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'concluded',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-18T00:00:00.000Z',
                    endInstantIsoString: '1999-12-28T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '1999-12-18T00:00:00.000Z',
                    concludeInstantIsoString: '1999-12-28T00:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 4',
                  id: '123e4567-e89b-12d3-a456-426614174003',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'active',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-28T00:00:00.000Z',
                    endInstantIsoString: '2000-01-06T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    currentPhase: 'active',
                    activateInstantIsoString: '1999-12-28T00:00:00.000Z',
                    concludeInstantIsoString: '2000-01-06T00:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 5',
                  id: '123e4567-e89b-12d3-a456-426614174004',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-06T00:00:00.000Z',
                    endInstantIsoString: '2000-01-16T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-06T00:00:00.000Z',
                    concludeInstantIsoString: '2000-01-16T00:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 6',
                  id: '123e4567-e89b-12d3-a456-426614174005',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-16T00:00:00.000Z',
                    endInstantIsoString: '2000-01-23T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-16T00:00:00.000Z',
                    concludeInstantIsoString: '2000-01-23T00:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 7',
                  id: '123e4567-e89b-12d3-a456-426614174006',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#D3D3D3',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-23T00:00:00.000Z',
                    endInstantIsoString: '2000-02-02T00:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-23T00:00:00.000Z',
                    concludeInstantIsoString: '2000-02-02T00:00:00.000Z',
                  },
                },
              ],
            },
          ],
        },
        {
          displayName: 'Group 2',
          id: '123e4567-e89b-12d3-a456-426614174007',
          order: 2,
          isImmutable: true,
          isLocked: true,
          colorInHex: '#FFA500',
          rows: [
            {
              displayName: 'Row 2',
              id: '123e4567-e89b-12d3-a456-426614174008',
              order: 1,
              colorInHex: '#FFA500',
              isImmutable: true,
              isLocked: true,
              items: [
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 8',
                  id: '123e4567-e89b-12d3-a456-426614174009',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#FFA500',
                  state: 'concluded',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-07T08:00:00.000Z',
                    endInstantIsoString: '1999-12-31T07:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '1999-12-07T08:00:00.000Z',
                    concludeInstantIsoString: '1999-12-31T07:00:00.000Z',
                  },
                  participantCount: 3098674,
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 9',
                  id: '123e4567-e89b-12d3-a456-426614174010',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#FFA500',
                  state: 'active',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-31T07:00:00.000Z',
                    endInstantIsoString: '2000-01-28T07:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    currentPhase: 'active',
                    activateInstantIsoString: '1999-12-31T07:00:00.000Z',
                    concludeInstantIsoString: '2000-01-28T07:00:00.000Z',
                  },
                  participantCount: 3478923,
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 10',
                  id: '123e4567-e89b-12d3-a456-426614174011',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#FFA500',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-28T07:00:00.000Z',
                    endInstantIsoString: '2000-02-26T07:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-28T07:00:00.000Z',
                    concludeInstantIsoString: '2000-02-26T07:00:00.000Z',
                  },
                },
              ],
            },
          ],
        },
        {
          displayName: 'Group 3',
          id: '123e4567-e89b-12d3-a456-426614174012',
          order: 3,
          isImmutable: true,
          isLocked: true,
          colorInHex: '#ADD8E6',
          rows: [
            {
              displayName: 'Row 3',
              id: '123e4567-e89b-12d3-a456-426614174013',
              order: 1,
              colorInHex: '#ADD8E6',
              isImmutable: true,
              isLocked: true,
              items: [
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 11',
                  id: '123e4567-e89b-12d3-a456-426614174014',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#ADD8E6',
                  state: 'concluded',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '1999-12-13T07:00:00.000Z',
                    endInstantIsoString: '1999-12-28T07:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '1999-12-13T07:00:00.000Z',
                    concludeInstantIsoString: '1999-12-28T07:00:00.000Z',
                  },
                  participantCount: 3098674,
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 12',
                  id: '123e4567-e89b-12d3-a456-426614174015',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#ADD8E6',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-02-02T08:00:00.000Z',
                    endInstantIsoString: '2000-02-14T08:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-02-02T08:00:00.000Z',
                    concludeInstantIsoString: '2000-02-14T08:00:00.000Z',
                  },
                },
              ],
            },
            {
              displayName: 'Row 4',
              id: '123e4567-e89b-12d3-a456-426614174016',
              order: 1,
              colorInHex: '#ADD8E6',
              isImmutable: true,
              isLocked: true,
              items: [
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 13',
                  id: '123e4567-e89b-12d3-a456-426614174017',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#ADD8E6',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-04T12:00:00.000Z',
                    endInstantIsoString: '2000-01-10T12:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-04T12:00:00.000Z',
                    concludeInstantIsoString: '2000-01-10T12:00:00.000Z',
                  },
                },
                {
                  timelineItemType: 'liveopsEvent',
                  displayName: 'Event 14',
                  id: '123e4567-e89b-12d3-a456-426614174018',
                  liveOpsEventType: 'MOCK_TYPE',
                  colorInHex: '#ADD8E6',
                  state: 'scheduled',
                  isImmutable: true,
                  isLocked: false,
                  isRecurring: false,
                  parameters: {},
                  timelinePosition: {
                    startInstantIsoString: '2000-01-18T05:00:00.000Z',
                    endInstantIsoString: '2000-01-25T05:00:00.000Z',
                  },
                  schedule: {
                    timeMode: 'utc',
                    activateInstantIsoString: '2000-01-18T05:00:00.000Z',
                    concludeInstantIsoString: '2000-01-25T05:00:00.000Z',
                  },
                },
              ],
            },
          ],
        },
      ],
    },
  ],
}
*/

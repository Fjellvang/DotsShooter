// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { Locator, Page } from '@metaplay/playwright-config'
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Player Event Stream', () => {
  test.beforeEach('Navigate to player details and open log tab', async ({ page, freshTestPlayer }) => {
    // Navigate to player page and select the tab where the event log is displayed.
    await page.goto(`/players/${freshTestPlayer}`)
    await page.getByTestId('tab-1').click()
  })

  test('Events show in the card', async ({ page, freshTestPlayer }) => {
    // Locators.
    const entityEventLogCard = page.getByTestId('entity-event-log-card')

    // Change player's name to create an event.
    await editNameAndWaitForEvent(page, freshTestPlayer, 'eventStream1')

    // Check that the event is listed.
    await findNameChangeEvent(entityEventLogCard, 'eventStream1', true)
  })

  test('Events are buffered when updates are paused', async ({ page, freshTestPlayer }) => {
    // The polling period for events is quite long, so this test can be slow.
    test.slow()

    // Locators.
    const entityEventLogCard = page.getByTestId('entity-event-log-card')
    const playPauseButton = entityEventLogCard.getByTestId('play-pause-button')

    // Event log card is blank if there are no events, so we need to be sure that it is awake.
    await editNameAndWaitForEvent(page, freshTestPlayer, 'eventStream1')

    // Event log card should initially be in a non-paused state.
    await expect(playPauseButton).toHaveText('Pause updates')

    // Pause the stream and check that the button text changes to reflect the new state.
    await playPauseButton.click()
    await expect(playPauseButton).toHaveText('Resume updates')

    // How many events do we have right now?
    const pausedEventCount = await countEvents(entityEventLogCard)

    // Cue up another event while the stream is paused.
    await editNameAndWaitForEvent(page, freshTestPlayer, 'eventStream2')

    // Check that no new events arrived.
    expect(await countEvents(entityEventLogCard)).toEqual(pausedEventCount)
    await findNameChangeEvent(entityEventLogCard, 'eventStream2', false)

    // Resume the stream and check that the button text changes to reflect the new state.
    await playPauseButton.click()
    await expect(playPauseButton).toHaveText('Pause updates')

    // Event will have arrived by now and un-pausing should make it immediately visible. We can't assume that we now
    // have exactly n+1 events because other player events could also have happened in the meantime.
    expect(await countEvents(entityEventLogCard)).toBeGreaterThan(pausedEventCount)
    await findNameChangeEvent(entityEventLogCard, 'eventStream2', true)
  })
})

// Helper Functions ---------------------------------------------------------------------------------------------------

/**
 * Change the player's name and wait for the name change event to be received.
 * @param page Playwright Page object.
 * @param playerId Player ID.
 * @param newName New name for the player.
 */
async function editNameAndWaitForEvent(page: Page, playerId: string, newName: string): Promise<void> {
  await test.step('Edit name and wait for event', async () => {
    // Open Edit Name modal.
    await page.getByTestId('action-edit-name-button').click()

    // Type the new name to the text input field.
    await page.getByTestId('name-input').fill(newName)

    // Click the `Ok` button to change the name, then wait for the dash to poll the server and receive the event. The
    // poll period is 10 seconds, so this could take a while.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(async (response) => {
        if (response.url().includes(`/api/players/${playerId}/eventLog`)) {
          const responseBody = await response.body()
          return responseBody.includes(`to ${newName} by`)
        }
        return false
      }),
      clickMButton(page.getByTestId('action-edit-name-modal-ok-button')),
    ])
  })
}

/**
 * Counts the number of events in an event log card.
 * @param entityEventLogCard Locator that points to the event log card.
 * @returns Number of events.
 */
async function countEvents(entityEventLogCard: Locator): Promise<number> {
  return await test.step('Count events', async () => {
    const eventCount = await entityEventLogCard.getByTestId('badge-text').textContent()
    if (eventCount === null) {
      throw new Error('Failed to retrieve event count label.')
    }
    return parseInt(eventCount)
  })
}

/**
 * Find if a name change event is or is not in the event log card.
 * @param entityEventLogCard Locator that points to the event log card.
 * @param name Name to search for in the event log.
 * @param expectToFind True if the name change event is expected to be found, false otherwise.
 */
async function findNameChangeEvent(entityEventLogCard: Locator, name: string, expectToFind: boolean): Promise<void> {
  await test.step('Find name change event', async () => {
    const event = entityEventLogCard.getByTestId('event-PlayerEventNameChanged').first()
    await event.scrollIntoViewIfNeeded()

    if (expectToFind) {
      // Check that the event is listed.
      await expect(event).toContainText(`to ${name} by`, { useInnerText: true })
    } else {
      // Check that the event is not listed.
      await expect(event).not.toContainText(`to ${name} by`, { useInnerText: true })
    }
  })
}

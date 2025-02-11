// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('In-Game Event Details', () => {
  test.beforeAll('Skip if there are no in-game events', async ({ featureFlags, request, apiURL }) => {
    test.skip(!featureFlags.ingameEvents, 'In-game events feature not enabled in this deployment')

    // Check if in-game event lists contains at least one in-game event.
    const response = await request.get(`${apiURL}/activables`)
    const data = (await response.json()) as Record<string, unknown>
    test.skip(Object.keys(data).length === 0, 'No in-game events available to test in this deployment')
  })

  test.beforeEach('Navigate to first available in-game event', async ({ page }) => {
    // Navigate to in-game events list page.
    await page.goto('/activables/Event')

    // Get the first navigation link button in the in-game event list card.
    const navigationLinkButton = page.getByTestId('view-event').first()

    // Check that the navigation link button has the text `View event`.
    await expect(navigationLinkButton).toHaveText('View event')

    // Click the navigation link button to go a specific in-game event detail page.
    await navigationLinkButton.click()
  })

  test('Overview, configuration and targeting cards render on in-game event detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('activable-detail-overview-card')).toBeVisible()

    // Get the game specific config card and check that it renders.
    await expect(page.getByTestId('activable-detail-game-specific-config-card')).toBeVisible()

    // Get the configuration card and check that it renders.
    await expect(page.getByTestId('activable-detail-configuration-card')).toBeVisible()

    // Get the segments card and check that it renders.
    await expect(page.getByTestId('segments-card')).toBeVisible()

    // Get the player conditions card and check that it renders.
    await expect(page.getByTestId('player-conditions-card')).toBeVisible()
  })
})

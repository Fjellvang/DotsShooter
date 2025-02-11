// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('LiveOps Event Details', () => {
  test.beforeAll('Skip if there are no liveOpsEvents', async ({ featureFlags, request, apiURL }) => {
    test.skip(!featureFlags.liveOpsEvents, 'LiveOps events feature not enabled in this deployment')

    // Check if liveOpsEvents lists contains at least one event.
    const response = await request.get(`${apiURL}/liveOpsEvents`)
    const data = (await response.json()) as { upcomingEvents: unknown[]; ongoingAndPastEvents: unknown[] }
    test.skip(
      data.upcomingEvents.length === 0 && data.ongoingAndPastEvents.length === 0,
      'No liveOps events available to test in this deployment'
    )
  })

  test.beforeEach('Navigate to first available liveOps event', async ({ page }) => {
    // Navigate to liveOps events list page.
    await page.goto('/liveOpsEvents')

    // Get the first navigation link button in the upcoming events or past events list card.
    const navigationLinkButton = page.getByTestId('view-live-ops-event').first()

    // Check that the navigation link button has the text `View event`.
    await expect(navigationLinkButton).toHaveText('View event')

    // Click the navigation link button to go a specific liveOps event detail page.
    await navigationLinkButton.click()
  })

  test('Overview, config, targeting and audit log cards render on liveOps event detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('live-ops-event-detail-overview-card')).toBeVisible()

    // Get the event configuration card and check that it renders.
    await expect(page.getByTestId('live-ops-event-detail-configuration-card')).toBeVisible()

    // Get the event targeting card and check that it renders.
    await expect(page.getByTestId('targeting-card')).toBeVisible()

    // Get the player list card and check that it renders.
    await expect(page.getByTestId('player-list-card')).toBeVisible()

    // Get the audit log card and check that it renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })
})

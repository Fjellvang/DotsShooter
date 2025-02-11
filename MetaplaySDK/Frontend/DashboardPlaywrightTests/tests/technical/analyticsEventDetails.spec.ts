// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Analytics Event Details', () => {
  test.beforeEach('Navigate to first available analytics event', async ({ page }) => {
    // Navigate to the analytics events list page.
    await page.goto('/analyticsEvents')

    // Get the first navigation link button in either core or custom event list card.
    const navigationLinkButton = page.getByTestId('analytics-details-link').first()

    // Check that the navigation link button contains the text `View details`.
    await expect(navigationLinkButton).toHaveText('View details')

    // Navigate to the specific analytics event detail page.
    await navigationLinkButton.click()
  })

  test('Overview and BigQuery event cards render on detail page', async ({ page }) => {
    // Get the overview card and check it renders.
    await expect(page.getByTestId('analytics-event-detail-overview-card')).toBeVisible()

    // Get the big query event card and check that it is rendered.
    await expect(page.getByTestId('big-query-event-card')).toBeVisible()
  })
})

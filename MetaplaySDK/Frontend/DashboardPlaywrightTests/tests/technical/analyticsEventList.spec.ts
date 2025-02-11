// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Analytics Events List', () => {
  test.beforeEach('Navigate to analytics events list', async ({ page }) => {
    // Navigate to the analytics events list page.
    await page.goto('/analyticsEvents')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview, core events and custom events cards render on list page', async ({ page }) => {
    // Get the overview card and check that it is rendered.
    await expect(page.getByTestId('analytics-event-list-overview-card')).toBeVisible()

    // Get the core events list card and check that it is rendered.
    await expect(page.getByTestId('analytics-event-list-core-events-card')).toBeVisible()

    // Get the custom events list card and check that it is rendered.
    await expect(page.getByTestId('analytics-event-list-custom-events-card')).toBeVisible()
  })
})

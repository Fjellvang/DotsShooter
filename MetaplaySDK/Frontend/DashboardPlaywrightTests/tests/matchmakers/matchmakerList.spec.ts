// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe.skip('Matchmaker List', () => {
  test.beforeAll('Skip if async matchmaker feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.asyncMatchmaker, 'Async matchmaker feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to matchmakers list', async ({ page }) => {
    // Navigate to matchmakers list page.
    await page.goto('/matchmakers')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Matchmaker cards render on the matchmakers list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('matchmakers-overview-card')).toBeVisible()

    // Get the async matchmakers list card and check that it renders.
    await expect(page.getByTestId('async-matchmakers-list-card')).toBeVisible()

    // Get the realtime matchmakers list card and check that it renders.
    await expect(page.getByTestId('realtime-matchmakers-list-card')).toBeVisible()
  })
})

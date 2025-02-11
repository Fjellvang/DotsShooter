// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('League List', () => {
  test.beforeAll('Skip if leagues feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.playerLeagues, 'Leagues feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to league list', async ({ page }) => {
    // Navigate to leagues page.
    await page.goto('/leagues')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('League cards render on the league list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('league-list-overview-card')).toBeVisible()

    // Get the all leagues list card and check that it renders.
    await expect(page.getByTestId('league-list-card')).toBeVisible()
  })
})

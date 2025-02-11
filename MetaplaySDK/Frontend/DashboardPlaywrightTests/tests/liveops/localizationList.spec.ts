// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Localization List', () => {
  test.beforeAll('Skip if localization feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.localization, 'Localization feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to localizations list', async ({ page }) => {
    // Navigate to localizations list page.
    await page.goto('/localizations')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview, published and unpublished cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('localization-list-overview-card')).toBeVisible()

    // Get the publish history list card and check that it renders.
    await expect(page.getByTestId('localization-published-list-card')).toBeVisible()

    // Get the unpublished localization list card and check that it renders.
    await expect(page.getByTestId('localization-unpublished-list-card')).toBeVisible()
  })
})

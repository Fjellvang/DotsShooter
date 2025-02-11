// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Game Config List', () => {
  test.beforeEach('Navigate to game configs list', async ({ page }) => {
    // Navigate to game configs list page.
    await page.goto('/gameConfigs')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview, published and unpublished cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('game-config-list-overview-card')).toBeVisible()

    // Get the publish history list card and check that it renders.
    await expect(page.getByTestId('game-config-published-list-card')).toBeVisible()

    // Get the unpublished config list card and check that it renders.
    await expect(page.getByTestId('game-config-unpublished-list-card')).toBeVisible()
  })
})

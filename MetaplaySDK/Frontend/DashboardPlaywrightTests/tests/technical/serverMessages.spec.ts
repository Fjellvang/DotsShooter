// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Runtime options', () => {
  test.beforeEach('Navigate to the message center', async ({ page }) => {
    // Navigate to the message center page.
    await page.goto('/serverErrors')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Checks that cards render', async ({ page }) => {
    await expect(page.getByTestId('game-server-errors-card')).toBeVisible()
    await expect(page.getByTestId('telemetry-messages-card')).toBeVisible()
  })
})

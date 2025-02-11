// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Player Segment List', () => {
  test.beforeEach('Navigate to segments list', async ({ page }) => {
    // Navigate to segments list page.
    await page.goto('/segments')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and all segments cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('player-segment-list-overview-card')).toBeVisible()

    // Get the all segments list card and check that it renders.
    await expect(page.getByTestId('player-segment-list-all-segments-card')).toBeVisible()
  })
})

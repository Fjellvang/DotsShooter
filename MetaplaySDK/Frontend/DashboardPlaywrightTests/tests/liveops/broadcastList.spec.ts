// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Broadcast List', () => {
  test.beforeEach('Navigate to broadcasts list', async ({ page }) => {
    // Navigate to broadcasts list page.
    await page.goto('/broadcasts')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and all broadcasts cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('broadcast-list-overview-card')).toBeVisible()

    // Get the all broadcasts card and check that it renders.
    await expect(page.getByTestId('all-broadcasts-list-card')).toBeVisible()
  })

  test('Create new broadcast button opens modal', async ({ page }) => {
    // Click the create new broadcast button.
    await page.getByTestId('create-new-broadcast-button').click()

    // Check that the modal is visible.
    await expect(page.getByTestId('create-new-broadcast-modal')).toBeVisible()
  })
})

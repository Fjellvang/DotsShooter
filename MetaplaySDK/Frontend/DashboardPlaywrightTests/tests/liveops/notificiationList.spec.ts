// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Notification List', () => {
  test.beforeEach('Navigate to notifications list', async ({ page }) => {
    // Navigate to notifications list page.
    await page.goto('/notifications')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and all notifications cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('notification-list-overview-card')).toBeVisible()

    // Get the all notifications card and check that it renders.
    await expect(page.getByTestId('all-notifications-list-card')).toBeVisible()
  })
})

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Game Config List', () => {
  test.beforeAll('Skip if liveOpsEvents feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.liveOpsEvents, 'LiveOps Events feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to liveOpsEvents list', async ({ page }) => {
    // Navigate to liveOpsEvents list page.
    await page.goto('/liveOpsEvents')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Timeline card renders on list page', async ({ page }) => {
    // Get the timeline card and check that it renders.
    await expect(page.getByTestId('timeline')).toBeVisible()

    // Check the sub-elements too.
    await expect(page.getByTestId('timeline-navigator')).toBeVisible()
    await expect(page.getByTestId('timeline-content')).toBeVisible()
  })

  test('Overview, upcoming and past events cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('live-ops-event-list-overview-card')).toBeVisible()

    // Get the upcoming events card and check that it renders.
    await expect(page.getByTestId('upcoming-live-ops-events-list-card')).toBeVisible()

    // Get the ongoing and past events card and check that it renders.
    await expect(page.getByTestId('past-live-ops-events-list-card')).toBeVisible()
  })

  test('`New LiveOps Event` button opens modal', async ({ page }) => {
    // Click the `New LiveOps Event` button.
    await page.getByTestId('live-ops-event-form-button').click()

    // Check that the modal is visible.
    await expect(page.getByTestId('live-ops-event-form-modal')).toBeVisible()
  })
})

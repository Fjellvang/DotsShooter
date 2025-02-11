// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Experiment List', () => {
  test.beforeEach('Navigate to experiments list', async ({ page }) => {
    // Navigate to experiments list page.
    await page.goto('/experiments')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and all experiments cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('experiment-list-overview-card')).toBeVisible()

    // Get the all experiments card and check that it renders.
    await expect(page.getByTestId('all-experiments-list-card')).toBeVisible()
  })
})

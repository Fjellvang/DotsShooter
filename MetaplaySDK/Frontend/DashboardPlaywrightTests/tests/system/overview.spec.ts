// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Overview', () => {
  test.beforeEach('Navigate to overview', async ({ page }) => {
    // Navigate to the overview page.
    await page.goto('/')
  })

  test('Overview link and header title', async ({ page }) => {
    // Get the overview link (game icon image) and click it to navigate to overview page.
    await page.getByTestId('overview-link').click()

    // Check that the header title is `Overview`.
    await expect(page.getByTestId('header-bar-title')).toHaveText('Overview')
  })

  test('Overview, concurrents, player actors, incidents and Grafana cards render', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('overview-card')).toBeVisible()

    // Get the concurrents card and check that it renders.
    await expect(page.getByTestId('concurrents-card')).toBeVisible()

    // Get the player actors card and check that it renders.
    await expect(page.getByTestId('player-card')).toBeVisible()

    // Get the global incident statistics card and check that it renders.
    await expect(page.getByTestId('global-incident-statistics-card')).toBeVisible()

    // Get the global incident history card and check that it renders.
    await expect(page.getByTestId('global-incident-history-card')).toBeVisible()

    // Get the Grafana card and check that it renders.
    await expect(page.getByTestId('grafana-card')).toBeVisible()
  })
})

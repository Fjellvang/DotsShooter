// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('League and Season Details', () => {
  test.beforeAll('Skip if leagues feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.playerLeagues, 'Leagues feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to first available league', async ({ page }) => {
    // Navigate to leagues page.
    await page.goto('/leagues')

    // Get the first navigation link button in the all leagues list card.
    const navigationLinkButton = page.getByTestId('view-league').first()

    // Check that the navigation link button contains the text `View league`.
    await expect(navigationLinkButton).toHaveText('View league')

    // Click the navigation link button to go a specific league detail page.
    await navigationLinkButton.click()
  })

  test('Overview, season, schedule and audit log cards render on the league detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('league-detail-overview-card')).toBeVisible()

    // Get the seasons list card and check that it renders.
    await expect(page.getByTestId('league-seasons-list-card')).toBeVisible()

    // Get the schedule card and check that it renders.
    await expect(page.getByTestId('league-schedule-card')).toBeVisible()

    // Get the audit log card and check that it renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })

  test.skip('End, preview and start season early', async () => {
    // TODO: finish the test after migrating remaining Cypress ones.
  })

  test('Overview and ranks cards render on the season detail page', async ({ page }) => {
    // Latest season link.
    const latestSeasonLink = page.getByTestId('latest-season-link')

    // Skip the test if there are no seasons. This can happen, eg, if the league has a season in the future that
    // hasn't started yet.
    const hasNoSeasons = (await latestSeasonLink.innerText()) === 'No seasons'
    test.skip(hasNoSeasons, 'No seasons available')

    // Get the latest season link and click it to go a specific season detail page.
    await latestSeasonLink.click()

    // Get the overview card and check that it renders.
    await expect(page.getByTestId('league-season-detail-overview-card')).toBeVisible()

    // Get the all ranks list card and check that it renders.
    await expect(page.getByTestId('league-season-ranks-card')).toBeVisible()
  })
})

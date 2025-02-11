// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Experiment Details', () => {
  test.beforeAll('Skip if there are no experiments', async ({ request, apiURL }) => {
    // Check if experiment list contains at least one experiment.
    const response = await request.get(`${apiURL}/experiments`)
    const data = (await response.json()) as { experiments: unknown[] }
    test.skip(data.experiments.length === 0, 'No experiments available to test in this deployment')
  })

  test.beforeEach('Navigate to first available experiment', async ({ page }) => {
    // Navigate to experiments list page.
    await page.goto('/experiments')

    // Get the first navigation link button in the all experiments list card.
    const navigationLinkButton = page.getByTestId('view-experiment').first()

    // Check that the navigation link button has the text `View experiment`.
    await expect(navigationLinkButton).toHaveText('View experiment')

    // Click the navigation link button to go a specific experiment detail page.
    await navigationLinkButton.click()
  })

  test('Checks overview card renders', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('experiment-detail-overview-card')).toBeVisible()
  })

  test('Tab 0 `Details`', async ({ page }) => {
    // Get the tab 0 button.
    const tab0Button = page.getByTestId('tab-0')

    await expect(tab0Button).toBeVisible()

    // Check that the tab 0 button contains text `Details`.
    await expect(tab0Button).toContainText('Details')

    // Check that the Game Configurations card is rendered.
    await expect(page.getByTestId('game-config-contents-card')).toBeVisible()
  })

  test('Tab 1 `Audience & Targeting`', async ({ page }) => {
    // Get the tab 1 button.
    const tab1Button = page.getByTestId('tab-1')

    // Navigate to the configurations tab.
    await tab1Button.click()

    // Check that the segments card is rendered.
    await expect(page.getByTestId('experiment-detail-segments-card')).toBeVisible()

    // Get the variants card and check that it renders.
    await expect(page.getByTestId('experiment-detail-variants-card')).toBeVisible()

    // Get the test players card and check that it renders.
    await expect(page.getByTestId('experiment-detail-test-players-card')).toBeVisible()
  })

  test('Tab 2 `Audit Log`', async ({ page }) => {
    // Get the tab 2 button.
    const tab2Button = page.getByTestId('tab-2')

    // Navigate to the audit log tab.
    await tab2Button.click()

    // Get the audit log card and check that it renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })

  test(
    'When experiment is missing alert renders, otherwise config contents renders',
    { tag: '@non-deterministic' },
    async ({ page }) => {
      // Navigate to first tab.
      await page.getByTestId('tab-0').click()

      // Get the missing experiment alert.
      const missingExperimentAlert = page.getByTestId('missing-experiment-alert')

      // Get the game configs content card.
      const gameConfigsContentCard = page.getByTestId('game-config-contents-card')

      // Check that either the missing experiment alert or the game configs content card renders.
      // Note: @non-deterministic part below.
      try {
        await Promise.any([
          expect(missingExperimentAlert).toContainText('Experiment removed'),
          expect(gameConfigsContentCard).toBeVisible(),
        ])
      } catch (error) {
        throw new Error('Neither "Experiment removed" alert nor game configs content card rendered:' + String(error))
      }
    }
  )
})

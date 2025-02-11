// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Game Config Details', () => {
  test.beforeAll('Skip if there are no game configs', async ({ request, apiURL }) => {
    // Check if game config lists contains at least one game config.
    const response = await request.get(`${apiURL}/gameConfig?showArchived=true`)
    const data = (await response.json()) as unknown[]
    test.skip(data.length === 0, 'No game configs available to test in this deployment')
  })

  test.beforeEach('Navigate to first available game config', async ({ page }) => {
    // Navigate to game configs list page.
    await page.goto('/gameConfigs')

    // Get the first navigation link button in the all game configs list card.
    const navigationLinkButton = page.getByTestId('view-config').first()

    // Check that the navigation link button has the text `View config`.
    await expect(navigationLinkButton).toHaveText('View config')

    // Click the navigation link button to go a specific game config detail page.
    await navigationLinkButton.click()
  })

  test('Overview card renders on the detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('game-config-detail-overview-card')).toBeVisible()
  })

  test('Tab `Details` that contains game config contents card', async ({ page }) => {
    // Get the tab `Details` button.
    const detailsTabButton = page.getByTestId('tab-0')

    // Check that the tab `Details` button has correct text.
    await expect(detailsTabButton).toHaveText('Details')

    // Check that the game config contents card renders.
    await expect(page.getByTestId('game-config-contents-card')).toBeVisible()

    // Note: old cypress test has a skipped section `Clicks on the first library item in the contents card`.
    // This doesn't seem to actually assert anything, just finds the first `data-testid=library-title-row` and clicks it.
    // TODO: Figure out how to make this useful once we are mocking data.
  })

  test('Tab `Build Log` contains build and validation log cards', async ({ page }) => {
    // Get the tab `Build Log` button.
    const buildLogTabButton = page.getByTestId('tab-1')

    // Check that the tab `Build Log` button has correct text.
    await expect(buildLogTabButton).toHaveText('Build Log')

    // Navigate to tab `Build Log` and check that the page query has updated.
    await buildLogTabButton.click()
    expect(page.url()).toContain('?tab=1')

    // Check that the build log card renders.
    await expect(page.getByTestId('game-config-build-log-card')).toBeVisible()

    // Check that the validation log card renders.
    await expect(page.getByTestId('game-config-validation-log-card')).toBeVisible()
  })

  test('Tab `Audit Log` contains audit log card', async ({ page }) => {
    // Get the tab `Audit Log` button.
    const auditLogTabButton = page.getByTestId('tab-2')

    // Check that the tab `Audit Log` button has correct text.
    await expect(auditLogTabButton).toHaveText('Audit Log')

    // Navigate to tab `Audit Log` and check that the page query has updated.
    await auditLogTabButton.click()
    expect(page.url()).toContain('?tab=2')

    // Check that the audit log card renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })
})

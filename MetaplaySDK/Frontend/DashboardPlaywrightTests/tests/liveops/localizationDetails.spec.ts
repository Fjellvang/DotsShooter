// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Localization Details', () => {
  test.beforeAll('Skip if there are no localizations', async ({ featureFlags, request, apiURL }) => {
    test.skip(!featureFlags.localization, 'Localization feature not enabled in this deployment')

    // Check if localization lists contains at least one localization.
    const response = await request.get(`${apiURL}/localizations?showArchived=true`)
    const data = (await response.json()) as unknown[]
    test.skip(data.length === 0, 'No localizations available to test in this deployment')
  })

  test.beforeEach('Navigate to first available localization', async ({ page }) => {
    // Navigate to localizations list page.
    await page.goto('/localizations')

    // Get the first navigation link button in the all localizations list card.
    const navigationLinkButton = page.getByTestId('view-localization').first()

    // Check that the navigation link button has the text `View localization`.
    await expect(navigationLinkButton).toHaveText('View localization')

    // Click the navigation link button to go a specific localization detail page.
    await navigationLinkButton.click()
  })

  test('Overview card renders on the detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('localization-detail-overview-card')).toBeVisible()
  })

  test('Tab `Details` that contains localization contents card', async ({ page }) => {
    // Get the tab `Details` button.
    const detailsTabButton = page.getByTestId('tab-0')

    // Check that the tab `Details` button has correct text.
    await expect(detailsTabButton).toHaveText('Details')

    // Check that the localization contents card renders.
    await expect(page.getByTestId('localization-contents-card')).toBeVisible()

    // Note: old cypress test has a skipped section `Clicks on the first library item in the contents card`.
    // This doesn't seem to actually assert anything, just finds the first `data-testid=library-title-row` and clicks it.
    // TODO: Figure out how to make this useful once we are mocking data.
  })

  test('Tab `Audit Log` contains audit log card', async ({ page }) => {
    // Get the tab `Audit Log` button.
    const auditLogTabButton = page.getByTestId('tab-1')

    // Check that the tab `Audit Log` button has correct text.
    await expect(auditLogTabButton).toHaveText('Audit Log')

    // Navigate to tab `Audit Log` and check that the page query has updated.
    await auditLogTabButton.click()
    expect(page.url()).toContain('?tab=1')

    // Check that the audit log card renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })
})

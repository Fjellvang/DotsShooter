// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Offer Group and Offer Details', () => {
  test.beforeAll('Skip if there are no offer groups', async ({ request, apiURL }) => {
    // Check if offer groups contains at least one offer group event.
    const response = await request.get(`${apiURL}/offers`)
    const data = (await response.json()) as { offerGroups: Record<string, unknown> }
    test.skip(Object.keys(data.offerGroups).length === 0, 'No offer groups available to test in this deployment')
  })

  test.beforeEach('Navigate to first available offer group', async ({ page }) => {
    // Navigate to offer groups list page.
    await page.goto('/offerGroups')

    // Get the first navigation link button in any of the offers group list cards.
    const navigationLinkButton = page.getByTestId('view-offer-group').first()

    // Check that the navigation link button has the text `View offer group`.
    await expect(navigationLinkButton).toHaveText('View offer group')

    // Click the navigation link button to go a specific offer group detail page.
    await navigationLinkButton.click()
  })

  test('Overview, offers, config and targeting cards render on offer group detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('offer-group-detail-overview-card')).toBeVisible()

    // Get the offers list card and check that it renders.
    await expect(page.getByTestId('offer-group-detail-offers-list-card')).toBeVisible()

    // Get the configuration card and check that it renders.
    await expect(page.getByTestId('offer-group-detail-configuration-card')).toBeVisible()

    // Get the segments card and check that it renders.
    await expect(page.getByTestId('segments-card')).toBeVisible()

    // Get the player conditions card and check that it renders.
    await expect(page.getByTestId('player-conditions-card')).toBeVisible()
  })

  test('Overview, configuration and targeting cards render on offer detail page', async ({ page }) => {
    // Get the first navigation link button in any of the offers list cards.
    const navigationLinkButton = page.getByTestId('view-offer').first()

    // Check that the navigation link button has the text `View offer`.
    await expect(navigationLinkButton).toHaveText('View offer')

    // Click the navigation link button to go a specific offer group detail page.
    await navigationLinkButton.click()

    // Get the overview card and check that it renders.
    await expect(page.getByTestId('offer-detail-overview-card')).toBeVisible()

    // Get the contents card and check that it renders.
    await expect(page.getByTestId('offer-detail-contents-card')).toBeVisible()

    // Get the references card and check that it renders.
    await expect(page.getByTestId('offer-detail-references-card')).toBeVisible()

    // Get the segments card and check that it renders.
    await expect(page.getByTestId('segments-card')).toBeVisible()

    // Get the player conditions card and check that it renders.
    await expect(page.getByTestId('player-conditions-card')).toBeVisible()
  })
})

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Player Segment Details', () => {
  test.beforeAll('Skip if no player segments exist', async ({ request, apiURL }) => {
    // Check if player segment list contains at least one segment with API call.
    const response = await request.get(`${apiURL}/segmentation`)
    const data = (await response.json()) as { segments: unknown[] }
    test.skip(data.segments.length === 0, 'No player segments available to test in this deployment')
  })

  test.beforeEach('Navigate to first available player segment', async ({ page }) => {
    // Navigate to player segments list page.
    await page.goto('/segments')

    // Get the first navigation link button in the all segments list card.
    const navigationLinkButton = page.getByTestId('view-segment').first()

    // Check that the navigation link button has the text `View segment`.
    await expect(navigationLinkButton).toHaveText('View segment')

    // Click the navigation link button to go a specific player segment detail page.
    await navigationLinkButton.click()
  })

  test('Overview and configuration cards render on detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('player-segment-detail-overview-card')).toBeVisible()

    // Get the conditions card and check that it renders.
    await expect(page.getByTestId('player-segment-detail-conditions-card')).toBeVisible()

    // Get the references card and check that it renders.
    await expect(page.getByTestId('player-segment-detail-references-card')).toBeVisible()
  })
})

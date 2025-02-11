// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Offers Group List', () => {
  test.beforeEach('Navigate to offers group list', async ({ page }) => {
    // Navigate to offers group list page.
    await page.goto('/offerGroups')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test(
    'Overview and offers group cards render on list page',
    { tag: '@non-deterministic' },
    async ({ page, request, apiURL }) => {
      // Get the overview card and check that it renders.
      await expect(page.getByTestId('offers-group-overview-card')).toBeVisible()

      // Get the unique placements with API call which the `MetaListCard`s in this list page are based on.
      const offerResponse = await request.get(`${apiURL}/offers`)
      const data = (await offerResponse.json()) as { offerGroups: Record<string, { config: { placement: string } }> }
      const offerGroupsList = Object.values(data.offerGroups as Record<string, { config: { placement: string } }>)
      const allPlacements = offerGroupsList.map((x) => x.config.placement)
      const uniquePlacements = [...new Set(allPlacements)]

      // Check if there is at least one placement.
      if (uniquePlacements.length > 0) {
        // Check if all placements list cards render.
        for (const placementTitle of uniquePlacements) {
          // This could resolve to multiple cards if placement title is similar for multiple offer groups.
          // Hence get the first card and check that it renders.
          // All unique placement titles will still be checked, after looping through the set.
          await expect(page.locator('.card', { hasText: placementTitle }).first()).toBeVisible()
        }
      }
    }
  )

  test('Custom time evaluation tool', async ({ page }) => {
    // Get the time evaluation tool and check that it renders.
    await expect(page.getByTestId('custom-time')).toBeVisible()

    // Note: as of migrating the playwright tests the custom evaluation tool is experiencing some bugs.
    // TODO: Previous cypress test testing that this feature actually works was also left for as todo.
  })
})

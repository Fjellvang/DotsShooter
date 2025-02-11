// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('In-Game Event List', () => {
  test.beforeAll('Skip if in-game events feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.ingameEvents, 'In-game events feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to in-game events list', async ({ page }) => {
    // Navigate to in-game events list page.
    await page.goto('/activables/Event')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and in-game events cards render on list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('event-overview-card')).toBeVisible()

    // Get the in-game event list card and check that it renders.
    await expect(page.getByTestId('event-list-card')).toBeVisible()
  })

  test('Custom time evaluation tool', async ({ page }) => {
    // Get the time evaluation tool and check that it renders.
    await expect(page.getByTestId('custom-time')).toBeVisible()

    // Note: as of migrating the playwright tests the custom evaluation tool is experiencing some bugs.
    // TODO: Previous cypress test testing that this feature actually works was also left for as todo.
  })
})

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Player Model Size Inspector', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Checks text and go to entity page', async ({ page, freshTestPlayer }) => {
    // Get the size inspector text button and go to the `Database Entity` page by clicking it.
    await page.getByTestId('model-size-link').click()

    // Get the database entity overview card.
    const databaseEntityOverviewCard = page.getByTestId('database-entity-overview-card')

    // Check that the database entity overview card is rendered.
    await expect(databaseEntityOverviewCard).toBeVisible()

    // Check that the database entity overview card contains the `sharedTestPlayer`.
    await expect(databaseEntityOverviewCard).toContainText(freshTestPlayer)

    // Check that the database entity data card is rendered.
    await expect(page.getByTestId('database-entity-data-card')).toBeVisible()

    // TODO: Audit logs
  })
})

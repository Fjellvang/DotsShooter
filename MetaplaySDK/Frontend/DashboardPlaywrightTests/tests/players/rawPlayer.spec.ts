// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import type { Page } from '@metaplay/playwright-config'
import { test, expect } from '@metaplay/playwright-config'

test.describe('Raw Player', () => {
  test('Overview and technical details cards render', async ({ page, freshTestPlayer }) => {
    // Navigate to raw player page.
    await page.goto(`/players/${freshTestPlayer}/raw`)

    // Get overview card and expect it to be rendered.
    await expect(page.getByTestId('raw-player-overview-card')).toBeVisible()

    // Get technical details card and expect it to be rendered.
    await expect(page.getByTestId('raw-player-technical-details-card')).toBeVisible()
  })

  test('Check rendering of valid player', async ({ page, freshTestPlayer, rawPlayerDetailEntryTitles }) => {
    // Navigate to raw player page.
    await page.goto(`/players/${freshTestPlayer}/raw`)

    // Make detailEntries where we expect all rows to be valid for `freshTestPlayer`.
    const detailEntries: DetailEntry[] = rawPlayerDetailEntryTitles.map((title: string) => ({
      title,
      isValid: true,
    }))

    await checkDetailEntryRowValidity(page, detailEntries)
  })

  test('Check rendering of missing player', async ({ page, rawPlayerDetailEntryTitles }) => {
    // Navigate to raw player page.
    await page.goto('/players/Player:9999999999/raw')

    // Make detailEntries where we expect the row with title `Player Metadata` to be valid and rest to be invalid.
    // Note: this is hard coded logic coped from previous cypress missing player test.
    // Ideally we would get the validity from the API in addition to the raw player detail entry titles.
    const detailEntries: DetailEntry[] = rawPlayerDetailEntryTitles.map((title: string) => ({
      title,
      isValid: title === 'Player Metadata',
    }))

    await checkDetailEntryRowValidity(page, detailEntries)
  })

  test('Check rendering of invalid player', async ({ page, rawPlayerDetailEntryTitles }) => {
    // Navigate to raw player page.
    await page.goto('/players/Player:invalid/raw')

    // Make detailEntries where we expect all rows to be invalid for invalid player.
    const detailEntries: DetailEntry[] = rawPlayerDetailEntryTitles.map((title: string) => ({
      title,
      isValid: false,
    }))

    await checkDetailEntryRowValidity(page, detailEntries)
  })
})

// Helper Functions ---------------------------------------------------------------------------------------------------

interface DetailEntry {
  title: string
  isValid: boolean
}

/**
 * Checks that the given list of detail entries exists in the technical details card and that each entry is in the expected state of validity.
 * @param page The Playwright Page object.
 * @param detailEntries List of detail entries with title and validity.
 */
async function checkDetailEntryRowValidity(page: Page, detailEntries: DetailEntry[]): Promise<void> {
  // Get the technical details card.
  const technicalDetailsCard = page.getByTestId('raw-player-technical-details-card')

  for (const detailEntry of detailEntries) {
    // Get the detail row heading by searching for the heading in the technical details card list.
    const detailRowHeading = technicalDetailsCard.getByRole('heading', { name: detailEntry.title })

    // Get the detail row by getting the parent of the detail row heading.
    const detailRow = detailRowHeading.locator('..')

    // Check that the detail row is valid or invalid.
    await expect(detailRow).toContainText(detailEntry.isValid ? 'Valid' : 'Not valid')
  }
}

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Reset Player', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Resets the player', async ({ page }) => {
    // Get the reset player button.
    const resetButton = page.getByTestId('action-reset-player-state-button')

    // Check that the button is rendered.
    await expect(resetButton).toBeVisible()

    // Open the reset player modal.
    await resetButton.click()

    // Ok the modal.
    await clickMButton(page.getByTestId('action-reset-player-state-modal-ok-button'))

    // TODO: Check for success toast.
  })
})

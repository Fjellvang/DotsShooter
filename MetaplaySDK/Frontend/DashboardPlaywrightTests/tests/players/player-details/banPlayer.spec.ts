// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Ban Player', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Bans and un-bans a player', { tag: '@dynamic-button-text' }, async ({ page }) => {
    // Get the ban player button. @dynamic-button-text
    const banPlayerButton = page.getByTestId('action-ban-player-button')

    // Check that the button name is `Ban Player`.
    await expect(banPlayerButton).toHaveText('Ban Player')

    // Open modal.
    await banPlayerButton.click()

    // Toggle ban on and press the `Save Settings` button.
    await page.getByTestId('player-ban-toggle-switch-control').click()
    await clickMButton(page.getByTestId('action-ban-player-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Get the player ban alert.
    const playerBannedAlert = page.getByTestId('player-banned-alert')

    // Check that the player ban alert is rendered.
    await expect(playerBannedAlert).toBeVisible()

    // Check that the button name is `Un-Ban Player`.
    await expect(banPlayerButton).toHaveText('Un-Ban Player')

    // Open modal again.
    await banPlayerButton.click()

    // Toggle ban off and press the ok button.
    await page.getByTestId('player-ban-toggle-switch-control').click()
    await clickMButton(page.getByTestId('action-ban-player-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that the player is not banned.
    await expect(playerBannedAlert).not.toBeVisible()

    // Check that the button name is once again `Ban Player`.
    await expect(banPlayerButton).toHaveText('Ban Player')
  })
})

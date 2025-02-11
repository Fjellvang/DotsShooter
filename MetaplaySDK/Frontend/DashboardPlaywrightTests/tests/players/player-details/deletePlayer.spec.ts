// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Delete player', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Schedules player for deletion and cancels the deletion', { tag: '@dynamic-button-text' }, async ({ page }) => {
    // Get the delete player button. @dynamic-button-text
    const deletePlayerButton = page.getByTestId('action-delete-player-button')

    // Check that the button name is `Delete Player`.
    await expect(deletePlayerButton).toHaveText('Delete Player')

    // Open modal.
    await deletePlayerButton.click()

    // Use the default scheduled time for player deletion by pressing the ok button.
    await clickMButton(page.getByTestId('action-delete-player-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that the player is marked for deletion.
    const playerDeleteAlert = page.getByTestId('player-deletion-alert')
    await expect(playerDeleteAlert).toBeVisible()

    // Check that the button name is `Cancel Deletion`.
    await expect(deletePlayerButton).toHaveText('Cancel Deletion')

    // Open modal again.
    await deletePlayerButton.click()

    // Toggle off the DateTime picker to cancel the scheduled deletion and press the ok button.
    await page.getByTestId('scheduled-date-time-enable-toggle-switch-control').click()
    await clickMButton(page.getByTestId('action-delete-player-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that the player is not marked for deletion.
    await expect(playerDeleteAlert).not.toBeVisible()

    // Check that the button name is once again `Delete Player`.
    await expect(deletePlayerButton).toHaveText('Delete Player')

    // TODO: Audit logs
  })
})

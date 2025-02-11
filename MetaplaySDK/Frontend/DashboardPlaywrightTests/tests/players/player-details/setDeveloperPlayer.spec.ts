import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Set Developer Player', () => {
  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Sets a player as developer and verifies it in the developer page', async ({ page, freshTestPlayer }) => {
    // Sets player as developer in the players detail page ------------------------------------------------------------

    // Get mark as developer button.
    const markAsDeveloperButton = page.getByTestId('action-set-developer-button')

    // Check that the button is rendered.
    await expect(markAsDeveloperButton).toBeVisible()

    // Open the set developer modal.
    await markAsDeveloperButton.click()

    // Toggle developer status on and press `Save Settings` button.
    await page.getByTestId('developer-status-toggle-switch-control').click()
    await clickMButton(page.getByTestId('action-set-developer-modal-ok-button'))

    // TODO: Check success toast.

    // Check that the player has been marked as developer through developer icon.
    await expect(page.getByTestId('player-is-developer-icon')).toBeVisible()

    // Navigate to the `Developer Players` page to check if the player is in the list ---------------------------------
    await page.getByTestId('sidebar').locator('li').filter({ hasText: 'Developers' }).click()

    // Get the developer player list card.
    const developerPlayerList = page.getByTestId('developer-players-list')

    // Check that the developer player list card contains title `All Developer Players`.
    await expect(developerPlayerList).toContainText('All Developer Players')

    // Check that the developer player list card contains the player ID.
    await expect(developerPlayerList).toContainText(freshTestPlayer, {
      useInnerText: true,
    })

    // Navigate back to the `freshTestPlayer` page to remove developer status ----------------------------------------
    await page.goBack()

    // Open the set developer modal again.
    await markAsDeveloperButton.click()

    // Toggle developer status off and press `Save Settings` button.
    await page.getByTestId('developer-status-toggle-switch-control').click()
    await clickMButton(page.getByTestId('action-set-developer-modal-ok-button'))

    // TODO: Check success toast.

    // Check that the player developer icon is no longer visible in the overview card.
    // Which means the player is no longer marked as developer.
    await expect(page.getByTestId('player-is-developer-icon')).not.toBeVisible()
  })
})

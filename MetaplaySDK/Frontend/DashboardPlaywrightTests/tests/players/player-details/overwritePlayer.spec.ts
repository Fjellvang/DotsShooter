// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Overwrite Player', () => {
  test.beforeEach('Grant clipboard permissions', async ({ context }) => {
    // Grant clipboard permissions to browser context.
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  })

  test.beforeEach('Navigate to player details', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)
  })

  test('Form validation with input string', async ({ page }) => {
    // Open overwrite modal.
    await page.getByTestId('action-overwrite-player-button').click()

    // Fill in a generic string that will cause the parsing to throw an error.
    await page
      .getByTestId('entity-archive-text')
      .fill('Test string that will cause the parsing to throw an error callout')

    // Check that the error callout contains the title `Calculating payload failed`.
    await expect(page.getByTestId('error-callout')).toContainText('Calculating payload failed', { useInnerText: true })
  })

  test('Form validation with file upload', async () => {
    // TODO: Upload broken file? Make sure its a JSON file that it will parse but something inside broken?
    test.skip()
  })

  test('Overwrites player', async ({ page, testToken }) => {
    // Copy current player data to clipboard and change the player name -----------------------------------------------

    // Open the export player modal.
    await page.getByTestId('export-player-button').click()

    // Check that the export player data has loaded through the ok modal button becoming enabled.
    await expect(page.getByTestId('export-player-modal-ok-button')).toBeEnabled()

    // Copy to clipboard.
    await page.getByTestId('export-player-modal').getByTestId('copy-to-clipboard').click()

    // Clipboard should contain data.
    const clipboardText = await page.evaluate(async () => await navigator.clipboard.readText())
    expect(clipboardText).toContain('entities')

    // Close modal.
    await page.getByTestId('export-player-modal-cancel-button').click()

    // Open the edit name modal.
    await page.getByTestId('action-edit-name-button').click()

    // Change the player name.
    // Note: depending on project specific name validation rules, this current method using testToken might not work.
    const newName = `New${testToken}`
    await page.getByTestId('name-input').fill(newName)
    await clickMButton(page.getByTestId('action-edit-name-modal-ok-button'))

    // Check that name was changed.
    await expect(page.getByTestId('player-overview-card')).toContainText(newName)

    // Restore the original name from clipboard copy by using overwrite -----------------------------------------------

    // Open overwrite modal.
    await page.getByTestId('action-overwrite-player-button').click()

    // Paste original player data from clipboard to the text input field.
    await page.getByTestId('entity-archive-text').fill(clipboardText)

    // Ok the modal.
    await clickMButton(page.getByTestId('action-overwrite-player-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that player was overwritten.
    await expect(page.getByTestId('player-overview-card')).not.toContainText(newName)

    // TODO: Audit logs
  })
})

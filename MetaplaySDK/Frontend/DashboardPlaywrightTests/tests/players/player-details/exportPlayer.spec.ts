// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import fs from 'node:fs'

import { test, expect } from '@metaplay/playwright-config'

test.describe('Export Player', () => {
  test.beforeEach('Grant clipboard permissions', async ({ context }) => {
    // Grant clipboard permissions to browser context.
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
  })

  test.beforeEach('Navigate to player details and open export modal', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)

    // Open the export player modal.
    await page.getByTestId('export-player-button').click()

    // Check that the export player data has loaded through the ok modal button becoming enabled.
    await expect(page.getByTestId('export-player-modal-ok-button')).toBeEnabled()
  })

  test('Export by clipboard copy', async ({ page, freshTestPlayer }) => {
    // Check that the text field has some data.
    await expect(page.getByTestId('export-payload')).toContainText(freshTestPlayer)

    // Copy to clipboard.
    await page.getByTestId('copy-player-to-clipboard').getByTestId('copy-to-clipboard').click()

    // Check copy contents.
    const clipboardText = await page.evaluate(async () => await navigator.clipboard.readText())
    const clipboardContentAsObject = JSON.parse(clipboardText) as { entities: { player: Record<string, object> } }
    expect(Object.keys(clipboardContentAsObject.entities.player)).toContain(freshTestPlayer)
    expect(Object.keys(clipboardContentAsObject.entities.player[freshTestPlayer])).toContain('payload')

    // Closes the modal.
    await page.getByTestId('export-player-modal-cancel-button').click()

    // TODO: Audit logs
  })

  test('Export by downloading file', async ({ page, freshTestPlayer }) => {
    // Note: Download testing is WIP. Remove the `test.skip()` to see what it does locally on your machine.
    test.skip()

    // Download the payload as a file.
    const downloadPromise = page.waitForEvent('download')
    await page.getByTestId('export-player-modal-ok-button').click()
    const download = await downloadPromise

    // TODO figure out proper path/directory for below
    const filePath = `./path/to/temporary/download/directory/${download.suggestedFilename()}`
    await download.saveAs(filePath)

    // Read the file content
    const fileContent = fs.readFileSync(filePath, 'utf8')

    // Optionally, parse the file content if it's JSON
    const fileContentAsObject = JSON.parse(fileContent) as { entities: { player: Record<string, object> } }

    // Assert the file content
    expect(fileContentAsObject).toHaveProperty('entities.player')
    expect(Object.keys(fileContentAsObject.entities.player)).toContain(freshTestPlayer)
    expect(Object.keys(fileContentAsObject.entities.player[freshTestPlayer])).toContain('payload')
  })
})

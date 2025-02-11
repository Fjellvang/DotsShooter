// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import fs from 'node:fs'

import { test, expect } from '@metaplay/playwright-config'

test.describe('GDPR Export', () => {
  test.beforeEach('Navigate to player details and open export modal', async ({ page, freshTestPlayer }) => {
    // Navigate to player details page.
    await page.goto(`/players/${freshTestPlayer}`)

    // Open the GDPR export modal.
    await page.getByTestId('gdpr-export-button').click()

    // Check that the GDPR export data has loaded through the ok modal button becoming enabled.
    await expect(page.getByTestId('gdpr-export-modal-ok-button')).toBeEnabled()
  })

  test('Check for data in modal preview', async ({ page }) => {
    // Check that the preview has some data.
    await expect(page.getByTestId('export-payload')).toContainText('inAppPurchaseHistory')

    // Closes the modal.
    await page.getByTestId('gdpr-export-modal-cancel-button').click()
  })

  test('Export by downloading file', async ({ page, freshTestPlayer }) => {
    // Note: Download testing is WIP. Remove the `test.skip()` to see what it does locally on your machine.
    test.skip()

    // Download the payload as a file.
    const downloadPromise = page.waitForEvent('download')
    await page.getByTestId('gdpr-export-modal-ok-button').click()
    const download = await downloadPromise

    // TODO figure out proper path/directory for below
    const filePath = `./path/to/temporary/download/directory/${download.suggestedFilename()}`
    await download.saveAs(filePath)

    // Read the file content
    const fileContent = fs.readFileSync(filePath, 'utf8')

    // TODO temporary console.log
    console.log(fileContent)

    // Optionally, parse the file content if it's JSON
    const fileContentAsObject = JSON.parse(fileContent) as {
      exportDetails: { playerId: string }
      model: { playerId: string }
    }

    // Assert the file content
    expect(fileContentAsObject).toHaveProperty('exportDetails.playerId')
    expect(fileContentAsObject).toHaveProperty('model.playerId')
    expect(fileContentAsObject).toHaveProperty('model.inAppPurchaseHistory')

    expect(fileContentAsObject.exportDetails.playerId).toContain(freshTestPlayer)
    expect(fileContentAsObject.model.playerId).toContain(freshTestPlayer)

    // TODO: Audit logs
  })
})

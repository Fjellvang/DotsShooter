// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Edit Name', () => {
  test.beforeEach('Navigate to player details and open edit name modal', async ({ page, freshTestPlayer }) => {
    // Navigate to player detail page.
    await page.goto(`/players/${freshTestPlayer}`)

    // Open the edit name modal.
    await page.getByTestId('action-edit-name-button').click()

    // Check that the modal has rendered.
    await expect(page.getByTestId('action-edit-name-modal')).toBeVisible()
  })

  test.beforeEach('Install handler to mock validation data', async ({ page, freshTestPlayer }) => {
    // Mock the validation request so that we can control the result. The result is a simple `true` if the requested
    // name is `validName` and `false` if the requested name is `invalidName`, everything else goes through the normal
    // validation route. This last part covers the case where the edit modal tries to validate the current name.
    await page.route(`/api/players/${freshTestPlayer}/validateName`, async (route) => {
      const postData = (await route.request().postDataJSON()) as { newName: string }
      const requestedName = postData.newName
      const responseData = (await route.request().postDataJSON()) as { nameWasValid: boolean }
      let mockedResponse: boolean = responseData.nameWasValid
      if (requestedName === 'validName') mockedResponse = true
      else if (requestedName === 'invalidName') mockedResponse = false
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({
          nameWasValid: mockedResponse,
        }),
      })
    })
  })

  test('Form validation', async ({ page }) => {
    // Get the new name input text field.
    const nameInput = page.getByTestId('name-input')

    // Legal name, which should have aria-invalid="false".
    await nameInput.fill('validName')
    await expect(nameInput).toHaveAttribute('aria-invalid', 'false')

    // Illegal name, which should have aria-invalid="true".
    await nameInput.fill('invalidName')
    await expect(nameInput).toHaveAttribute('aria-invalid', 'true')

    // Legal name again, which should have aria-invalid="false".
    await nameInput.fill('validName')
    await expect(nameInput).toHaveAttribute('aria-invalid', 'false')
  })

  test('Check for audit log', async ({ page }) => {
    // Create a new name.
    // Note: depending on game specific name validation rules, this might not work.
    const newName = 'validName'

    // Type in the new name in the text input field.
    await page.getByTestId('name-input').fill(newName)

    // Ok the modal.
    await clickMButton(page.getByTestId('action-edit-name-modal-ok-button'))

    // TODO: Check that we got a success toast.

    // Check that the name was changed.
    await expect(page.getByTestId('player-overview-card')).toContainText(newName)

    // Navigate to tab 1.
    await page.getByTestId('tab-1').click()

    // Get the audit log card.
    const auditLogCard = page.getByTestId('audit-log-card')

    // Check that a player log entry was created.
    const event = auditLogCard.getByTestId('event-PlayerChangeNameController-PlayerEventNameChanged')
    await event.scrollIntoViewIfNeeded()
    await expect(event).toContainText('validName')
  })
})

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Scan Jobs', () => {
  test.beforeEach('Navigate to scan jobs list', async ({ page }) => {
    // Navigate to the scan jobs list page.
    await page.goto('/scanJobs')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and list cards render on the scan jobs list page', async ({ page }) => {
    // Get the scan jobs overview card and checks that it is rendered.
    await expect(page.getByTestId('scan-jobs-overview-card')).toBeVisible()

    // Get the active scan jobs list card and checks that it is rendered.
    await expect(page.getByTestId('active-scan-jobs-card')).toBeVisible()

    // Get the past scan jobs by type list card and checks that it is rendered.
    await expect(page.getByTestId('past-scan-jobs-by-type-card')).toBeVisible()

    // Get the past scan jobs list card and checks that it is rendered.
    await expect(page.getByTestId('past-scan-jobs-card')).toBeVisible()
  })

  test('Pauses and resumes all scan jobs', { tag: '@dynamic-button-text' }, async ({ page }) => {
    // Get the pause all scan jobs button.
    const pauseResumeJobsButton = page.getByTestId('pause-all-jobs-button')

    // Check that the pause resume jobs button has the text `Pause Jobs`.
    await expect(pauseResumeJobsButton).toHaveText('Pause Jobs')

    // Open the pause all database scan jobs modal.
    await pauseResumeJobsButton.click()

    // Toggle scan jobs to be paused and press the `Apply` button.
    await page.getByTestId('pause-jobs-toggle-switch-control').click()
    await clickMButton(page.getByTestId('pause-all-jobs-modal-ok-button'))

    // TODO: check for success toast.

    // Check that the pause alert is visible.
    const allJobsPausedAlert = page.getByTestId('all-jobs-paused-alert')
    await expect(allJobsPausedAlert).toBeVisible()

    // Check that the pause resume jobs button has the text `Resume Jobs`.
    await expect(pauseResumeJobsButton).toHaveText('Resume Jobs')

    // Open the pause all database scan jobs modal again.
    await pauseResumeJobsButton.click()

    // Toggle scan jobs to be resumed and press the `Apply` button.
    await page.getByTestId('pause-jobs-toggle-switch-control').click()
    await clickMButton(page.getByTestId('pause-all-jobs-modal-ok-button'))

    // TODO: check for success toast.

    // Check that the pause alert is no longer visible.
    await expect(allJobsPausedAlert).not.toBeVisible()

    // Check that the pause resume jobs button once again has the text `Pause Jobs`.
    await expect(pauseResumeJobsButton).toHaveText('Pause Jobs')
  })

  test('Starts a new player refresher job and cancels it', async ({ page }) => {
    // Open modal to create a new scan job ----------------------------------------------------------------------------

    // Get create job button and click to open the create job modal.
    await page.getByTestId('new-scan-job-button').click()

    // Get the scan job type `MetaInputPlayerSelect` wrapper.
    const scanJobTypeWrapper = page.getByTestId('job-kind-select')

    // Find the input element within the wrapper.
    const scanJobTypeInput = scanJobTypeWrapper.locator('input.multiselect-search')

    // Fill the input element with `Refresher for Players`.
    await scanJobTypeInput.fill('Refresher for Players')

    // Select the `Refresher for Players` option.
    await page.locator('#multiselect-option-Refresh_Player').click()

    // Create the scan job by clicking the `Ok` button.
    await clickMButton(page.getByTestId('new-scan-job-modal-ok-button'))

    // TODO: check for success toast.

    // Find the newly created ongoing scan job and cancel it before it finishes ---------------------------------------

    // Get the active scan jobs list card.
    const activeScanJobsCard = page.getByTestId('active-scan-jobs-card')

    // Check that the active scan jobs list card has rendered.
    await expect(activeScanJobsCard).toBeVisible()

    // Get the first scan jobs entry.
    const firstScanJobEntry = activeScanJobsCard.getByTestId('scan-jobs-entry').first()

    // Check that the first scan job entry contains the text `Refresher for Players` from the newly created job.
    await expect(firstScanJobEntry).toContainText('Refresher for Players')

    // Open the cancel scan job modal.
    await firstScanJobEntry.getByTestId('cancel-scan-job-button').click()

    // Cancel the scan job by clicking the `Ok` button, which triggers an API call. Then we wait for the API response of the scan job cancellation.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().search(/\/api\/.+\/cancel$/) !== -1 && response.status() === 200
      ),
      clickMButton(page.getByTestId('cancel-scan-job-modal-ok-button')),
    ])

    // TODO: check for success toast.

    // Check that the cancelled job is no longer active ---------------------------------------------------------------

    // Wait for the `getAllScanJobsSubscriptionOptions` poll indicate that the cancelled job is no longer active.
    // Note: This is the deterministic way to wait for the response instead of using timeout.
    await page.waitForResponse(async (response) => {
      if (response.url().includes('/api/databaseScanJobs') && response.status() === 200) {
        const responseJson = (await response.json()) as { activeJobs: Array<{ jobTitle: string }> }

        // Check that the active jobs list doesn't contain a job with title "Refresher for Players"
        return !responseJson.activeJobs.some((job: { jobTitle: string }) => job.jobTitle === 'Refresher for Players')
      }
      return false
    })

    // Check that the active scan jobs list card no longer contains the text `Refresher for Players`.
    await expect(activeScanJobsCard).not.toContainText('Refresher for Players')
  })
})

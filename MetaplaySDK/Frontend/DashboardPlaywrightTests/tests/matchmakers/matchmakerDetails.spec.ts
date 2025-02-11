// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('Matchmaker Detail', () => {
  test.beforeAll('Skip if async matchmaker feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.asyncMatchmaker, 'Async matchmaker feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to first available matchmaker', async ({ page }) => {
    // Navigate to matchmakers list page.
    await page.goto('/matchmakers')

    // Get the first navigation link button in the async matchmakers list card.
    const navigationLinkButton = page.getByTestId('view-matchmaker').first()

    // Check that the navigation link button has the text `View matchmaker`.
    await expect(navigationLinkButton).toHaveText('View matchmaker')

    // Click the navigation link button to go a specific async matchmaker detail page.
    await navigationLinkButton.click()
  })

  test('Matchmaker and audit log cards render on the matchmaker detail page', async ({ page }) => {
    // Get overview card and check that it renders.
    await expect(page.getByTestId('matchmaker-overview-card')).toBeVisible()

    // Get the matchmaker bucket chart card and check that it renders.
    await expect(page.getByTestId('matchmaker-bucket-chart')).toBeVisible()

    // Get buckets list card and check that it renders.
    await expect(page.getByTestId('matchmaker-buckets-list-card')).toBeVisible()

    // Get the top players list card and check that it renders.
    await expect(page.getByTestId('matchmaker-top-players-list-card')).toBeVisible()

    // Get the audit log card and check that it renders.
    await expect(page.getByTestId('audit-log-card')).toBeVisible()
  })

  test('Simulate feature', { tag: '@non-deterministic' }, async ({ page }) => {
    // Open the simulation modal by clicking the `Simulate` button.
    await page.getByTestId('simulate-matchmaking-button').click()

    // Type in `9001` as the new MMR into the input.
    await page.getByTestId('attackmmr-input').fill('9001')

    // Simulate matchingmaking by clicking the `Simulate` button, which triggers an API call.
    await page.getByTestId('simulate-matchmaking-ok-button').click()

    // Check for the success of the simulation by checking the number of fulfilled promises.
    // Expect the results column to contain either simulation results list or a message saying `No matches found!`.
    // Only 1 promise should be fulfilled, if not, throw an error.
    // The error represents an edge case scenario where neither or both are rendered.
    // Note: @non-deterministic part below.
    const simulatePlayerCheck = await Promise.allSettled([
      expect(page.getByTestId('simulation-results-list')).toBeVisible(),
      expect(page.getByTestId('simulate-matchmaking-modal')).toContainText('No matches found!', { useInnerText: true }),
    ])
    const fulfilledPromisesCount: number = simulatePlayerCheck.reduce(
      (fulfilledCount, promiseSettledResult) => (promiseSettledResult.status === 'fulfilled' ? 1 : 0) + fulfilledCount,
      0
    )
    if (fulfilledPromisesCount !== 1) {
      throw new Error(`Expected exactly 1 promise to be fulfilled, but got ${fulfilledPromisesCount}`)
    }

    // Close the simulate matchmaking modal.
    await page.getByTestId('simulate-matchmaking-modal-close').click()
  })

  test('Rebalance feature', async ({ page }) => {
    // Potentially move this test to C#, since it cannot be fast enough for default 30 second time out due to relating to scan jobs and sample collection.
    test.skip()

    // Open the rebalancing modal by clicking the `Rebalance` button.
    await page.getByTestId('rebalance-matchmaker-button').click()

    await page.waitForResponse(async (response) => {
      if (response.url().includes('/matchmakers') && response.status() === 200) {
        const responseJson = (await response.json()) as { data: { hasEnoughDataForBucketRebalances: boolean } }
        return responseJson.data.hasEnoughDataForBucketRebalances
      }

      return false
    })

    // Ok the modal.
    await clickMButton(page.getByTestId('rebalance-matchmaker-modal-ok-button'))

    // TODO: Check that we got a success toast if previous clickMButton was successful.

    // Open the reset modal by clicking the `Reset` button.
    await page.getByTestId('reset-matchmaker-button').click()

    // Ok the modal.
    await clickMButton(page.getByTestId('reset-matchmaker-modal-ok-button'))
  })

  test('Reset feature', async ({ page }) => {
    // Open the reset modal by clicking the `Reset` button.
    await page.getByTestId('reset-matchmaker-button').click()

    // Ok the modal.
    await clickMButton(page.getByTestId('reset-matchmaker-modal-ok-button'))

    // TODO: Check that we got a success toast
  })
})

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('Environment', () => {
  test.beforeEach('Navigate to environment', async ({ page }) => {
    // Navigate to the environment page.
    await page.goto('/environment')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Tab 0 `Clusters`', async ({ page }) => {
    // Get the tab 0 button.
    const tab0Button = page.getByTestId('tab-0')

    // Check that the tab 0 button contains text `Clusters`.
    await expect(tab0Button).toContainText('Servers')

    // Get the node set card.
    const nodeSetCard = page.getByTestId('node-sets')

    // Check that node set card contains title `All Nodes`.
    await expect(nodeSetCard).toContainText('All Nodes')

    // Navigate to the first node in the node set.
    await page.getByTestId('node-set-item').first().click()

    const entitiesList = page.getByTestId('node-set-item-entities')

    // Check that the node set entities list is rendered.
    await expect(entitiesList).toBeVisible()

    const nodeDetails = page.getByTestId('node-set-item-details')

    // Check that the node set details are rendered.
    await expect(nodeDetails).toBeVisible()

    // Check that the manage node button is rendered.
    await expect(page.getByTestId('manage-node-button')).toBeVisible()

    // Navigate to the manage node modal.
    await page.getByTestId('manage-node-button').click()

    // Check that the manage node modal is rendered.
    await expect(page.getByTestId('manage-node-modal')).toBeVisible()

    // Check that the grafana logs button is rendered.
    await expect(page.getByTestId('view-grafana-logs-button')).toBeVisible()

    // Close the manage node modal.
    await page.getByTestId('manage-node-modal-close').click()

    // Check that the manage node modal is no longer visible.
    await expect(page.getByTestId('manage-node-modal')).not.toBeVisible()
  })

  test('Tab 1 `Database`', async ({ page, freshTestPlayer }) => {
    // Navigate to tab `Database` and check for cards being rendered --------------------------------------------------

    // Get the tab 1 button.
    const tab1Button = page.getByTestId('tab-1')

    // Check that the tab 1 button contains text `Database`
    await expect(tab1Button).toContainText('Database')

    // Navigate to the `Database` tab.
    await tab1Button.click()

    // Get the database shards list card.
    const databaseShardsListCard = page.getByTestId('database-shards')

    // Check that the database shards list card contains title `Database Shards`.
    await expect(databaseShardsListCard).toContainText('Database Shards')

    // Get the database items list card.
    const databaseItemsListCard = page.getByTestId('database-items')

    // Check that the database items list card contains title `Database Items`.
    await expect(databaseItemsListCard).toContainText('Database Items')

    // Check that the database items list card contains row with `Players` key.
    await expect(databaseItemsListCard).toContainText('Players')

    // TODO: Check if items per shard modal renders? Was not in original cypress test.

    // Use model size inspector modal to go to into freshTestPlayer inspector page -----------------------------------

    // Get the model size inspector modal.
    const modelSizeInspectorModal = page.getByTestId('inspect-entity-button')

    // Open the model size inspector modal.
    await modelSizeInspectorModal.click()

    // Type in the `freshTestPlayer` id.
    // This triggers an API call to verify if the player entity exists.
    await page.getByTestId('entity-id-input').fill(freshTestPlayer)

    // Wait for the response that indicates the player entity exists.
    // Note: This is the deterministic way to wait for the response instead of using timeout.
    await page.waitForResponse(
      (response) => response.url().includes(`/api/entities/${freshTestPlayer}/exists`) && response.status() === 200
    )

    // Click the inspect entity modal ok button.
    await clickMButton(page.getByTestId('inspect-entity-modal-ok-button'))

    // Get the database entity overview card.
    const databaseEntityOverviewCard = page.getByTestId('database-entity-overview-card')

    // Check that the database entity overview card is rendered.
    await expect(databaseEntityOverviewCard).toBeVisible()

    // Check that the database entity overview card contains the `freshTestPlayer`.
    await expect(databaseEntityOverviewCard).toContainText(freshTestPlayer)

    // Check that the database entity data card is rendered.
    await expect(page.getByTestId('database-entity-data-card')).toBeVisible()

    // TODO: Audit logs
  })
})

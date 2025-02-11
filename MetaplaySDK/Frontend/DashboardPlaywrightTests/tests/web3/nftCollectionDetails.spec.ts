// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, clickMButton } from '@metaplay/playwright-config'

test.describe('NFT Collections Detail', () => {
  test.beforeAll('Skip if web3 feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.web3, 'Web3 feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to first available NFT collection', async ({ page }) => {
    // Navigate to the NFT collections page.
    await page.goto('/web3')

    // Get navigation link button in the NFT collections list card.
    const navigationLinkButton = page.getByTestId('view-nft-collection').first()

    // Check that navigation link button has the text `View NFT Collection`.
    await expect(navigationLinkButton).toHaveText('View NFT Collection')

    // Click the navigation link button to go to a specific NFT collections detail page.
    await navigationLinkButton.click()
  })

  test('Overview, NFT and audit log cards render on the NFT collections detail page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('nft-collection-overview-card')).toBeVisible()

    // Get the NFTs list card and check that it renders.
    await expect(page.getByTestId('nft-collection-nft-list')).toBeVisible()

    // Get recent orphan NFTs list card and check that it renders.
    await expect(page.getByTestId('nft-collection-uninitialized-nfts-card')).toBeVisible()

    // Get the audit log card and check that it renders.
    await expect(page.getByTestId('nft-collection-audit-log-card')).toBeVisible()
  })

  test('Refreshes collection metadata', async ({ page }) => {
    // Warning: This is skipped due to server responding with error 500 `Refresh failed. Collection not found in ledger`.
    test.skip()

    // Get refresh metadata button and click it to open `Refresh NFT Collection Metadata` modal.
    await page.getByTestId('refresh-nft-collection-button').click()

    // Ok the modal.
    await clickMButton(page.getByTestId('refresh-nft-collection-modal-ok-button'))
  })

  // Note: old task from previous cypress test.
  // TODO: how to automatically test batch initialization?

  test('Initialize a new NFT and check its cards and features on the detail page', async ({ page }) => {
    // Initialize a new NFT and prepare to navigate to the new NFT detail page ----------------------------------------

    // Get the `Init single NFT` button and click it to open `Initialize a New NFT` modal.
    // Once modal is open wait for the API response of `MetaGeneratedForm` validation.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(async (response) => {
        const metaGeneratedFormValidationEndpoint =
          '/api/forms/schema/Metaplay.Server.AdminApi.Controllers.NftController.NftInitializationParams/validate'
        if (response.url().includes(metaGeneratedFormValidationEndpoint) && response.status() === 200) {
          const validationResults = (await response.json()) as unknown[]
          // This is logic from `MetaGeneratedForm`, where the response returns an empty array when validation passes.
          if (validationResults.length === 0) {
            return true
          }
        }
        return false
      }),
      page.getByTestId('initialize-nft-button').click(),
    ])

    // Initialize NFT by clicking the `Initialize` button, which triggers an API call. Then we wait for the API response of the NFT initialization.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().search(/\/api\/.+\/.+\/initialize$/) !== -1 && response.status() === 200
      ),
      clickMButton(page.getByTestId('initialize-nft-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // Get navigaton link button in the NFTs list card.
    const navigationLinkButton = page.getByTestId('view-nft').first()

    // Check that navigation link button has the text `View NFT`.
    await expect(navigationLinkButton).toHaveText('View NFT')

    // Click the navigation link button to go to a specific NFT detail page.
    await navigationLinkButton.click()

    // Overview, data preview and audit log cards render on the NFT detail page ---------------------------------------

    // Check that overview card renders.
    await expect(page.getByTestId('nft-overview-card')).toBeVisible()

    // Note: old cypress commented out `data-testid=nft-game-state-card` after nft-overview-card and before nft-public-data-preview-card.

    // Check that public data preview card renders.
    await expect(page.getByTestId('nft-public-data-preview-card')).toBeVisible()

    // Check that audit log card renders.
    await expect(page.getByTestId('nft-audit-log-card')).toBeVisible()

    // Test features of the NFT detail page ---------------------------------------------------------------------------

    // Get the `Refresh Ownership` button and click it to open `Refresh External Ownership Status` modal.
    await page.getByTestId('refresh-nft-button').click()

    // Refresh ownership by clicking the `Refresh` button, which triggers an API call. Then we wait for the API response of NFT ledger status update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().search(/\/api\/.+\/.+\/refresh$/) !== -1 && response.status() === 200
      ),
      clickMButton(page.getByTestId('refresh-nft-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // Get the `Re-save Metadata` button and click it to open `Force Re-save NFT's Public Metadata` modal.
    await page.getByTestId('republish-nft-metadata-button').click()

    // Republish the NFT's public metadata by clicking the `Republish` button, which triggers an API call. Then we wait for the API response of NFT metadata update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().search(/\/api\/.+\/.+\/republishMetadata$/) !== -1 && response.status() === 200
      ),
      clickMButton(page.getByTestId('republish-nft-metadata-modal-ok-button')),
    ])

    // TODO: Check for success toast.

    // TODO chain this with Promise.all as well?
    // Get the `Edit` button and click it to open `Edit the NFT` modal.
    await page.getByTestId('edit-nft-button').click()

    // Edit the NFT by clicking the `Edit` button, which triggers an API call. Then we wait for the API response of NFT metadata update.
    // Note: Start listening to the response before the click to avoid server response being missed.
    await Promise.all([
      page.waitForResponse(
        (response) => response.url().search(/\/api\/.+\/.+\/edit$/) !== -1 && response.status() === 200
      ),
      clickMButton(page.getByTestId('edit-nft-modal-ok-button')),
    ])

    // TODO: Check for success toast.
  })
})

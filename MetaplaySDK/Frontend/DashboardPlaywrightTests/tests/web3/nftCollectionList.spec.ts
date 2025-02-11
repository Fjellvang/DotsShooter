// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect, activeLinkExistsForCurrentPage } from '@metaplay/playwright-config'

test.describe('NFT Collections List', () => {
  test.beforeAll('Skip if web3 feature not enabled', async ({ featureFlags }) => {
    test.skip(!featureFlags.web3, 'Web3 feature not enabled in this deployment')
  })

  test.beforeEach('Navigate to NFT collections list', async ({ page }) => {
    // Navigate to the NFT collections page.
    await page.goto('/web3')
  })

  test('Checks that an active sidebar link exists to the current page', async ({ page, baseURL }) => {
    await activeLinkExistsForCurrentPage(page, baseURL)
  })

  test('Overview and list cards render on the NFT collections list page', async ({ page }) => {
    // Get the overview card and check that it renders.
    await expect(page.getByTestId('web3-overview-card')).toBeVisible()

    // Get the NFT collections list card and check that it renders.
    await expect(page.getByTestId('nft-collections-list-card')).toBeVisible()
  })
})

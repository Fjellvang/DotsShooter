// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
import { test, expect } from '@metaplay/playwright-config'

test.describe('Runtime options', () => {
  test.beforeEach('Navigate to the environment page', async ({ page }) => {
    // Navigate to the environment page.
    await page.goto('/environment')
  })

  test('Finds the runtime options tab', async ({ page }) => {
    // Get the runtime options tab.
    const runtimeOptionsTab = page.getByTestId('tab-2')
    await expect(runtimeOptionsTab).toContainText('Runtime Options')

    // Navigate to the runtime options tab and check that the page query has updated.
    await runtimeOptionsTab.click()
    expect(page.url()).toContain('?tab=2')

    // Get the first runtime options collapse header.
    const firstRuntimeOptionsCollapseHeader = page.getByTestId('runtime-option-collapse-header').first()
    // Check that the first runtime options collapse header contains the text `Admin Api`.
    await expect(firstRuntimeOptionsCollapseHeader).toContainText('Admin Api')
  })
})

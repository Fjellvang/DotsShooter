// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Services;
using Microsoft.Playwright;

namespace Metaplay.System.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MaintenanceModeTests : MetaPageTest
{
    // \todo Refactor to be a common pattern for AdminApi endpoints -- put in some shared location
    class EmptyResponse { }

    /// <summary>
    /// Schedule a maintenance mode, then de-schedule it.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task MaintenanceMode()
    {
        // \todo Potential improvements to test case: start maintenance mode immediately,
        //       use bots to check they are kicked out on maintenance start, and
        //       use bots to check that they cannot connect during maintenance

        // Force maintenance mode to be disabled
        await HttpUtil.RequestDeleteAsync<EmptyResponse>(HttpUtil.SharedJsonClient, $"{TestUtil.ServerBaseUrl}/api/maintenanceMode");

        // \todo Set maintenance mode directly via API instead of thru dashboard
        //await HttpUtil.RequestJsonPutAsync<ScheduledMaintenanceMode, EntityAskOk>(HttpUtil.SharedJsonClient, $"{TestUtil.ServerBaseUrl}/api/maintenanceMode", new ScheduledMaintenanceMode(...));

        // Navigate to system page
        await Page.GotoAsync($"{TestUtil.DashboardBaseUrl}/system");

        // Check that the maintenance mode banner isn't present
        await Expect(Page.GetByTestId("maintenance-mode-header-notification")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("maintenance-scheduled-label")).ToHaveCountAsync(0);

        // Open maintenance mode dialog
        await Page.GetByTestId("system-maintenance-mode-card").GetByTestId("maintenance-mode-button").ClickAsync();

        // Check that modal opens
        ILocator modal = Page.GetByTestId("maintenance-mode-modal");
        await Expect(modal).ToBeVisibleAsync();

        // Enable the maintenance mode toggle (must not be checked yet)
        ILocator toggle = modal.GetByTestId("maintenance-enabled-switch-control");
        await Expect(toggle).ToBeCheckedAsync(new LocatorAssertionsToBeCheckedOptions { Checked = false });
        await toggle.ClickAsync();

        // Enable this to force a failure
        //await Expect(toggle).ToBeCheckedAsync(new LocatorAssertionsToBeCheckedOptions { Checked = false });

        // Save settings
        await TestUtil.ClickMetaButtonAsync(Page.GetByTestId("maintenance-mode-modal-ok-button-root"));

        // Check that modal closed
        await Expect(Page.GetByTestId("maintenance-mode-modal")).ToHaveCountAsync(0);

        // Check that the maintenance mode banner & label becomes visible
        await Expect(Page.GetByTestId("maintenance-mode-header-notification")).ToBeVisibleAsync();
        await Expect(Page.GetByTestId("maintenance-scheduled-label")).ToHaveTextAsync("Maintenance scheduled");

        // Open the dialog to disable maintenance mode
        await Page.GetByTestId("maintenance-mode-button").ClickAsync();

        // Disable the checkbox (must be checked)
        await Expect(toggle).ToBeCheckedAsync();
        await toggle.ClickAsync();

        // Save maintenance mode disabled
        await TestUtil.ClickMetaButtonAsync(Page.GetByTestId("maintenance-mode-modal-ok-button-root"));

        // Check that banner no longer visible
        await Expect(Page.GetByTestId("maintenance-mode-header-notification")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("maintenance-scheduled-label")).ToHaveCountAsync(0);
    }
}

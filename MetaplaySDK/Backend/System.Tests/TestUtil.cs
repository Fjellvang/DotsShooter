// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.Services;
using Metaplay.Server.AdminApi.Controllers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace Metaplay.System.Tests;

[SetUpFixture]
public class GlobalTestSetUp
{
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        TestHelper.SetupForTests();
        await TestUtil.Initialize();
    }
}

/// <summary>
/// Shared utilities for the test such as dealing with the MButton safety lock feature.
/// Also fetches the /api/hello endpoint from the server at the start so tests can check
/// the enabled feature flags for instance.
/// </summary>
public static class TestUtil
{
    /// <summary>Base url of the game server, defaults to http://localhost:5550</summary>
    public static string                                ServerBaseUrl       { get; private set; }
    /// <summary>Base url of the admin dashboard, defaults to http://localhost:5551 (server with 'pnpm dev')</summary>
    public static string                                DashboardBaseUrl    { get; private set; }
    /// <summary>Should videos be captured from the browser when running the tests (enable by setting CAPTURE_VIDEO=yes environment variable)</summary>
    public static bool                                  EnableVideoCapture  { get; private set; }
    /// <summary>Directory where test output artifacts (screenshots and videos) are written to (set with OUTPUT_DIRECTORY environment variable)</summary>
    public static string                                OutputDirectory     { get; private set; }
    /// <summary>Game server's response to the /api/hello endpoint, contains for example the feature flags enabled in the project</summary>
    public static SystemStatusController.HelloResponse  HelloResponse       { get; private set; }

    public static async Task Initialize()
    {
        // Configure from environment variables, or default to local run
        ServerBaseUrl       = Environment.GetEnvironmentVariable("SERVER_BASE_URL") ?? "http://localhost:5550";
        DashboardBaseUrl    = Environment.GetEnvironmentVariable("DASHBOARD_BASE_URL") ?? "http://localhost:5551";
        EnableVideoCapture  = IsEnvVariableTruthy("CAPTURE_VIDEO");
        OutputDirectory     = Environment.GetEnvironmentVariable("OUTPUT_DIRECTORY") ?? "PlaywrightOutput";

        // Fetch /api/hello to get server config
        HelloResponse = await HttpUtil.RequestJsonGetAsync<SystemStatusController.HelloResponse>(HttpUtil.SharedJsonClient, $"{ServerBaseUrl}/api/hello");
    }

    static bool IsEnvVariableTruthy(string variableName)
    {
        string value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrEmpty(value)) // missing values are falsy
            return false;
        value = value.ToLowerInvariant();
        if (value == "yes" || value == "true" || value == "1")
            return true;
        if (value == "no" || value == "false" || value == "0")
            return false;
        throw new InvalidOperationException($"Environment variable {variableName} is not truthy or falsy: got '{value}', allowed values are yes/true/1 and no/false/0");
    }

    /// <summary>
    /// Fetch the game server /api/status endpoint response.
    /// </summary>
    /// <returns></returns>
    public static async Task<SystemStatusController.StatusResponse> GetServerStatusAsync()
    {
        return await HttpUtil.RequestJsonGetAsync<SystemStatusController.StatusResponse>(HttpUtil.SharedJsonClient, $"{ServerBaseUrl}/api/status");
    }

    /// <summary>
    /// Click an MButton in the dashboard. If the safety locks are enabled, this automatically disables
    /// the lock and then presses the button. You should pass the element with data-testid ending in
    /// '-button-root' to this method.
    /// </summary>
    /// <param name="buttonRoot"></param>
    /// <returns></returns>
    public static async Task ClickMetaButtonAsync(ILocator buttonRoot)
    {
        // Check that button is visible & enabled and actually the button root
        await Assertions.Expect(buttonRoot).ToBeVisibleAsync();
        await Assertions.Expect(buttonRoot).ToBeEnabledAsync();
        await Assertions.Expect(buttonRoot).ToHaveAttributeAsync("safety-lock-active", new Regex("yes|no"));

        // If safety lock is active, disable it
        bool isSafetyLockActive = await buttonRoot.GetAttributeAsync("safety-lock-active") == "yes";
        if (isSafetyLockActive)
        {
            // Toggle the safety lock
            ILocator lockToggleLocator = buttonRoot.GetByTestId("safety-lock-button");
            await Assertions.Expect(lockToggleLocator).ToBeVisibleAsync();
            await Assertions.Expect(lockToggleLocator).ToBeEnabledAsync();
            await lockToggleLocator.ClickAsync(new LocatorClickOptions { Force = true });
            await Assertions.Expect(buttonRoot).ToHaveAttributeAsync("safety-lock-active", "no");

            // Check that the actual button is now enabled
            ILocator actualButton = buttonRoot.GetByRole(AriaRole.Button);
            await Assertions.Expect(actualButton).ToBeVisibleAsync();
            await Assertions.Expect(actualButton).ToBeEnabledAsync();
        }

        // Click the button itself
        ILocator buttonLocator = buttonRoot.GetByRole(AriaRole.Button);
        await Assertions.Expect(buttonLocator).ToBeVisibleAsync();
        await buttonLocator.ClickAsync();
    }
}

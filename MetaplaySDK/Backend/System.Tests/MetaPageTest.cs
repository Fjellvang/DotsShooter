// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework.Interfaces;

namespace Metaplay.System.Tests;

/// <summary>
/// Replacement for Playwright's <c>PageTest</c> class which automatically takes screenshots of any
/// failed tests. Also captures videos if CAPTURE_VIDEO environment variable is set.
/// </summary>
public class MetaPageTest : ContextTest
{
    public const string InitialBrowserUrl = "about:blank";

    protected IPage Page { get; private set; }

    public override BrowserNewContextOptions ContextOptions()
    {
        BrowserNewContextOptions options = base.ContextOptions();

        // Enable video recording if requested
        if (TestUtil.EnableVideoCapture)
        {
            options.RecordVideoDir = $"{TestUtil.OutputDirectory}/{TestContext.CurrentContext.Test.FullName}/";
            options.RecordVideoSize = new RecordVideoSize { Width = 1024, Height = 768 };
        }

        return options;
    }

    [SetUp]
    public async Task InitializeAsync()
    {
        Page = await Context.NewPageAsync();

        // Check that out InitialBrowserUrl is correct (it's used to skip screenshots in test that didn't navigate anywhere)
        await Expect(Page).ToHaveURLAsync(InitialBrowserUrl);
    }

    [TearDown]
    public async Task TeardownAsync()
    {
        // Take a screenshot if a test failed and browser has navigated from the 'about:blank' page
        if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed && Page.Url != InitialBrowserUrl)
        {
            string testName = TestContext.CurrentContext.Test.FullName;
            string screenshotPath = $"{TestUtil.OutputDirectory}/{testName}.png";
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
        }
    }
}

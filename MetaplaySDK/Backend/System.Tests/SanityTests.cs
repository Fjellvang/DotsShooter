// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Network;
using Microsoft.Playwright.NUnit;
using System.Net;

namespace Metaplay.System.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SanityTests : PageTest
{
    /// <summary>
    /// Check that the server AdminApi responds to requests.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task ServerAdminApiUp()
    {
        MetaHttpResponse response = await MetaHttpClient.DefaultInstance.GetAsync($"{TestUtil.ServerBaseUrl}/api/hello");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Check that we can access the webhook test endpoint.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task ServerWebhookApiIsUp()
    {
        MetaHttpResponse response = await MetaHttpClient.DefaultInstance.GetAsync($"{TestUtil.ServerBaseUrl}/webhook/test");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// Check that the LiveOps Dashboard is served properly.
    /// </summary>
    /// <returns></returns>
    [Test]
    public async Task DashboardIsUp()
    {
        await Page.GotoAsync(TestUtil.DashboardBaseUrl);
        await Expect(Page).ToHaveTitleAsync("LiveOps Dashboard - Overview");
    }
}

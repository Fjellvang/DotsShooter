// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Server.Authentication;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Metaplay.Server
{
    [RuntimeOptions("Steam", isStatic: true, "Configuration options for Steam app distribution.")]
    public class SteamOptions : RuntimeOptionsBase
    {
        [MetaDescription("Enables authentication using Steam.")]
        public bool EnableSteam { get; private set; } = false;

        [MetaDescription("The App Id of your game, this must match the App ID used in the client.")]
        public int AppId { get; private set; } = 0;

        [MetaDescription("The path to the publisher authentication key, this can be a secret manager path.")]
        public string PublisherAuthenticationKeyPath { get; private set; } = string.Empty;

        [Sensitive]
        [ComputedValue]
        public string PublisherAuthenticationKey { get; private set; } = string.Empty;

        public override async Task OnLoadedAsync()
        {
            if (EnableSteam)
            {
                if (string.IsNullOrEmpty(PublisherAuthenticationKeyPath))
                    throw new InvalidOperationException($"Steam:{nameof(PublisherAuthenticationKeyPath)} must be set if {nameof(EnableSteam)} is true");

                string key = await SecretUtil.ResolveSecretAsync(Log, PublisherAuthenticationKeyPath);
                PublisherAuthenticationKey = key;

                if (AppId <= 0)
                    throw new InvalidOperationException($"Steam:{nameof(AppId)} must be set if {nameof(EnableSteam)} is true");

                await ValidateSteamApiAccess();
            }

            await base.OnLoadedAsync();
        }

        async Task ValidateSteamApiAccess()
        {
            QueryString queryString  = new QueryString();
            queryString = queryString.Add("key", PublisherAuthenticationKey);

            using (HttpRequestMessage tokenRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.steampowered.com/ISteamApps/GetPartnerAppListForWebAPIKey/v2/" + queryString))
            {
                Applist appList = null;
                try
                {
                    // The WebApi returns a non 200 http code response if the authentication fails for any reason, e.g. the authentication key is wrong, or the appId is wrong.
                    appList = (await HttpUtil.RequestAsync<AppListResponse>(HttpUtil.SharedJsonClient, tokenRequest)).AppList;
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to query SteamAPI, Steam features are likely unavailable.\n{ex}");
                }

                if (appList != null)
                {
                    if (appList.Apps.App.All(x => x.AppId != AppId))
                        Log.Error(Invariant($"Steam:{PublisherAuthenticationKey} does not have access to '{AppId}', please give the group access to the corresponding app or provide a different Steam:{nameof(PublisherAuthenticationKey)}."));
                }
                else
                {
                    Log.Error($"Steam:{PublisherAuthenticationKey} could not be used to retrieve a list of apps, the Steam:{nameof(PublisherAuthenticationKey)} is potentially invalid or the API is down.");
                }
            }
        }

        class AppListResponse
        {
            public Applist AppList { get; set; }
        }

        class App
        {
            [JsonProperty("appid")]
            public int    AppId    { get; set; }
            [JsonProperty("app_type")]
            public string AppType { get; set; }
            [JsonProperty("app_name")]
            public string AppName { get; set; }
        }

        class Applist
        {
            public Apps Apps { get; set; }
        }

        class Apps
        {
            public List<App> App { get; set; }
        }
    }
}

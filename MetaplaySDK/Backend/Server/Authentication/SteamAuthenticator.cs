// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Core;
using Metaplay.Server.Authentication.Authenticators;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace Metaplay.Server.Authentication
{
    public class SteamAuthenticator : SocialPlatformAuthenticatorBase
    {
        static readonly HttpClient s_httpClient = HttpUtil.CreateJsonHttpClient();

        public class SteamResponseParams
        {
            public string Result          { get; set; }
            public string SteamId         { get; set; }
            public string OwnerSteamID    { get; set; }
            public bool   VacBanned       { get; set; }
            public bool   PublisherBanned { get; set; }
        }
        public class SteamResponseError
        {
            public int    ErrorCode { get; set; }
            public string ErrorDesc { get; set; }
        }

        public class SteamAuthResponse
        {
            public SteamResponseParams Params { get; set; }
            public SteamResponseError  Error  { get; set; }
        }

        public class SteamAuthResponseWrapper
        {
            public SteamAuthResponse Response { get; set; }
        }

        public static async Task<AuthenticatedSocialClaimKeys> AuthenticateAsync(SocialAuthenticationClaimSteam steam)
        {
            SteamOptions storeOpts = RuntimeOptionsRegistry.Instance.GetCurrent<SteamOptions>();
            if (!storeOpts.EnableSteam)
                throw new AuthenticationError($"Steam authentication is disabled in {nameof(SteamOptions)}");

            string stringTicket = BitConverter.ToString(steam.Ticket,0,steam.Ticket.Length).Replace("-", string.Empty);

            QueryString queryString  = new QueryString();
            queryString = queryString.Add("key", storeOpts.PublisherAuthenticationKey);
            queryString = queryString.Add("appid", storeOpts.AppId.ToString(CultureInfo.InvariantCulture));
            queryString = queryString.Add("ticket", stringTicket);
            queryString = queryString.Add("format", "json");

            if (!string.IsNullOrWhiteSpace(MetaplayCore.Options.ProjectName))
                queryString = queryString.Add("identity", MetaplayCore.Options.ProjectName);

            SteamAuthResponse steamAuthResponse;
            using (HttpRequestMessage tokenRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/" + queryString))
            {
                try
                {
                    // The WebApi returns a non 200 http code response if the authentication fails for any reason, e.g. the authentication key is wrong, or the appId is wrong.
                    steamAuthResponse = (await HttpUtil.RequestAsync<SteamAuthResponseWrapper>(s_httpClient, tokenRequest)).Response;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Steam authentication failed with a HTTP exception");
                    throw new AuthenticationTemporarilyUnavailable("Steam sign-in HTTP request failed, likely temporarily unavailable.");
                }
            }

            // The WebApi returns an error object if the request is wrong, i.e. the ticket is incorrect or the identity doesn't match the client's pchIdentity
            if (steamAuthResponse.Error != null)
                throw new AuthenticationError($"Steam authentication failed with error code '{steamAuthResponse.Error.ErrorCode}' and message '{steamAuthResponse.Error.ErrorDesc}'. The ticket sent by the client is likely wrong.");

            if (steamAuthResponse.Params != null)
            {
                // No idea when this happens, haven't been able to repro it.
                if (steamAuthResponse.Params.Result != "OK")
                    throw new AuthenticationError($"Steam auth failed with unknown result '{steamAuthResponse.Params?.Result}");

                if (steamAuthResponse.Params.PublisherBanned || steamAuthResponse.Params.VacBanned)
                    throw new AuthenticationError("Player is banned in Steam, denying login request");

                return AuthenticatedSocialClaimKeys.FromSingleKey(new AuthenticationKey(AuthenticationPlatform.Steam, steamAuthResponse.Params.SteamId));
            }

            throw new AuthenticationError($"Unexpected response from the SteamAPI, response was {JsonConvert.SerializeObject(steamAuthResponse)}");
        }
    }
}

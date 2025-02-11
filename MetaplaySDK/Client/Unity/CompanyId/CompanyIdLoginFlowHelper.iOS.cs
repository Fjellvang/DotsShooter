// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_IOS

using Metaplay.Core.Tasks;
using System;
using System.Threading.Tasks;

namespace Metaplay.Unity.CompanyId
{
    static partial class CompanyIdLoginFlowHelper
    {
        static Task<GameLoginAccessToken> IosLogin(string companyIdBaseUrl, string companyIdEnvironment)
        {
            string loginCallbackUrl = $"metaplaylogincb://ios.{MetaplaySDK.AppleBundleId}/authorize";
            string codeVerifier = UnityOAuth2Util.CreateCodeVerifier();
            string authorizeUrl = GetAuthorizeUrl(companyIdBaseUrl, companyIdEnvironment, codeVerifier, loginCallbackUrl);
            string tokenUrl = GetTokenUrl(companyIdBaseUrl);
            TaskCompletionSource<GameLoginAccessToken> tcs = new TaskCompletionSource<GameLoginAccessToken>();

            try
            {
                MetaplayIosWebAuthenticationSession.BeginAuthenticate(authorizeUrl, "metaplaylogincb", (result) =>
                {
                    if (!string.IsNullOrEmpty(result.ErrorString))
                    {
                        tcs.TrySetException(new CompanyIdLoginFlowException(phase: "HandleCallback", message: $"Authentication session failed: {result.ErrorString}"));
                        return;
                    }

                    if (!result.Url.StartsWith(loginCallbackUrl))
                    {
                        tcs.TrySetException(new CompanyIdLoginFlowException(phase: "HandleCallback", message: $"Authentication session failed, unexpected callback url: {result.Url}"));
                        return;
                    }

                    UnityOAuth2Util.CallbackParams callbackParams = UnityOAuth2Util.ParseCallback(result.Url);
                    string codeMaybe = callbackParams.Code;
                    string errorMaybe = callbackParams.Error;
                    if (errorMaybe != null)
                    {
                        tcs.TrySetException(new CompanyIdLoginFlowException(phase: "HandleCallback", message: $"Authorization server refused to provide code: {errorMaybe}"));
                        return;
                    }
                    CompanyIdLog.Log.Info("Got callback from auth provider. Exchanging code for access token.");

                    _ = MetaTask.Run(async () =>
                    {
                        try
                        {
                            GameLoginAccessToken response = await ExchangeCodeForAccessTokenAsync(tokenUrl, codeMaybe, codeVerifier, loginCallbackUrl);
                            tcs.TrySetResult(response);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(new CompanyIdLoginFlowException(phase: "CodeExchange", message: "Authorization server refused to exchange code for access token", ex));
                        }
                    }, scheduler: MetaTask.BackgroundScheduler);
                });
                return tcs.Task;
            }
            catch (Exception ex)
            {
                return Task.FromException<GameLoginAccessToken>(new CompanyIdLoginFlowException(phase: "Init", message: "Failed to launch WebAuthenticationSession", ex));
            }
        }
    }
}

#endif

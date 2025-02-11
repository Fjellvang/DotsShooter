// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if UNITY_ANDROID

using Metaplay.Core.Tasks;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Metaplay.Unity.CompanyId
{
    static partial class CompanyIdLoginFlowHelper
    {
        class PendingLoginState
        {
            public string CodeVerifier;
            public string TokenUrl;
            public string LoginCallbackUrl;
            public TaskCompletionSource<GameLoginAccessToken> Tcs;

            public PendingLoginState(string codeVerifier, string tokenUrl, string loginCallbackUrl)
            {
                CodeVerifier = codeVerifier;
                TokenUrl = tokenUrl;
                LoginCallbackUrl = loginCallbackUrl;
                Tcs = new TaskCompletionSource<GameLoginAccessToken>();
            }
        }

        static bool _deeplinkHandlerSet;
        static PendingLoginState _pendingLogin;

        static void EnsureDeepLinkHandler()
        {
            if (!_deeplinkHandlerSet)
            {
                Application.deepLinkActivated += HandleDeepLink;
                _deeplinkHandlerSet = true;
            }
        }

        static Task<GameLoginAccessToken> AndroidLogin(string companyIdBaseUrl, string companyIdEnvironment)
        {
            EnsureDeepLinkHandler();

            string loginCallbackUrl = $"metaplaylogincb://android.{Application.identifier}/authorize";
            string codeVerifier = UnityOAuth2Util.CreateCodeVerifier();
            string authorizeUrl = GetAuthorizeUrl(companyIdBaseUrl, companyIdEnvironment, codeVerifier, loginCallbackUrl);
            string tokenUrl = GetTokenUrl(companyIdBaseUrl);

            _pendingLogin = new PendingLoginState(codeVerifier, tokenUrl, loginCallbackUrl);

            try
            {
                #if UNITY_EDITOR
                throw new CompanyIdLoginFlowException(phase: "Init", message: "In Unity Editor, CompanyIdLoginFlowHelper.AndroidLogin() is not supported. Should use EditorLogin() instead.");
                #elif !METAPLAY_HAS_PLUGIN_ANDROID_CUSTOMTABS
                throw new CompanyIdLoginFlowException(phase: "Init", message: "Missing io.metaplay.unitysdk.androidcustomtabs package.");
                #else
                Metaplay.Unity.Android.MetaplayAndroidCustomTabs.LaunchCustomTabs(authorizeUrl);
                return _pendingLogin.Tcs.Task;
                #endif
            }
            catch (Exception ex)
            {
                return Task.FromException<GameLoginAccessToken>(new CompanyIdLoginFlowException(phase: "Init", message: "Failed to launch custom tabs", ex));
            }
        }

        static void HandleDeepLink(string url)
        {
            string cbUrlPrefix = $"metaplaylogincb://android.{Application.identifier}/authorize?";
            if (!url.StartsWith(cbUrlPrefix))
                return;

            if (_pendingLogin == null)
            {
                CompanyIdLog.Log.Warning("Got deeplink but there is no ongoing login.");
                return;
            }

            // Consume login attempt (this gets captured into the background operation)
            PendingLoginState loginState = _pendingLogin;
            _pendingLogin = null;

            UnityOAuth2Util.CallbackParams callbackParams = UnityOAuth2Util.ParseCallback(url);
            string codeMaybe = callbackParams.Code;
            string errorMaybe = callbackParams.Error;
            if (errorMaybe != null)
            {
                loginState.Tcs.TrySetException(new CompanyIdLoginFlowException(phase: "HandleCallback", message: $"Authorization server refused to provide code: {errorMaybe}"));
                return;
            }

            CompanyIdLog.Log.Info("Got callback from auth provider. Exchanging code for access token.");

            _ = MetaTask.Run(async () =>
            {
                try
                {
                    GameLoginAccessToken response = await ExchangeCodeForAccessTokenAsync(loginState.TokenUrl, codeMaybe, loginState.CodeVerifier, loginState.LoginCallbackUrl);
                    loginState.Tcs.TrySetResult(response);
                }
                catch (Exception ex)
                {
                    loginState.Tcs.TrySetException(new CompanyIdLoginFlowException(phase: "CodeExchange", message: "Authorization server refused to exchange code for access token", ex));
                }
            }, scheduler: MetaTask.BackgroundScheduler);
        }
    }
}

#endif

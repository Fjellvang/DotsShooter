// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Metaplay.Unity
{
    /// <summary>
    /// Helper for common OAuth2 operations.
    /// </summary>
    public static class UnityOAuth2Util
    {
        /// <summary>
        /// Commonly used port for OAuth2 (localhost) callback.
        /// </summary>
        public const int LocalhostCallbackPort = 42543;

        public static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data)
                .Replace("=", "")
                .Replace('+', '-')
                .Replace('/', '_');
        }

        public static string CreateCodeVerifier()
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] buf = new byte[32];
                rng.GetBytes(buf);
                return Base64UrlEncode(buf);
            }
        }

        public static byte[] CreateCodeChallengeS256(string codeVerifier)
        {
            return GetSHA256Hash(Encoding.ASCII.GetBytes(codeVerifier));
        }

        public static byte[] GetSHA256Hash(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(bytes);
            }
        }

        public static string CreateUrlSafeStateParameter()
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] buf = new byte[32];
                rng.GetBytes(buf);
                return Base64UrlEncode(buf);
            }
        }

        /// <summary>
        /// Parses <c>code</c> and <c>state</c> query parameters from a request made to the callback URL.
        /// URL prefix must be validated separately by the caller; this method only looks at the query part.
        /// </summary>
        /// <returns>
        /// Parsed callback parameters and a possible error string, such that:<br/>
        /// <see cref="CallbackParams.Code"/> (query param <c>code</c>) will be non-null, or else <see cref="CallbackParams.Error"/> will be non-null.<br/>
        /// <see cref="CallbackParams.State"/> (query param <c>state</c>) may be null even if <see cref="CallbackParams.Error"/> is null;
        /// caller must check its validity explicitly if desired.
        /// </returns>
        public static CallbackParams ParseCallback(string url)
        {
            // \note: HttpUtility is not supported on older Unity
            Uri parsedUrl = new Uri(url);
            string query = parsedUrl.Query;
            string codeMaybe = null;
            string stateMaybe = null;
            string errorMaybe = null;
            string errorDescriptionMaybe = null;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                string[] keyValues = query.Substring(1).Split('&');
                foreach (string keyValue in keyValues)
                {
                    int sep = keyValue.IndexOf('=');
                    if (sep == -1)
                        continue;
                    string key = keyValue.Substring(0, sep);
                    string value = keyValue.Substring(sep + 1);
                    if (key == "code")
                        codeMaybe = value;
                    else if (key == "state")
                        stateMaybe = value;
                    else if (key == "error")
                        errorMaybe = value;
                    else if (key == "error_description")
                        errorDescriptionMaybe = value;
                }
            }

            if (errorMaybe != null && errorDescriptionMaybe != null)
                return new CallbackParams(code: null, state: null, error: $"{errorMaybe}: {errorDescriptionMaybe}");
            if (errorMaybe != null)
                return new CallbackParams(code: null, state: null, error: errorMaybe);
            if (errorDescriptionMaybe != null)
                return new CallbackParams(code: null, state: null, error: errorDescriptionMaybe);
            if (codeMaybe == null)
                return new CallbackParams(code: null, state: null, error: "missing code argument");
            return new CallbackParams(code: codeMaybe, state: stateMaybe, error: null);
        }

        public struct CallbackParams
        {
            public string Code;
            public string State;
            public string Error;

            public CallbackParams(string code, string state, string error)
            {
                Code = code;
                State = state;
                Error = error;
            }
        }

        public static string CreateHttpBasicAuthHeader(string clientId, string clientSecret)
        {
            // https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-10#name-client-secret
            string clientIdUrlEncoded = UnityWebRequest.EscapeURL(clientId);
            string clientSecretUrlEncoded = UnityWebRequest.EscapeURL(clientSecret);
            string credentials = $"{clientIdUrlEncoded}:{clientSecretUrlEncoded}";
            string credentialsBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
            string basicAuthHeader = $"Basic {credentialsBase64}";
            return basicAuthHeader;
        }

        public static async Task<TAccessToken> ExchangeCodeForAccessTokenAsync<TAccessToken>(string tokenUrl, string formEncodedRequestPayload, string authHeaderMaybe)
        {
            HttpWebRequest tokenRequest = (HttpWebRequest)HttpWebRequest.CreateDefault(new Uri(tokenUrl));
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            using (Stream tokenRequestStream = await tokenRequest.GetRequestStreamAsync())
            {
                byte[] tokenRequestParamsBytes = Encoding.UTF8.GetBytes(formEncodedRequestPayload);
                tokenRequestStream.Write(tokenRequestParamsBytes, 0, tokenRequestParamsBytes.Length);
                tokenRequestStream.Close();
            }

            if (authHeaderMaybe != null)
                tokenRequest.Headers["Authorization"] = authHeaderMaybe;

            using (MemoryStream responseBuffer = new MemoryStream())
            {
                using (HttpWebResponse tokenResponse = (HttpWebResponse)await tokenRequest.GetResponseAsync())
                {
                    if (tokenResponse.StatusCode != HttpStatusCode.OK)
                        throw new InvalidOperationException($"Invalid status code, expected 200, got {tokenResponse.StatusCode}");
                    string mimeType = tokenResponse.ContentType.Split(';')[0];
                    if (mimeType != "application/json")
                        throw new InvalidOperationException($"Invalid content type, expected it to have mime type application/json, but got content type {tokenResponse.ContentType}");
                    await tokenResponse.GetResponseStream().CopyToAsync(responseBuffer);
                }

                TAccessToken result = JsonUtility.FromJson<TAccessToken>(Encoding.UTF8.GetString(responseBuffer.GetBuffer()));
                return result;
            }
        }

        #if UNITY_EDITOR

        /// <summary>
        /// Chooses an available callback url, and opens a listener for it.
        /// </summary>
        public static (HttpListener, Uri) CreateListenerForCallbackIntoEditor(List<Uri> callbackUris)
        {
            // Try to open any login callback url and listen there
            Exception lastError = null;
            foreach (Uri callbackUri in callbackUris)
            {
                HttpListener http = null;
                try
                {
                    http = new HttpListener();
                    http.Prefixes.Add(callbackUri.GetLeftPart(UriPartial.Authority) + "/");
                    http.Start();
                }
                catch (Exception ex)
                {
                    ((IDisposable)http)?.Dispose();
                    lastError = ex;
                    continue;
                }

                return (http, callbackUri);
            }
            throw new InvalidOperationException("Could not open OAuth2 callback listener. Check firewall settings.", lastError);
        }

        #endif
    }
}

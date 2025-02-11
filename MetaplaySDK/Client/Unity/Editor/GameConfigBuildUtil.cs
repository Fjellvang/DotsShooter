// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Json;
using Metaplay.Core.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Metaplay.Unity
{
    // \todo: Rename to BackendAdminApi. This has nothing to do with Builds.
    public static class GameConfigBuildUtil
    {
        /// <summary>
        /// Download the currently active StaticGameConfig and its guid from the target server.
        /// </summary>
        /// <param name="adminApiBaseUrl">AdminApi URL of the backend, for example <c>http://localhost:5550/api/</c>.</param>
        /// <param name="authorizationToken">Access token for the AdminApi. <c>null</c> if no authorization is required. Otherwise, see <see cref="MetaplayOAuth2Client"/> for generating a Access token.</param>
        public static Task<(ConfigArchive, MetaGuid)> DownloadActiveGameConfigAsync(string adminApiBaseUrl, string authorizationToken)
        {
            return DownloadGameConfigAsync("$active", adminApiBaseUrl, authorizationToken);
        }

        /// <summary>
        /// Download the StaticGameConfig and its guid from the target server.
        /// </summary>
        /// <param name="guid">The guid of the game config that you wish to download.</param>
        /// <param name="adminApiBaseUrl">AdminApi URL of the backend, for example <c>http://localhost:5550/api/</c>.</param>
        /// <param name="authorizationToken">Access token for the AdminApi. <c>null</c> if no authorization is required. Otherwise, see <see cref="MetaplayOAuth2Client"/> for generating a Access token.</param>
        public static Task<(ConfigArchive, MetaGuid)> DownloadGameConfigAsync(string guid, string adminApiBaseUrl, string authorizationToken)
        {
            return ExceptionErrorLogger(Task.Run(async () =>
                {
                    await EnsureValidAdminApiBaseUrlAsync(adminApiBaseUrl, authorizationToken);

                    string downloadUri = $"{adminApiBaseUrl}gameConfig/{guid}?binary=true";
                    using (HttpClient httpClient = new HttpClient())
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, downloadUri))
                    {
                        if (authorizationToken != null)
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

                        using (HttpResponseMessage response = await httpClient.SendAsync(request))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                Debug.LogError($"Failed to download active game config from server {adminApiBaseUrl} (responseCode={response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
                                throw new Exception("Failed to download active GameConfig");
                            }

                            // Parse ConfigArchive & Guid from response
                            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                            ConfigArchive configArchive = ConfigArchive.FromBytes(bytes);
                            IEnumerable<string> gameConfigIdHeaders = response.Headers.GetValues("GameConfig-Id");
                            MetaGuid configId = MetaGuid.Parse(gameConfigIdHeaders.First());
                            Debug.Log($"Successfully downloaded active game config (id={configId})");
                            return (configArchive, configId);
                        }
                    }
                }));
        }

        /// <summary>
        /// Uploads GameConfig archive on the target game backend, and optionally (by default) makes it active.
        /// </summary>
        /// <param name="adminApiBaseUrl">AdminApi URL of the backend, for example <c>http://localhost:5550/api/</c>.</param>
        /// <param name="configArchive">GameConfig archive</param>
        /// <param name="authorizationToken">Access token for the AdminApi. <c>null</c> if no authorization is required. Otherwise, see <see cref="MetaplayOAuth2Client"/> for generating a Access token.</param>
        /// <param name="confirmDialog">if true, shows a Dialog requiring user confirmation for operation</param>
        /// <param name="setAsActive">If true, game config is set as active on backend. If false, game config is uploaded but not set as active. It can then be set as active in the Dashboard.</param>
        /// <param name="parentMustMatchActive">
        /// If true, and uploading a PARTIAL game config archive, the publish will check that the currently active game config is the same game config the partial config is created from, and fail otherwise.
        /// This is useful for preventing unintended modifications if multiple users perform Download-Modify-UploadPartial at the same time.
        /// </param>
        /// <param name="name">Name shown in dashboard</param>
        /// <param name="description">Description shown in dashboard</param>
        /// <returns>true if successful</returns>
        public static Task<bool> PublishGameConfigArchiveToServerAsync(string adminApiBaseUrl, ConfigArchive configArchive, string authorizationToken, bool confirmDialog, bool setAsActive = true, bool parentMustMatchActive = true, string name = null, string description = null)
        {
            return ExceptionErrorLogger(Task.Run(() =>
                {
                    bool isConfigArchive = configArchive.Entries.Any(e => e.Name == "Server.mpa") && configArchive.Entries.Any(e => e.Name == "Shared.mpa") && configArchive.Entries.Any(e => e.Name == "_metadata");
                    if (!isConfigArchive)
                        throw new ArgumentException("The archive is not Static game config archive. It should contain both Server-only and Shared sections in order to be published.");

                    string queryParams = $"setAsActive={(setAsActive ? "true" : "false")}&parentMustMatchActive={(parentMustMatchActive ? "true" : "false")}";
                    if (!string.IsNullOrEmpty(name))
                        queryParams += "&name=" + UnityWebRequest.EscapeURL(name);
                    if (!string.IsNullOrEmpty(description))
                        queryParams += "&description=" + UnityWebRequest.EscapeURL(description);
                    return PublishArchiveToServerAsync(adminApiBaseUrl, ArchiveType.GameConfig, "gameConfig", configArchive, authorizationToken, confirmDialog, queryParams);
                }));
        }

        [Serializable]
        public class GameConfigInfo
        {
            public string Name;
            public bool   IsActive;
            public string Id;
            public string Description;
            public string BuildStartedAt;
            public string PublishedAt;

            public string BestEffortStatus;

            public MetaTime BuildStartedAtTime => MetaTime.Parse(BuildStartedAt);
            public MetaTime PublishedAtTime => !string.IsNullOrWhiteSpace(PublishedAt) ? MetaTime.Parse(PublishedAt) : MetaTime.Epoch;
        }

        public static Task<List<GameConfigInfo>> FetchGameConfigsFromServerAsync(string adminApiBaseUrl, string authorizationToken)
        {
            return ExceptionErrorLogger(Task.Run(
                async () =>
                {
                    await EnsureValidAdminApiBaseUrlAsync(adminApiBaseUrl, authorizationToken);

                    string uri = $"{adminApiBaseUrl}gameConfig";

                    using (HttpClient httpClient = new HttpClient())
                    using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri))
                    {
                        if (authorizationToken != null)
                            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

                        using (HttpResponseMessage response = await httpClient.SendAsync(request))
                        {
                            string content = await response.Content.ReadAsStringAsync();
                            if (!response.IsSuccessStatusCode)
                            {
                                Debug.LogError($"Failed to fetch gameconfigs from server (responseCode={response.StatusCode}): {content}");
                                _ = await MetaTask.Run(() => EditorUtility.DisplayDialog("Error", $"Failed to fetch gameconfigs from server: {response.ReasonPhrase} - Check console for details", "Ok"), MetaTask.UnityMainScheduler);
                                return null;
                            }

                            JsonSerialization.Options defaultOptions = JsonSerialization.Options.DefaultOptions;
                            defaultOptions.TypeNameHandling = TypeNameHandling.None;
                            JsonSerializer       jsonSerializer = JsonSerialization.CreateSerializer(defaultOptions);

                            using (StringReader textReader = new StringReader(content))
                            using (JsonTextReader jsonReader = new JsonTextReader(textReader))
                                return jsonSerializer.Deserialize<List<GameConfigInfo>>(jsonReader)?.Where(x=>x.BestEffortStatus == "Success").OrderByDescending(x => x.BuildStartedAtTime).ToList();
                        }
                    }
                }));
        }

        /// <summary>
        /// Uploads Localization archive on the target game backend, and makes it active.
        /// </summary>
        /// <param name="adminApiBaseUrl">AdminApi URL of the backend, for example <c>http://localhost:5550/api/</c>.</param>
        /// <param name="configArchive">Localization archive</param>
        /// <param name="authorizationToken">Access token for the AdminApi. <c>null</c> if no authorization is required. Otherwise, see <see cref="MetaplayOAuth2Client"/> for generating a Access token.</param>
        /// <param name="confirmDialog">if true, shows a Dialog requiring user confirmation for operation</param>
        /// <returns>true if successful</returns>
        /// <param name="name">Name shown in dashboard</param>
        /// <param name="description">Description shown in dashboard</param>
        public static Task<bool> PublishLocalizationArchiveToServerAsync(string adminApiBaseUrl, ConfigArchive configArchive, string authorizationToken, bool confirmDialog, string name = null, string description = null)
        {
            return ExceptionErrorLogger(Task.Run(() =>
                {
                    string queryParams = null;
                    if (!string.IsNullOrEmpty(name))
                        queryParams = (string.IsNullOrEmpty(queryParams) ? string.Empty : queryParams + "&") + "name=" + UnityWebRequest.EscapeURL(name);
                    if (!string.IsNullOrEmpty(description))
                        queryParams = (string.IsNullOrEmpty(queryParams) ? string.Empty : queryParams + "&") + "description=" + UnityWebRequest.EscapeURL(description);
                    return PublishArchiveToServerAsync(adminApiBaseUrl, ArchiveType.Localization, "localizations", configArchive, authorizationToken, confirmDialog, queryParams);
                }));
        }

        enum ArchiveType
        {
            GameConfig,
            Localization
        }

        static async Task<bool> PublishArchiveToServerAsync(string adminApiBaseUrl, ArchiveType archiveType, string archiveName, ConfigArchive configArchive, string authorizationToken, bool confirmDialog, string queryParams)
        {
            await EnsureValidAdminApiBaseUrlAsync(adminApiBaseUrl, authorizationToken);

            // Resolve publish uri
            string publishUri = $"{adminApiBaseUrl}{archiveName}";
            if (queryParams != null)
                publishUri += "?" + queryParams;

            // Ask for confirmation
            if (confirmDialog)
            {
                bool confirm = await MetaTask.Run(() => EditorUtility.DisplayDialog($"Publish {archiveType}", $"Are you sure you want to publish {archiveType} to ({publishUri})", "Continue", "Cancel"), MetaTask.UnityMainScheduler);
                if (!confirm)
                    return false;
            }

            // Send ConfigArchive as POST request to publish endpoint
            byte[] archiveBytes = ConfigArchiveBuildUtility.ToBytes(configArchive);
            Debug.Log($"Publishing {archiveType} version {configArchive.Version} to {publishUri} ({archiveBytes.Length} bytes, generated at {configArchive.CreatedAt} UTC)");

            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, publishUri))
            {
                request.Content = new ByteArrayContent(archiveBytes);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                if (authorizationToken != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

                using (HttpResponseMessage response = await httpClient.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.LogError($"Failed to upload {archiveType} to server (responseCode={response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
                        _ = await MetaTask.Run(() => EditorUtility.DisplayDialog("Error", $"Failed to upload {archiveType} to server: {response.ReasonPhrase} - Check console for details", "Ok"), MetaTask.UnityMainScheduler);
                        return false;
                    }

                    Debug.Log($"Successfully uploaded {archiveType} to server");
                    return true;
                }
            }
        }

        public static async Task EnsureValidAdminApiBaseUrlAsync(string adminApiBaseUrl, string authorizationToken)
        {
            if (string.IsNullOrEmpty(adminApiBaseUrl))
                throw new ArgumentException($"Must provide a valid adminApiBaseUrl", nameof(adminApiBaseUrl));
            if (!adminApiBaseUrl.EndsWith("/"))
                throw new ArgumentException($"AdminApiBaseUrl must end with a trailing /");

            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{adminApiBaseUrl}hello"))
            {
                if (authorizationToken != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorizationToken);

                try
                {
                    using (HttpResponseMessage response = await httpClient.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();

                        // Server said 200 OK, but that does not guarantee anything. Let's check some few fields
                        // to make sure it's not completely wrong.
                        string hello = await response.Content.ReadAsStringAsync();
                        if (hello.StartsWith("{") && hello.EndsWith("}") && hello.Contains("\"projectName\":") && hello.Contains("\"commitId\":"))
                            return;
                        throw new Exception($"Hello is not valid. Expected server hello, got: {hello}");
                    }
                }
                catch (Exception ex)
                {
                    if (authorizationToken == null)
                        throw new ArgumentException($"AdminApiBaseUrl is not valid, or authentication may be needed. Target server failed to reply to /hello.", ex);
                    else
                        throw new ArgumentException($"AdminApiBaseUrl is not valid, or authentication may be invalid. Target server failed to reply to /hello.", ex);
                }
            }
        }

        static Task<TResult> ExceptionErrorLogger<TResult>(Task<TResult> task)
        {
            // Hook continuation, but return the original task
            Action<Task<TResult>> writeError = (task) =>
            {
                Debug.LogError(task.Exception.InnerException);
            };
            _ = task.ContinueWith(writeError, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return task;
        }
    }
}

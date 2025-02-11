// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace Metaplay.Unity
{
    /// <summary>
    /// Utility to run backend server locally. This utility tolerates Domain Reloads, making
    /// it useful in Editor.
    /// </summary>
    public static class LocalServer
    {
        const string PidConfigName = "Metaplay.LocalServer.PID";
        const bool ShowServerTerminalWindow = true;

        static HttpClient _http = new HttpClient();

        /// <summary>
        /// Returns true if there is a local server running.
        /// </summary>
        public static bool IsRunning()
        {
            using (Process p = InternalTryGetProcess())
            {
                return p != null;
            }
        }

        /// <summary>
        /// Starts the local server by running <c>dotnet run</c> in the server folder,
        /// and blocks until server is running. If a server is already running, does nothing.
        /// Throws if server cannot be started.
        /// </summary>
        public static void Start()
        {
            if (IsRunning())
            {
                UnityEngine.Debug.Log("Attempted to start Local Server but server was already running. Ignored.");
                return;
            }

            #if UNITY_EDITOR_OSX
            string dotnetPath = EditorPrefs.GetString("DotnetPath");
            if (string.IsNullOrEmpty(dotnetPath))
            {
                dotnetPath = EditorUtility.OpenFilePanel("Select dotnet executable", "/usr/local/share/dotnet", "");
                if (!string.IsNullOrEmpty(dotnetPath))
                {
                    EditorPrefs.SetString("DotnetPath", dotnetPath);
                }
                else
                {
                    UnityEngine.Debug.LogError("Failed to start Local Server: dotnet executable not found.");
                    return;
                }
            }
            #endif

            UnityEngine.Debug.Log("Attempting to launch Local Server.");

            string serverPath = Path.Combine(Directory.GetCurrentDirectory(), "Backend", "Server");
            try
            {
                using (Process p = new Process())
                {
                    #if UNITY_EDITOR_OSX
                    ProcessStartInfo startInfo = new ProcessStartInfo(dotnetPath);
                    #else
                    ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
                    #endif
                    startInfo.Arguments = "run --Environment:EnableSystemHttpServer=true --Environment:SystemHttpPort=8888 --Environment:EnableKeyboardInput=false";
                    startInfo.CreateNoWindow = !ShowServerTerminalWindow;
                    startInfo.UseShellExecute = ShowServerTerminalWindow;
                    startInfo.WorkingDirectory = serverPath;

                    p.StartInfo = startInfo;
                    if (!p.Start())
                        throw new InvalidOperationException("Failed to launch server");

                    // Wait for server to become ready
                    try
                    {
                        UnityEngine.Debug.Log("Waiting for Local Server start up sequence to complete.");
                        WaitForServerToBecomeReady(p).Wait();
                    }
                    catch
                    {
                        // \todo: Make server write logs to /tmp/ and print the error message here.

                        if (!p.HasExited)
                            p.Kill();
                        throw;
                    }

                    UnityEngine.Debug.Log($"Local Server started with pid {p.Id}.");
                    EditorPrefs.SetInt(PidConfigName, p.Id);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to start Local Server: {ex}");
            }

        }

        /// <summary>
        /// Stops the local server, if any. Blocks until server has been shut down.
        /// </summary>
        public static void Stop()
        {
            using (Process p = InternalTryGetProcess())
            {
                if (p == null)
                {
                    UnityEngine.Debug.Log("Tried to stop Local Server that wasn't running. Ignored.");
                    return;
                }

                bool didShutDownGracefully = false;
                try
                {
                    UnityEngine.Debug.Log("Shutting Local Server down gracefully...");
                    TryStartServerShutdown().Wait();
                    didShutDownGracefully = p.WaitForExit(milliseconds: 5000);
                }
                catch
                {
                }

                if (!didShutDownGracefully && !p.HasExited)
                {
                    UnityEngine.Debug.LogError($"Failed to stop server. Server did not shut down by timeout. Killing forcefully");
                    p.Kill();
                }

                EditorPrefs.SetInt(PidConfigName, -1);
                UnityEngine.Debug.Log("Local Server stopped");
            }
        }

        [UnityEditor.InitializeOnLoadMethod]
        static void Reload()
        {
            using (Process p = InternalTryGetProcess())
            {
            }

            // Stop server if editor is shut down
            EditorApplication.quitting += () =>
            {
                if (IsRunning())
                {
                    Stop();
                }
            };
        }

        static Process InternalTryGetProcess()
        {
            int pid = EditorPrefs.GetInt(PidConfigName, -1);
            if (pid == -1)
                return null;

            try
            {
                return Process.GetProcessById(pid);
            }
            catch
            {
                EditorPrefs.SetInt(PidConfigName, -1);
                return null;
            }
        }

        static async Task WaitForServerToBecomeReady(Process process)
        {
            // Wait 120 seconds for Ready probe to complete
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: 120_000);
            for (;;)
            {
                try
                {
                    // \note: using WithCancelAsync to force immediate completion instead of waiting for a cooperative cancel
                    using (HttpResponseMessage response = await _http.GetAsync("http://127.0.0.1:8888/isReady", cts.Token).WithCancelAsync(cts.Token).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                            return;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new Exception("Timeout while waiting for server to become ready");
                }
                catch
                {
                    // Crash on start?
                    if (process.HasExited)
                        throw new Exception("Server process exited while waiting for it to become ready");

                    // Transient error. Throttle a bit to avoid adverse effects from error spam.
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        static async Task TryStartServerShutdown()
        {
            // Wait at most 5 seconds trying to inform the server.
            CancellationTokenSource cts = new CancellationTokenSource(millisecondsDelay: 5_000);
            try
            {
                // \note: using WithCancelAsync to force immediate completion instead of waiting for a cooperative cancel
                using (HttpResponseMessage response = await _http.PostAsync("http://127.0.0.1:8888/gracefulShutdown", new StringContent(""), cts.Token).WithCancelAsync(cts.Token).ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                        return;
                }
            }
            catch (OperationCanceledException)
            {
                throw new Exception("Timeout while waiting for server to complete /gracefulShutdown");
            }
        }

        /// <summary>
        /// Provides the Unity Menu for Metaplay -> LocalServer.
        /// </summary>
        public class MenuItems
        {
            [MenuItem("Metaplay/Local Server/Start", priority = 200)]
            public static void StartServer()
            {
                if (IsRunning())
                {
                    if (!EditorUtility.DisplayDialog("Local Server", "Local Server is already running. Restart it?", ok: "Restart", cancel: "Cancel"))
                    {
                        UnityEngine.Debug.Log("Local Server restart cancelled");
                        return;
                    }

                    EditorUtility.DisplayProgressBar("Local Server", "Stopping Local Server", 0.1f);
                    Stop();
                }

                EditorUtility.DisplayProgressBar("Local Server", "Starting Local Server", 0.2f);
                try
                {
                    Start();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            [MenuItem("Metaplay/Local Server/Stop", priority = 201)]
            public static void StopServer()
            {
                if (!IsRunning())
                {
                    EditorUtility.DisplayDialog("Local Server", "Local Server is not running", ok: "Ok");
                    return;
                }

                EditorUtility.DisplayProgressBar("Local Server", "Stopping Local Server", 0.2f);
                try
                {
                    Stop();
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            [MenuItem("Metaplay/Local Server/Delete Local Database", isValidateFunction: false, priority = 202)]
            public static void DeleteLocalDatabase()
            {
                if (!EditorUtility.DisplayDialog("Local Server", "Delete the database of the local server. This resets all server and player states on the local server.", ok: "Delete", cancel: "Cancel"))
                    return;

                string path = "Backend/Server/bin";
                string[] databaseFiles = Directory.GetFiles(path, "*.db");

                if (databaseFiles.Length == 0)
                    UnityEngine.Debug.Log($"No *.db files in '{path}'");

                foreach (string file in databaseFiles)
                {
                    UnityEngine.Debug.Log($"Deleting '{file}'");
                    File.Delete(file);
                }
            }
        }
    }
}

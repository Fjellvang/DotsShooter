// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

#if !UNITY_WEBGL || UNITY_EDITOR
#   define HAS_FILESYSTEM
#endif

// In pure .net, we dont want to expose SyncIO
#if !NETCOREAPP
#   define HAS_SYNCHRONOUS_IO
#endif

#if HAS_FILESYSTEM
#pragma warning disable MP_WGL_00 // "Feature is poorly supported in WebGL". False positive, this is non-WebGL.

using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Metaplay.Core.Impl
{
    public class FileUtilImplFileSystem
    {
        public static async Task<bool> WriteAllBytesAtomicAsync(string filePath, byte[] bytes)
        {
            try
            {
                string tempPath = filePath + ".new";
                using (FileAccessLock fsLock = await FileAccessLock.AcquireAsync(filePath).ConfigureAwait(false))
                {
                    // Write to a temp file, and then Move over to the destination, overriding the previous destination file if it exists.
                    // We use lock to prevent others clobbering the temp file.
                    //
                    // In Unity, use File.Replace() as there is no File.Move() overload that takes the overwrite argument. Mono's implementation
                    // uses POSIX rename(): https://github.com/mono/mono/blob/89f1d3cc22fd3b0848ecedbd6215b0bdfeea9477/mono/metadata/w32file-unix.c#L470
                    //
                    // On Windows and POSIX, use File.Move() which uses the Win32 MoveFile(). This should be atomic as Microsoft uses it themselves in
                    // their C++ STL implementation but Microsoft does not give strict guarantees on atomicity.
                    //
                    // Since we have a lock, we don't need to handle races.

                    await WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(false);

                    if (File.Exists(filePath))
                    {
                        try
                        {
                            Stopwatch sw = Stopwatch.StartNew();
#if UNITY_2018_1_OR_NEWER
                            // In Unity, there is no File.Move() overload with overwrite argument so we use File.Replace():
                            File.Replace(sourceFileName: tempPath, destinationFileName: filePath, destinationBackupFileName: null);
#else
                            // Outside Unity, we're using File.Move() as File.Replace() can sometimes takes multiple seconds to execute on Windows. This only
                            // seems to happen if running the game server with multiple processes and if Windows Defender real-time scanning is enabled.
                            // File.Move() always seems to be fast. We're still tracking performance just in case there are further performance drops.
                            File.Move(sourceFileName: tempPath, destFileName: filePath, overwrite: true);
#endif
                            long elapsedMs = sw.ElapsedMilliseconds;
                            if (elapsedMs >= 2000)
                                DebugLog.Warning("Replacing file {DstPath} with {TempPath} took {Elapsed}ms", filePath, tempPath, elapsedMs);
                        }
                        catch
                        {
                            // On Windows, there are stray locks every now and then. Possibly caused by Windows Defender?
                            // Fall back to two-step operation which is no longer atomic.
                            // To avoid non-atomicy issues, we only do this on Development platforms, i.e. (Server on Windows) and (Unity Editor on Windows)
#if NETCOREAPP || UNITY_EDITOR
                            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                            {
                                File.Delete(filePath); // \note no-op if file doesn't exist
                                File.Move(sourceFileName: tempPath, destFileName: filePath);
                                UpdateFileCreatedTimestamp(filePath);
                                return true;
                            }
#endif
                            throw;
                        }
                    }
                    else
                    {
                        File.Move(sourceFileName: tempPath, destFileName: filePath);
                        UpdateFileCreatedTimestamp(filePath);
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static async Task WriteAllBytesAsync(string filePath, byte[] bytes)
        {
            await File.WriteAllBytesAsync(filePath, bytes);
            UpdateFileCreatedTimestamp(filePath);
        }

        public static async Task WriteAllTextAsync(string filePath, string text)
        {
            await File.WriteAllTextAsync(filePath, text);
            UpdateFileCreatedTimestamp(filePath);
        }

        public static Task DeleteAsync(string filePath)
        {
            // There is no File.DeleteAsync(). To prevent this from potentially blocking on server,
            // we throttle deletions to at most one at a time.
            // \note: To throttle, we acquire a lock to pseudo path /?/delete-lock. Since the path
            //        contains '?' it's unlikely there would be real file-access to that path.
            return Task.Run(async () =>
            {
                using (FileAccessLock fsLock = await FileAccessLock.AcquireAsync("/?/delete-lock"))
                {
                    File.Delete(filePath);
                }
            });
        }

        public async static Task<string> ReadAllTextAsync(string filePath)
        {
            using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8))
                return await textReader.ReadToEndAsync();
        }

        public async static Task<byte[]> ReadAllBytesAsync(string filePath)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] buffer = new byte[fileStream.Length];
                int numBytesRead = await fileStream.ReadAsync(buffer);
                if (numBytesRead != buffer.Length)
                    throw new IOException($"Unable to read all bytes from file '{filePath}', only read {numBytesRead} out of {buffer.Length} bytes");
                return buffer;
            }
        }

#if HAS_SYNCHRONOUS_IO

        public static void DeleteSync(string filePath) => File.Delete(filePath);

        public static void WriteAllBytesSync(string filePath, byte[] bytes)
        {
            File.WriteAllBytes(filePath, bytes);
            UpdateFileCreatedTimestamp(filePath);
        }

        public static void WriteAllTextSync(string filePath, string contents)
        {
            File.WriteAllText(filePath, contents);
            UpdateFileCreatedTimestamp(filePath);
        }

#endif

        // \note: Legacy filesystem sync IO exposed

        public static string ReadAllTextSync(string filePath)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8))
                return textReader.ReadToEnd();
        }

        public static byte[] ReadAllBytesSync(string filePath)
        {
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader binaryReader = new BinaryReader(fileStream))
                return binaryReader.ReadBytes((int)fileStream.Length);
        }

        /// <summary>
        /// Reset creation dates of the files to make it easier for a developer to see which
        /// files have their contents changed when inspecting directories.
        /// <para>
        /// Even if files were deleted before re-writing to the path, i.e. this is an overwrite and not a new file,
        /// manual resetting is still needed:
        /// "NTFS-formatted drives may cache file meta-info, such as file creation time, for a short period
        /// of time. This process is known as file tunneling."
        /// </para>
        /// <para>
        /// Since this is only relevant for developers inspecting files, we do this on development platforms:
        /// * On Unity, only in UnityEditor
        /// * On dotnet, only on Windows and OSX
        /// </para>
        /// </summary>
        /// <param name="filePath"></param>
        static void UpdateFileCreatedTimestamp(string filePath)
        {
            bool resetCreateTime;
#if UNITY_EDITOR
            resetCreateTime = true;
#elif NETCOREAPP
            resetCreateTime =
                System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                || System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX);
#else
            resetCreateTime = false;
#endif
            if (resetCreateTime)
                File.SetCreationTimeUtc(filePath, System.DateTime.UtcNow);
        }
    }
}

#pragma warning restore MP_WGL_00
#endif // HAS_FILESYSTEM

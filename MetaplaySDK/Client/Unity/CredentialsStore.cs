// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Metaplay.Unity
{
    /// <summary>
    /// Utility class for persisting <see cref="GuestCredentials"/>.
    /// </summary>
    public static partial class CredentialsStore
    {
        const string KeyDeviceId    = "did";
        const string KeyAuthToken   = "at";
        const string KeyPlayerId    = "pid";

        static bool IsDefaultSlot(string slot)
        {
            return slot.Length == 0;
        }

        static string GetCredentialStorePath(string slot)
        {
            string fileName = IsDefaultSlot(slot) ? "MetaplayCredentials.dat" : $"MetaplayCredentials_{slot}.dat";
            return Path.Combine(MetaplaySDK.PersistentDataPath, fileName);
        }

        /// <summary>
        /// Retrieves stored guest credentials if any. If no credentials are available, returns null credentials.
        /// Throws if credential store is in a state it cannot store or update credentials.
        /// </summary>
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task<GuestCredentials> TryGetGuestCredentialsAsync(string slot)
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // Try each login credential provider and choose first result.
            GuestCredentials resolvedCredentials = null;

            // Credentials for non-default slot
            if (!IsDefaultSlot(slot))
            {
                return TryGetFileCredentials(slot);
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            GuestCredentials gmsBlockStoreCreds;
            try
            {
                gmsBlockStoreCreds = await GmsBlockStoreStore.TryGetGuestCredentialsAsync();
            }
            catch (NoGmsBlockStoreAvailableOnDeviceError)
            {
                MetaplaySDK.Logs.Metaplay.Warning("Google Blockstore is not supported and is skipped. Either this device has no Google Services or the application is missing the Blockstore library. EDM4U is recommended for managing android dependencies.");
                gmsBlockStoreCreds = null;
            }
            catch (Exception ex)
            {
                // tolerate
                MetaplaySDK.Logs.Metaplay.Warning("Failed to read credentials from blockstore: {Error}", ex);
                gmsBlockStoreCreds = null;
            }
            if (resolvedCredentials == null)
                resolvedCredentials = gmsBlockStoreCreds;
            #endif

            #if UNITY_IOS && !UNITY_EDITOR
            GuestCredentials keychainCreds;
            try
            {
                keychainCreds = await IosKeychain.TryGetGuestCredentialsAsync();
            }
            catch (Exception ex)
            {
                // tolerate
                MetaplaySDK.Logs.Metaplay.Warning("Failed to read credentials from keychain: {Error}", ex);
                keychainCreds = null;
            }
            if (resolvedCredentials == null)
                resolvedCredentials = keychainCreds;
            #endif

            // Try to use the credentials on disk
            GuestCredentials fileCreds = TryGetFileCredentials();
            if (resolvedCredentials == null)
                resolvedCredentials = fileCreds;

            // Try read from PlayerPrefs (legacy prefs)
            GuestCredentials legacyPrefs = TryGetLegacyCredentials();
            if (resolvedCredentials == null)
                resolvedCredentials = legacyPrefs;

            // After resolving, sync results back to desynced providers.
            // Other failures are tolerable except for failure to write FileCredentials. FileCredentials is the fallback method
            // and it must work.

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (gmsBlockStoreCreds != resolvedCredentials)
            {
                try
                {
                    await GmsBlockStoreStore.StoreGuestCredentialsAsync(resolvedCredentials);
                }
                catch (Exception ex)
                {
                    MetaplaySDK.Logs.Metaplay.Warning("Failed to sync resolved credentials to blockstore: {Error}", ex);
                }
            }
            #endif

            #if UNITY_IOS && !UNITY_EDITOR
            if (keychainCreds != resolvedCredentials)
            {
                try
                {
                    await IosKeychain.StoreGuestCredentialsAsync(resolvedCredentials);
                }
                catch (Exception ex)
                {
                    MetaplaySDK.Logs.Metaplay.Warning("Failed to sync resolved credentials to keychain: {Error}", ex);
                }
            }
            #endif

            if (fileCreds != resolvedCredentials)
            {
                if (!TryStoreFileCredentials(resolvedCredentials))
                {
                    MetaplaySDK.Logs.Metaplay.Warning("Failed to sync resolved credentials to filecredentials");
                    throw new InvalidOperationException("Could not sync resolved credentials to filecredentials");
                }
            }

            if (legacyPrefs != resolvedCredentials)
            {
                StoreLegacyCredentials(resolvedCredentials);
            }

            return resolvedCredentials;
        }

        /// <summary>
        /// Saves or updates the credentials. Throws if credentials cannot be stored or updated.
        /// </summary>
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task StoreGuestCredentialsAsync(GuestCredentials credentials, string slot)
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (IsDefaultSlot(slot))
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    await GmsBlockStoreStore.StoreGuestCredentialsAsync(credentials);
                }
                catch (Exception ex)
                {
                    MetaplaySDK.Logs.Metaplay.Warning("Failed to store credentials to blockstore: {Error}", ex);
                }
                #endif

                #if UNITY_IOS && !UNITY_EDITOR
                try
                {
                    await IosKeychain.StoreGuestCredentialsAsync(credentials);
                }
                catch (Exception ex)
                {
                    MetaplaySDK.Logs.Metaplay.Warning("Failed to store credentials to keychain: {Error}", ex);
                }
                #endif
            }

            bool fileWriteResult = TryStoreFileCredentials(credentials, slot);
            if (IsDefaultSlot(slot))
                StoreLegacyCredentials(credentials);

            if (!fileWriteResult)
            {
                throw new InvalidOperationException("Failed to store credentials to filecredentials");
            }
        }

        /// <summary>
        /// Clears the saved credentials.
        /// </summary>
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task ClearGuestCredentialsAsync()
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                await GmsBlockStoreStore.ClearGuestCredentialsAsync();
            }
            catch (Exception ex)
            {
                MetaplaySDK.Logs.Metaplay.Warning("Failed to clear credentials in blockstore: {Error}", ex);
            }
            #endif

            #if UNITY_IOS && !UNITY_EDITOR
            try
            {
                await IosKeychain.ClearGuestCredentialsAsync();
            }
            catch (Exception ex)
            {
                MetaplaySDK.Logs.Metaplay.Warning("Failed to clear credentials on keychain: {Error}", ex);
            }
            #endif

            try
            {
                ClearFileCredentials();
            }
            catch (Exception ex)
            {
                MetaplaySDK.Logs.Metaplay.Warning("Failed to clear file credentials: {Error}", ex);
            }

            try
            {
                ClearLegacyCredentials();
            }
            catch (Exception ex)
            {
                MetaplaySDK.Logs.Metaplay.Warning("Failed to clear legacy: {Error}", ex);
            }
        }

#if UNITY_EDITOR

        /// <summary>
        /// Deletes stored credential for Editor. This requires no SDK init.
        /// </summary>
        public static void ClearCredentialsInEditor()
        {
            ClearLegacyCredentials();
            ClearFileCredentials();
        }

#endif

        static GuestCredentials TryGetLegacyCredentials()
        {
            // Only use credentials if all entries found (otherwise may be partial save)
            // All fields must exist but PlayerId contents are only a hint and is optional.
            if (PlayerPrefs.HasKey(KeyDeviceId) && PlayerPrefs.HasKey(KeyAuthToken) && PlayerPrefs.HasKey(KeyPlayerId))
            {
                EntityId playerIdHint = EntityId.None;
                try
                {
                    playerIdHint = EntityId.ParseFromString(PlayerPrefs.GetString(KeyPlayerId));
                }
                catch
                {
                }

                return new GuestCredentials
                {
                    DeviceId    = PlayerPrefs.GetString(KeyDeviceId),
                    AuthToken   = PlayerPrefs.GetString(KeyAuthToken),
                    PlayerId    = playerIdHint,
                };
            }
            return null;
        }
        static void StoreLegacyCredentials(GuestCredentials credentials)
        {
            PlayerPrefs.SetString(KeyDeviceId, credentials.DeviceId.ToString());
            PlayerPrefs.SetString(KeyPlayerId, credentials.PlayerId.ToString());
            PlayerPrefs.SetString(KeyAuthToken, credentials.AuthToken);
            PlayerPrefs.Save();
        }
        static void ClearLegacyCredentials()
        {
            PlayerPrefs.DeleteKey(KeyDeviceId);
            PlayerPrefs.DeleteKey(KeyPlayerId);
            PlayerPrefs.DeleteKey(KeyAuthToken);
        }

        static GuestCredentials TryGetFileCredentials(string slot = "")
        {
            byte[] blob = AtomicBlobStore.TryReadBlob(GetCredentialStorePath(slot));
            if (blob != null)
                return GuestCredentialsSerializer.TryDeserialize(blob);
            return null;
        }
        static bool TryStoreFileCredentials(GuestCredentials credentials, string slot = "")
        {
            return AtomicBlobStore.TryWriteBlob(GetCredentialStorePath(slot), GuestCredentialsSerializer.Serialize(credentials));
        }
        static void ClearFileCredentials(string slot = "")
        {
            _ = AtomicBlobStore.TryDeleteBlob(GetCredentialStorePath(slot));
        }
    }
}

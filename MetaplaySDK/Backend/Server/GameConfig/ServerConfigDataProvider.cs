// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Services;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using System;

namespace Metaplay.Server.GameConfig
{
    public class ServerConfigDataProvider : IDisposable
    {
        public IBlobStorage                  PublicBlobStorage;
        public LocalizationLanguageProvider  LocalizationLanguageProvider;

        public static ServerConfigDataProvider Instance => MetaplayServices.Get<ServerConfigDataProvider>();

        public ServerConfigDataProvider(RuntimeOptionsRegistry opts)
        {
            BlobStorageOptions blobStorageOpts = opts.GetCurrent<BlobStorageOptions>();

            // Setup public blob storage for game config data
            PublicBlobStorage = blobStorageOpts.CreatePublicBlobStorage("GameConfig");

            // Setup localization languages server-side provider (for accessing localization data from server code)
            IBlobProvider gameDataProvider = new StorageBlobProvider(PublicBlobStorage);
            LocalizationLanguageProvider = new LocalizationLanguageProvider(gameDataProvider, "Localizations");
        }

        public void Dispose()
        {
            PublicBlobStorage?.Dispose();
            PublicBlobStorage = null;
        }
    }
}

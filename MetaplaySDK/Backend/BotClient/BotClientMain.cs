// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Network;
using System;
using System.Threading.Tasks;

namespace Metaplay.BotClient
{
    // BotClientMain

    public class BotClientMain : Application
    {
        public sealed override string ApplicationSymbolicName => "BotClient";

        public BotClientMain()
        {
        }

        public Task<int> RunBotsAsync(string[] cmdLineArgs) => RunApplicationMainAsync(cmdLineArgs);

        void InitializeGameConfigProviders()
        {
            BotOptions botOpts = RuntimeOptionsRegistry.Instance.GetCurrent<BotOptions>();
            if (string.IsNullOrEmpty(botOpts.GameConfigCachePath))
                throw new InvalidOperationException("Must specify valid BotOptions.GameConfigCachePath");

            // Setup http-based GameConfigProvider with on-disk caching
            string cacheDirectory = botOpts.GameConfigCachePath;
            DiskBlobStorage cacheStorage = new DiskBlobStorage(cacheDirectory);
            MetaplayCdnAddress cdnAddress = MetaplayCdnAddress.Create(botOpts.CdnBaseUrl, prioritizeIPv4: true).GetSubdirectoryAddress("GameConfig");
            HttpBlobProvider httpProvider = new HttpBlobProvider(MetaHttpClient.DefaultInstance, cdnAddress);
            StorageBlobProvider cacheProvider = new StorageBlobProvider(cacheStorage);
            CachingBlobProvider cachingProvider = new CachingBlobProvider(httpProvider, cacheProvider);

            BotGameConfigProvider.Instance.Initialize(botOpts.GameConfigCachePath, cdnAddress);
            BotLocalizationLanguageProvider.Instance = new LocalizationLanguageProvider(cachingProvider, "Localizations");
        }

        protected override sealed Task StartCoreServicesAsync()
        {
            // Setup GameConfig providers
            InitializeGameConfigProviders();

            return Task.CompletedTask;
        }

        protected override sealed Task StopCoreServicesAsync()
        {
            _logger.Information("BotClient stopped");

            return Task.CompletedTask;
        }

        protected override sealed Task StartCoreApplicationAsync() => Task.CompletedTask;
        protected override sealed Task StopCoreApplicationAsync() => Task.CompletedTask;

        protected override void HandleKeyPress(ConsoleKeyInfo key)
        {
            BotOptions botOpts = RuntimeOptionsRegistry.Instance.GetCurrent<BotOptions>();
            switch (key.Key)
            {
                case ConsoleKey.OemPlus:
                    botOpts.MaxBots = 2 * botOpts.MaxBots;
                    Console.WriteLine("MaxBots = {0}", botOpts.MaxBots);
                    break;

                case ConsoleKey.OemMinus:
                    botOpts.MaxBots = Math.Max(1, botOpts.MaxBots / 2);
                    Console.WriteLine("MaxBots = {0}", botOpts.MaxBots);
                    break;
            }
        }
    }
}

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace Metaplay.Unity
{
    public interface IUnityGameConfigBuildIntegration : IMetaIntegrationSingleton<IUnityGameConfigBuildIntegration>
    {
        IGameConfigSourceFetcherConfig CreateFetcherConfig();
        IEnumerable<Type> GetCustomBuildSourceTypesForSource(string sourcePropertyName);
        IEnumerable<GameConfigBuildSource> GetAvailableGameConfigBuildSources(string sourcePropertyName);
    }

    public class DefaultUnityGameConfigBuildIntegration : IUnityGameConfigBuildIntegration
    {
        const string LocalFilesPath = "GameConfigs";

        public virtual IGameConfigSourceFetcherConfig CreateFetcherConfig()
        {
            byte[] base64 = Convert.FromBase64String("R09DU1BYLUdHdXpjdDNQQjBHUzFPWTJjX2VlQ2JvRzlOeUo=");

            return GameConfigSourceFetcherConfigCore.Create()
                .WithLocalFileSourcesPath(LocalFilesPath)
                .WithGoogleCredentialsFromUserInput("838201960126-326dl3694n2jmoe6gj2e29g1usof5j49.apps.googleusercontent.com", Encoding.UTF8.GetString(base64));
        }

        public virtual IEnumerable<Type> GetCustomBuildSourceTypesForSource(string sourcePropertyName)
        {
            yield return typeof(GoogleSheetBuildSource);
            yield return typeof(FileSystemBuildSource);
        }

        public virtual IEnumerable<GameConfigBuildSource> GetAvailableGameConfigBuildSources(string sourcePropertyName)
        {
            return IntegrationRegistry.Get<GameConfigBuildIntegration>().GetAvailableGameConfigBuildSources(sourcePropertyName);
        }
    }
}

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Cloud.Utility;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Server.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Server.GameConfig
{
    public class ServerGameDataBuildInfo
    {
        ServerGameDataBuildInfo() { }

        ServerGameDataBuildInfo(Type buildParametersType)
        {
            MetaDictionary<string, List<GameConfigBuildSource>> slotToAvailableSourcesMapping = IntegrationRegistry.Get<GameConfigBuildIntegration>()
                .GetAllAvailableBuildSources(buildParametersType)
                .ToMetaDictionary(x => x.SourceProperty, x => x.Sources.ToList());

            BuildSupported                        = slotToAvailableSourcesMapping.Any();
            BuildParametersType                   = buildParametersType;
            BuildParametersNamespaceQualifiedName = buildParametersType.ToNamespaceQualifiedTypeString();
            SlotToAvailableSourcesMapping         = slotToAvailableSourcesMapping;
        }

        public static ServerGameDataBuildInfo GameConfigBuildInfo => new ServerGameDataBuildInfo(
            IntegrationRegistry.Get<GameConfigBuildIntegration>().GetDefaultGameConfigBuildParametersType());

        public static ServerGameDataBuildInfo LocalizationsBuildInfo => new ServerGameDataBuildInfo(
            IntegrationRegistry.Get<GameConfigBuildIntegration>().GetDefaultLocalizationsBuildParametersType());

        public bool                                                   BuildSupported                        { get; }
        public Type                                                   BuildParametersType                   { get; }
        public string                                                 BuildParametersNamespaceQualifiedName { get; }
        public MetaDictionary<string, List<GameConfigBuildSource>> SlotToAvailableSourcesMapping         { get; }
    }

    public abstract class GameDataBuildBackgroundTask : BackgroundTask
    {
        [MetaSerializableDerived(1)]
        protected class Progress : IBackgroundTaskOutput
        {
            [MetaMember(1)] public string CurrentOperation { get; private set; }

            public Progress() { }

            public Progress(string currentOp)
            {
                CurrentOperation = currentOp;
            }
        }

        protected IGameConfigSourceFetcherConfig FetcherConfig()
        {
            GameConfigSourceFetcherConfigCore ret = GameConfigSourceFetcherConfigCore.Create();

            // Google sheets
            GoogleSheetOptions opts            = RuntimeOptionsRegistry.Instance.GetCurrent<GoogleSheetOptions>();
            string             credentialsJson = opts.CredentialsJson;
            if (credentialsJson != null)
                ret = ret.WithGoogleCredentialsJson(credentialsJson);

            return ret;
        }
    }

    [MetaSerializableDerived(1)]
    public class BuildStaticGameConfigTask : GameDataBuildBackgroundTask
    {
        [MetaMember(1)] public MetaGuid GameConfigId { get; private set; }
        [MetaMember(2)] public MetaGuid ParentConfigId { get; private set; }

        [MetaMember(3)] public GameConfigBuildParameters BuildParams { get; private set; }

        public BuildStaticGameConfigTask() { }

        public BuildStaticGameConfigTask(MetaGuid id, MetaGuid parentId, GameConfigBuildParameters buildParams)
        {
            GameConfigId   = id;
            ParentConfigId = parentId;
            BuildParams    = buildParams;
        }

        async Task RunInternal(BackgroundTaskContext context)
        {
            ConfigArchive parent         = null;
            MetaGuid      parentConfigId = MetaGuid.None;

            if (BuildParams.IsIncremental)
            {
                parentConfigId = ParentConfigId;
                if (!parentConfigId.IsValid)
                {
                    // Default to using current active
                    GlobalStatusResponse status = await context.EntityAskAsync(GlobalStateManager.EntityId, GlobalStatusRequest.Instance);
                    parentConfigId = status.ActiveStaticGameConfigId;
                    if (!parentConfigId.IsValid)
                        throw new ArgumentException("No ParentConfigVersion given and no current active GameConfig version");
                }

                context.UpdateTaskOutput(new Progress("Loading parent config"));
                MetaDatabase              db              = MetaDatabase.Get(QueryPriority.Normal);
                PersistedStaticGameConfig persistedParent = await db.TryGetAsync<PersistedStaticGameConfig>(parentConfigId.ToString());
                parent = ConfigArchive.FromBytes(persistedParent.ArchiveBytes);
            }

            // Build the static full game config
            context.UpdateTaskOutput(new Progress("Building GameConfig archive"));
            // \note Memory-expensive debug check is only enabled in local servers.
            //       It checks for code bugs, not config content bugs (though whether the bug triggers can
            //       depend on config content), so those bugs are likely to be caught at development time.
            GameConfigBuildDebugOptions debugOpts = new GameConfigBuildDebugOptions() { EnableDebugDumpCheck = RuntimeOptionsBase.IsLocalEnvironment };


            GameConfigBuild build = IntegrationRegistry.Get<GameConfigBuildIntegration>().MakeGameConfigBuild(FetcherConfig(), debugOpts);
            ConfigArchive   archive;
            string          failureInfo = null;

            void HandleException(GameConfigBuildReport gameConfigBuildReport)
            {
                context.UpdateTaskOutput(new Progress("Config build failed: persisting report"));

                GameConfigMetaData metaData      = build.GetMetaDataForBuild(parentConfigId, parent, BuildParams, gameConfigBuildReport);
                byte[]             metaDataBytes = metaData.ToBytes();
                ConfigArchiveEntry archiveEntry  = ConfigArchiveEntry.FromBlob("_metadata", metaDataBytes);

                archive     = ConfigArchive.CreateFromBuild(MetaTime.Now, Enumerable.Repeat(archiveEntry, 1));
                failureInfo = "Game Config build failed, see log for errors";
            }

            try
            {
                archive = await build.CreateArchiveAsync(MetaTime.Now, BuildParams, parentConfigId, parent);
                context.UpdateTaskOutput(new Progress("Persisting GameConfig archive"));
            }
            catch (GameConfigBuildFailed buildFailed)
            {
                // When config building fails with GameConfigBuildFailed, create an archive with only metadata about
                // the failed build in it.
                HandleException(buildFailed.BuildReport);
            }
            catch (Exception ex)
            {
                // Otherwise, we construct a build report from the exception message, and then create an archive with
                // only metadata about the failed in it.
                GameConfigBuildReport report = new GameConfigBuildReport(
                    new []
                    {
                        new GameConfigBuildMessage(
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            GameConfigLogLevel.Error,
                            "Game Config build ran into an exception during build.",
                            ex.ToString(), null,null,0)
                    }, null);

                HandleException(report);
            }

            byte[] bytes = ConfigArchiveBuildUtility.ToBytes(archive, CompressionAlgorithm.None, 0);
            await context.EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateGameConfigRequest()
                {
                    Id      = GameConfigId,
                    Content = bytes,
                    FailureInfo = failureInfo
                });
        }

        public override async Task<IBackgroundTaskOutput> Run(BackgroundTaskContext context)
        {
            try
            {
                await RunInternal(context);
            }
            catch (Exception ex)
            {
                await context.EntityAskAsync(
                    GlobalStateManager.EntityId,
                    new CreateOrUpdateGameConfigRequest()
                    {
                        Id          = GameConfigId,
                        FailureInfo = ex.ToString()
                    });
            }

            return new Progress("Done");
        }
    }

    [MetaSerializableDerived(3)]
    public class BuildLocalizationsTask : GameDataBuildBackgroundTask
    {
        [MetaMember(1)] public MetaGuid LocalizationsId { get; private set; }
        [MetaMember(2)] public LocalizationsBuildParameters BuildParams { get; private set; }

        BuildLocalizationsTask() { }

        public BuildLocalizationsTask(MetaGuid localizationsId, LocalizationsBuildParameters buildParams)
        {
            LocalizationsId = localizationsId;
            BuildParams = buildParams;
        }

        async Task RunInternal(BackgroundTaskContext context)
        {
            GameConfigBuildIntegration integration = IntegrationRegistry.Get<GameConfigBuildIntegration>();
            LocalizationsBuild         build       = integration.MakeLocalizationsBuild(FetcherConfig());

            ConfigArchive archive = await build.CreateArchiveAsync(MetaTime.Now, BuildParams, CancellationToken.None);
            byte[]        bytes   = ConfigArchiveBuildUtility.ToBytes(archive, CompressionAlgorithm.None, 0);

            await context.EntityAskAsync(
                GlobalStateManager.EntityId,
                new CreateOrUpdateLocalizationsRequest()
                {
                    Id          = LocalizationsId,
                    Content     = bytes
                });
        }

        public override async Task<IBackgroundTaskOutput> Run(BackgroundTaskContext context)
        {
            try
            {
                await RunInternal(context);
            }
            catch (Exception ex)
            {
                await context.EntityAskAsync(
                    GlobalStateManager.EntityId,
                    new CreateOrUpdateLocalizationsRequest()
                    {
                        Id          = LocalizationsId,
                        FailureInfo = ex.ToString()
                    });
            }

            return new Progress("Done");
        }
    }

}

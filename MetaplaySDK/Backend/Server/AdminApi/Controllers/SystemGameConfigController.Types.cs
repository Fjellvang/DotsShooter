// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.RuntimeOptions;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Metaplay.Server.AdminApi.Controllers
{
    public partial class SystemGameConfigController
    {
        public class LibraryCountGameConfig
        {
            public IReadOnlyDictionary<string, int> SharedLibraries { get; init; }
            public IReadOnlyDictionary<string, int> ServerLibraries { get; init; }
            public GameConfigMetaData               MetaData        { get; init; }

            LibraryCountGameConfig(IReadOnlyDictionary<string, int> sharedLibraries, IReadOnlyDictionary<string, int> serverLibraries, GameConfigMetaData metaData)
            {
                SharedLibraries = sharedLibraries;
                ServerLibraries = serverLibraries;
                MetaData        = metaData;
            }

            public static LibraryCountGameConfig FromGameConfig(FullGameConfig gameConfig)
            {
                return new LibraryCountGameConfig(
                    gameConfig.SharedConfig?.GetConfigEntries().Where(x => x.Entry != null).ToDictionary(x => x.EntryInfo.Name, x => x.Entry is IGameConfigLibraryEntry lib ? lib.Count : MetaSerializerTypeRegistry.GetTypeSpec(x.Entry.GetType()).Members.Count),
                    gameConfig.ServerConfig?.GetConfigEntries().Where(x => x.Entry != null).ToDictionary(x => x.EntryInfo.Name, x => x.Entry is IGameConfigLibraryEntry lib ? lib.Count : MetaSerializerTypeRegistry.GetTypeSpec(x.Entry.GetType()).Members.Count),
                    gameConfig.MetaData);
            }
        }

        public class GameConfigInfoBase
        {
            /// <summary>
            /// This is a best effort guess if this instance was constructed with only the metadata. As we can't load the archive to test that is currently available.
            /// </summary>
            public GameDataControllerUtility.GameDataStatus Status { get; protected init; }

            public MetaTime LastModifiedAt { get; protected init; }

            public MetaTime? PublishedAt { get; protected init; }
            public MetaTime? UnpublishedAt { get; protected init; }

            /// <summary>
            /// This is derived from the ConfigArchive.CreatedAt, this is currently (2023/12/11) always the start time of the build.
            /// </summary>
            public MetaTime BuildStartedAt { get; protected init; }
            public string   Source         { get; protected init; }
            public bool     IsActive       { get; protected init; }
            public bool     IsArchived     { get; protected init; }
            public MetaGuid Id             { get; protected init; }
            public string   Name           { get; protected init; }
            public string   Description    { get; protected init; }
            /// <summary>
            /// The version string for the full (server+shared) config archive.
            /// This is not the same hash as seen by the client; see <see cref="CdnVersion"/> for that instead.
            /// </summary>
            public string FullConfigVersion { get; protected init; }
            /// <summary>
            /// The version string for the CDN deliverable part, and to identify gameconfigs on client.
            /// </summary>
            public string CdnVersion { get; protected init; }

            public int BlockingGameConfigMessageCount { get; protected set; }

            public List<GameConfigErrorBase> PublishBlockingErrors    { get; protected init; }

            protected static int CalculateBlockingGameConfigMessages(GameConfigMetaData metaData)
            {
                GameConfigBuildOptions configBuildOptions = RuntimeOptionsRegistry.Instance.GetCurrent<GameConfigBuildOptions>();
                int                    blockingMessages   = 0;
                blockingMessages += metaData.BuildSummary.BuildMessagesCount[GameConfigLogLevel.Error];
                blockingMessages += metaData.BuildSummary.ValidationMessagesCount[GameConfigLogLevel.Error];

                if (configBuildOptions.TreatWarningsAsErrors)
                {
                    blockingMessages += metaData.BuildSummary.BuildMessagesCount[GameConfigLogLevel.Warning];
                    blockingMessages += metaData.BuildSummary.ValidationMessagesCount[GameConfigLogLevel.Warning];
                }

                return blockingMessages;
            }
        }

        public class ExperimentData
        {
            public string       DisplayName      { get; private init; }
            public string       Id               { get; private init; }
            public List<string> PatchedLibraries { get; private init; }
            public List<string> Variants         { get; private init; }

            public static ExperimentData Create(PlayerExperimentInfo experimentInfo)
            {
                List<string> patchedLibraries = new List<string>();

                foreach ((ExperimentVariantId _, PlayerExperimentInfo.Variant value) in experimentInfo.Variants)
                {
                    if (value.ConfigPatch?.ServerConfigPatch != null)
                        foreach ((string libraryKey, GameConfigEntryPatch _) in value.ConfigPatch.ServerConfigPatch.EnumerateEntryPatches())
                            if (!patchedLibraries.Contains(libraryKey))
                                patchedLibraries.Add(libraryKey);

                    if (value.ConfigPatch?.SharedConfigPatch != null)
                        foreach ((string libraryKey, GameConfigEntryPatch _) in value.ConfigPatch.SharedConfigPatch.EnumerateEntryPatches())
                            if (!patchedLibraries.Contains(libraryKey))
                                patchedLibraries.Add(libraryKey);
                }

                return new ExperimentData()
                {
                    Id               = experimentInfo.ExperimentId.ToString(),
                    DisplayName      = experimentInfo.DisplayName,
                    PatchedLibraries = patchedLibraries,
                    Variants         = experimentInfo.Variants.Select(x => x.Key.Value).ToList()
                };
            }
        }

        public enum GameConfigPhaseType
        {
            Build,
            Import
        }

        public enum GameConfigErrorType
        {
            /// <summary>
            /// An exception occurred which can't be attributed to a specific library
            /// </summary>
            Exception,

            /// <summary>
            /// Importing a library threw an exception, see <see cref="LibraryCountGameConfigInfo.LibraryImportErrors"/> for more info
            /// </summary>
            LibraryImport,

            /// <summary>
            /// An exception occurred that is already transformed to a string (likely from building, and stored in the database)
            /// With the changes to GameConfigMetaData, this is often `Game Config build failed, see log for errors` but can still be a stringified exception in rare cases.
            /// </summary>
            StringException,

            /// <summary>
            /// The build task mysteriously disappeared, likely due to the server stopping or crashing.
            /// </summary>
            TaskDisappeared,

            /// <summary>
            /// The build messages or validation messages contain one or more messages that are treated as errors.
            /// </summary>
            BlockingMessages,
        }

        public class GameConfigErrorBase
        {
            [Required]
            public GameConfigPhaseType PhaseType { get; init; }

            [Required]
            public GameConfigErrorType ErrorType { get; init; }

            public GameConfigErrorBase(GameConfigErrorType errorType, GameConfigPhaseType phaseType)
            {
                PhaseType = phaseType;
                ErrorType = errorType;
            }
        }

        public class StringExceptionGameConfigError : GameConfigErrorBase
        {
            public string FullException { get; init; }

            public StringExceptionGameConfigError(string stringException, GameConfigPhaseType phaseType): base(GameConfigErrorType.StringException, phaseType)
            {
                FullException = stringException;
            }
        }

        public class ExceptionGameConfigError : StringExceptionGameConfigError
        {
            public Type   ExceptionType { get; init; }
            public string Message       { get; init; }

            public ExceptionGameConfigError(Exception ex, GameConfigPhaseType phaseType) : base(ex.ToString(), phaseType)
            {
                ErrorType     = GameConfigErrorType.Exception;
                PhaseType     = phaseType;
                ExceptionType = ex.GetType();
                Message       = ex.Message;
            }
        }

        public class LibraryCountGameConfigInfo : GameConfigInfoBase
        {
            public LibraryCountGameConfig Contents    { get; private init; }
            public List<ExperimentData>   Experiments { get; private init; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<string, GameConfigErrorBase> LibraryImportErrors { get; private init; }

            public DashboardBuildReportSummary BuildReportSummary { get; private init; }

            public static LibraryCountGameConfigInfo FromGameConfig(
                PersistedStaticGameConfig persisted,
                MetaGuid activeId,
                FullGameConfig fullGameConfigOrNull,
                GameConfigImportExceptions importErrors,
                Dictionary<MetaGuid, BackgroundTaskStatus> statuses = null)
            {
                MetaGuid               id            = MetaGuid.Parse(persisted.Id);
                LibraryCountGameConfig libraryCounts = null;
                List<ExperimentData>   experiments   = null;

                DashboardBuildReportSummary buildReportSummary = null;

                if (fullGameConfigOrNull != null)
                {
                    libraryCounts = LibraryCountGameConfig.FromGameConfig(fullGameConfigOrNull);

                    if(fullGameConfigOrNull.ServerConfig != null)
                        experiments = fullGameConfigOrNull.ServerConfig.PlayerExperiments?.Values
                            .Select(ExperimentData.Create)
                            .ToList();

                    if (libraryCounts.MetaData?.BuildSummary != null)
                        buildReportSummary = DashboardBuildReportSummary.CreateFromBuildSummary(libraryCounts.MetaData.BuildSummary);
                }

                int blockingGameConfigMessageCount = libraryCounts?.MetaData?.BuildSummary == null ? 0 : CalculateBlockingGameConfigMessages(libraryCounts.MetaData);

                (GameDataControllerUtility.GameDataStatus status, List<GameConfigErrorBase> errors) = GetPublishableStatusAndErrors(
                    importErrors,
                    persisted.FailureInfo,
                    persisted,
                    statuses,
                    blockingGameConfigMessageCount);
                return new LibraryCountGameConfigInfo()
                {
                    Id                             = id,
                    Name                           = persisted.Name,
                    Description                    = persisted.Description,
                    FullConfigVersion              = persisted.VersionHash,
                    CdnVersion                     = GetCdnVersionForActiveConfig(id, activeId),
                    BuildStartedAt                 = MetaTime.FromDateTime(persisted.ArchiveBuiltAt != DateTime.MinValue ? persisted.ArchiveBuiltAt : id.GetDateTime()),
                    LastModifiedAt                 = MetaTime.FromDateTime(persisted.LastModifiedAt),
                    Source                         = persisted.Source,
                    IsActive                       = id == activeId,
                    IsArchived                     = persisted.IsArchived,
                    Status                         = status,
                    Contents                       = libraryCounts,
                    Experiments                    = experiments,
                    BlockingGameConfigMessageCount = blockingGameConfigMessageCount,
                    LibraryImportErrors           = importErrors?.LibraryImportExceptions?.ToDictionary<KeyValuePair<string, Exception>, string, GameConfigErrorBase>(
                            x => x.Key,
                            x => new ExceptionGameConfigError(x.Value, GameConfigPhaseType.Import)) ??
                        new Dictionary<string, GameConfigErrorBase>(),
                    PublishBlockingErrors = errors,
                    BuildReportSummary    = buildReportSummary,
                    PublishedAt           = persisted.PublishedAt != null ? MetaTime.FromDateTime(persisted.PublishedAt.Value) : null,
                    UnpublishedAt         = persisted.UnpublishedAt != null ? MetaTime.FromDateTime(persisted.UnpublishedAt.Value) : null
                };
            }
        }

        [MetaSerializable]
        [MetaBlockedMembers(1)]
        public struct StaticGameConfigBuildInput
        {
            [MetaMember(2)] public bool                                                 SetAsActive    { get; private set; }
            [MetaMember(3)] public GameConfigBuildParameters                            BuildParams    { get; private set; }
            [MetaMember(4)] public MetaGuid                                             ParentConfigId { get; private set; }
            [MetaMember(5)] public GameDataControllerUtility.GameDataEditableProperties Properties     { get; private set; }
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameConfigPublished)]
        public class GameConfigEventGameConfigPublished : GameConfigEventPayloadBase
        {
            [MetaMember(1)] public bool KickConnectedClients { get; private set; }

            GameConfigEventGameConfigPublished() { }

            public GameConfigEventGameConfigPublished(bool kickConnectedClients)
            {
                KickConnectedClients = kickConnectedClients;
            }
            override public string EventTitle       => "Published";
            override public string EventDescription => $"Game config published.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameConfigUnpublished)]
        public class GameConfigEventGameConfigUnpublished : GameConfigEventPayloadBase
        {
            public GameConfigEventGameConfigUnpublished() { }
            override public string EventTitle       => "Unpublished";
            override public string EventDescription => $"Game config unpublished.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameConfigStaticGameConfigUploaded)]
        public class GameConfigEventStaticGameConfigUploaded : GameConfigEventPayloadBase
        {
            public GameConfigEventStaticGameConfigUploaded() { }
            override public string EventTitle       => "Uploaded";
            override public string EventDescription => $"Static game config uploaded.";
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameConfigStaticGameConfigEdited)]
        public class GameConfigEventStaticGameConfigEdited : GameConfigEventPayloadBase
        {
            [MetaMember(2)] public GameDataControllerUtility.GameDataEditableProperties OldValues { get; private set; }
            [MetaMember(3)] public GameDataControllerUtility.GameDataEditableProperties NewValues { get; private set; }

            public GameConfigEventStaticGameConfigEdited() { }
            public GameConfigEventStaticGameConfigEdited(GameDataControllerUtility.GameDataEditableProperties oldValues, GameDataControllerUtility.GameDataEditableProperties newValues)
            {
                OldValues = oldValues;
                NewValues = newValues;
            }
            public override string EventTitle       => "Edited";
            public override string EventDescription => NewValues.GetSummary("Game Config", OldValues);
        }

        [MetaSerializableDerived(MetaplayAuditLogEventCodes.GameConfigStaticGameConfigBuildStarted)]
        public class GameConfigEventStaticGameBuildStarted : GameConfigEventPayloadBase
        {
            [MetaMember(1)] public StaticGameConfigBuildInput Input { get; private set; }

            public GameConfigEventStaticGameBuildStarted() { }
            public GameConfigEventStaticGameBuildStarted(StaticGameConfigBuildInput input)
            {
                Input = input;
            }
            override public string EventTitle       => "Build started";
            override public string EventDescription => $"Static game config build from source '{Input.BuildParams.DefaultSource?.DisplayName ?? "unknown"}' started, setAsActive: '{Input.SetAsActive}'.";
        }

        public class DashboardBuildReportSummary
        {
            DashboardBuildReportSummary(
                IReadOnlyDictionary<GameConfigLogLevel, int> buildLogLogLevelCounts,
                IReadOnlyDictionary<GameConfigLogLevel, int> validationResultsLogLevelCounts,
                IReadOnlyDictionary<GameConfigLogLevel, int> totalLogLevelCounts,
                bool isBuildMessagesTrimmed,
                bool isValidationMessagesTrimmed)
            {
                BuildLogLogLevelCounts          = buildLogLogLevelCounts;
                ValidationResultsLogLevelCounts = validationResultsLogLevelCounts;
                TotalLogLevelCounts             = totalLogLevelCounts;
                IsValidationMessagesTrimmed     = isValidationMessagesTrimmed;
                IsBuildMessagesTrimmed          = isBuildMessagesTrimmed;
            }

            public IReadOnlyDictionary<GameConfigLogLevel, int> BuildLogLogLevelCounts          { get; private init; }
            public IReadOnlyDictionary<GameConfigLogLevel, int> ValidationResultsLogLevelCounts { get; private init; }
            public IReadOnlyDictionary<GameConfigLogLevel, int> TotalLogLevelCounts             { get; private init; }
            public bool                                         IsValidationMessagesTrimmed     { get; private set; }
            public bool                                         IsBuildMessagesTrimmed          { get; private set; }

            public static DashboardBuildReportSummary CreateFromBuildSummary(GameConfigBuildSummary summary)
            {
                Dictionary<GameConfigLogLevel, int> totalLogLevelToCountMapping = summary.BuildMessagesCount.ToDictionary(x => x.Key, x => summary.ValidationMessagesCount[x.Key] + x.Value);

                return new DashboardBuildReportSummary(
                    summary.BuildMessagesCount,
                    summary.ValidationMessagesCount,
                    totalLogLevelToCountMapping,
                    summary.IsBuildMessagesTrimmed,
                    summary.IsValidationMessagesTrimmed);
            }
        }

        public class MinimalGameConfigInfo
        {
            public MinimalGameConfigInfo(StaticGameConfigInfo info)
            {
                BestEffortStatus               = info.Status;
                Name                           = info.Name;
                IsArchived                     = info.IsArchived;
                IsActive                       = info.IsActive;
                Id                             = info.Id;
                Description                    = info.Description;
                FullConfigVersion              = info.FullConfigVersion;
                BlockingGameConfigMessageCount = info.BlockingGameConfigMessageCount;
                BuildStartedAt                 = info.BuildStartedAt;
                Source                         = info.Source;
                if (info.Contents?.MetaData?.BuildSummary != null)
                    BuildReportSummary = DashboardBuildReportSummary.CreateFromBuildSummary(info.Contents.MetaData.BuildSummary);

                PublishBlockingErrors = info.PublishBlockingErrors;
                PublishedAt           = info.PublishedAt;
                UnpublishedAt           = info.UnpublishedAt;
            }

            public string Source { get; init; }

            /// <summary>
            /// This is derived from the ConfigArchive.CreatedAt, this is currently (2023/12/11) always the start time of the build.
            /// </summary>
            public MetaTime BuildStartedAt { get; init; }

            // TODO: this is currently a best effort guess as we can't load all gameconfigs to see if they still parse, we should keep a cache somewhere if it is still parseable to get correct information
            public GameDataControllerUtility.GameDataStatus BestEffortStatus { get; init; }

            public string Name { get; init; }

            public bool IsActive { get; init; }

            public bool IsArchived { get; init; }

            public List<GameConfigErrorBase> PublishBlockingErrors { get; init; }

            public MetaGuid Id { get; init; }

            public string Description { get; init; }

            public string FullConfigVersion { get; init; }

            public int BlockingGameConfigMessageCount { get; init; }

            public DashboardBuildReportSummary BuildReportSummary { get; init; }

            public MetaTime? PublishedAt   { get; protected init; }
            public MetaTime? UnpublishedAt { get; protected init; }
        }

        public class ConfigRequestPostArgs
        {
            public List<string> Libraries   { get; set; }
            public List<string> Experiments { get; set; }
        }
    }
}

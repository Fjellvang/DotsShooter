// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Message;
using Metaplay.Core.MultiplayerEntity.Messages;
using Metaplay.Core.Player;
using System;
using static Metaplay.Core.Message.SessionProtocol;

namespace Metaplay.Core.Client
{
    /// <summary>
    /// Client-side state of the resource negoatiation during session handshake.
    /// </summary>
    public class ClientSessionNegotiationResources
    {
        public MetaDictionary<ContentHash, ConfigArchive>                    ConfigArchives = new MetaDictionary<ContentHash, ConfigArchive>();
        public MetaDictionary<ContentHash, GameConfigSpecializationPatches>  PatchArchives  = new MetaDictionary<ContentHash, GameConfigSpecializationPatches>();
        public LocalizationLanguage                                             ActiveLanguage;

        public SessionProtocol.SessionResourceProposal ToResourceProposal()
        {
            OrderedSet<ContentHash> configVersions = new OrderedSet<ContentHash>();
            OrderedSet<ContentHash> patchVersions = new OrderedSet<ContentHash>();

            foreach (ContentHash version in ConfigArchives.Keys)
                configVersions.Add(version);
            foreach (ContentHash version in PatchArchives.Keys)
                patchVersions.Add(version);

            SessionProtocol.SessionResourceProposal proposal = new SessionProtocol.SessionResourceProposal(
                configVersions:                 configVersions,
                patchVersions:                  patchVersions,
                clientActiveLanguage:           ActiveLanguage?.LanguageId,
                clientLocalizationVersion:      ActiveLanguage?.Version ?? ContentHash.None
                );
            return proposal;
        }
    }

    /// <summary>
    /// Client-side result of resource negoatiation completed during session handshake.
    /// </summary>
    public class ClientSessionStartResources
    {
        public readonly int LogicVersion;
        readonly MetaDictionary<(ContentHash, ContentHash, GameConfigSpecializationKey), ISharedGameConfig> _gameConfigs;
        readonly MetaDictionary<ContentHash, GameConfigSpecializationPatches> _patches;
        public readonly OrderedSet<ContentHash> GameConfigBaselineVersions;
        public readonly OrderedSet<ContentHash> GameConfigPatchVersions;

        public ClientSessionStartResources(int logicVersion, MetaDictionary<(ContentHash, ContentHash, GameConfigSpecializationKey), ISharedGameConfig> gameConfigs, MetaDictionary<ContentHash, GameConfigSpecializationPatches> patches)
        {
            LogicVersion = logicVersion;
            _gameConfigs = gameConfigs;
            _patches = patches;

            GameConfigBaselineVersions = new OrderedSet<ContentHash>();
            GameConfigPatchVersions = new OrderedSet<ContentHash>();
            foreach ((ContentHash gameConfigVersion, ContentHash patchVersion, GameConfigSpecializationKey _) in gameConfigs.Keys)
            {
                GameConfigBaselineVersions.Add(gameConfigVersion);
                if (patchVersion != ContentHash.None)
                    GameConfigPatchVersions.Add(patchVersion);
            }
        }

        public static ClientSessionStartResources SpecializeResources(SessionProtocol.SessionStartSuccess sessionStartSuccess, ClientSessionNegotiationResources negotiationResources, Func<ConfigArchive, (GameConfigSpecializationPatches, GameConfigSpecializationKey)?, ISharedGameConfig> gameConfigImporter)
        {
            MetaDictionary<(ContentHash, ContentHash, GameConfigSpecializationKey), ISharedGameConfig> gameConfigs = new MetaDictionary<(ContentHash, ContentHash, GameConfigSpecializationKey), ISharedGameConfig>();
            MetaDictionary<ContentHash, GameConfigSpecializationPatches> gameConfigPatches = new MetaDictionary<ContentHash, GameConfigSpecializationPatches>();

            void TrySpecializeEntityResources(ContentHash sharedGameConfigVersion, ContentHash sharedConfigPatchesVersion, EntityActiveExperiment[] experiments, bool isPlayer)
            {
                MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ExperimentsToExperimentAssignment(experiments);

                // Check we have the baseline config archive
                if (!negotiationResources.ConfigArchives.TryGetValue(sharedGameConfigVersion, out ConfigArchive configArchive))
                {
                    if (isPlayer)
                        throw new InvalidOperationException("Handshake resources are malformed: cannot succeed without player game config");
                    return;
                }

                // Check we have the patch file
                (GameConfigSpecializationPatches, GameConfigSpecializationKey)? specializationMaybe;
                GameConfigSpecializationKey specializationKey = default;
                if (sharedConfigPatchesVersion != ContentHash.None)
                {
                    if (!negotiationResources.PatchArchives.TryGetValue(sharedConfigPatchesVersion, out GameConfigSpecializationPatches patches))
                    {
                        if (isPlayer)
                            throw new InvalidOperationException("Handshake resources are malformed: cannot succeed without player config patch");
                        return;
                    }

                    specializationKey = patches.CreateKeyFromAssignment(assignment);
                    specializationMaybe = (patches, specializationKey);

                    gameConfigPatches[patches.Version] = patches;
                }
                else
                {
                    if (assignment != null)
                        throw new InvalidOperationException("Handshake resources are malformed: experiment assignment given but no patch file version");

                    specializationMaybe = null;
                }

                (ContentHash, ContentHash, GameConfigSpecializationKey) key = (sharedGameConfigVersion, sharedConfigPatchesVersion, specializationKey);

                // If config is already seen, skip
                if (gameConfigs.ContainsKey(key))
                    return;

                gameConfigs[key] = gameConfigImporter(configArchive, specializationMaybe);
            }

            // Create resources for Player. For a successful session start, the negotiated resource contain the player's requested config.
            TrySpecializeEntityResources(sessionStartSuccess.PlayerState.SharedGameConfigVersion, sessionStartSuccess.PlayerState.SharedConfigPatchesVersion, sessionStartSuccess.PlayerState.ActiveExperiments, isPlayer: true);

            // Create resources for all entities for which were present in ClientSessionNegotiationResources. The session start may
            // contain additional entities, but config for those is not created here. Config for such entities are loaded dynamically at runtime.
            foreach (EntityInitialState entity in sessionStartSuccess.EntityStates)
                TrySpecializeEntityResources(entity.State.SharedGameConfigVersion, entity.State.SharedConfigPatchesVersion, entity.State.ActiveExperiments, isPlayer: false);

            return new ClientSessionStartResources(sessionStartSuccess.LogicVersion, gameConfigs, gameConfigPatches);
        }

        /// <summary>
        /// Returns negotiated game config for the entity. Returns null if game config is not available (caller needs to download it).
        /// This can happen if actor's <c>RequireClientHasGameConfigPresentOnSessionStart</c> is <c>false</c>.
        /// </summary>
        public ISharedGameConfig TryGetConfigForEntity(EntitySerializedState initialState)
        {
            return TryGetConfigForEntity(initialState.SharedGameConfigVersion, initialState.SharedConfigPatchesVersion, initialState.ActiveExperiments);
        }

        /// <summary>
        /// Returns negotiated game config for the player.
        /// </summary>
        public ISharedGameConfig GetConfigForPlayer(SessionProtocol.InitialPlayerState initialState)
        {
            return TryGetConfigForEntity(initialState.SharedGameConfigVersion, initialState.SharedConfigPatchesVersion, initialState.ActiveExperiments);
        }

        ISharedGameConfig TryGetConfigForEntity(ContentHash gameConfigBaselineVersion, ContentHash gameConfigPatchVersion, EntityActiveExperiment[] experiments)
        {
            // If we have a patch, we need the specialization key. Otherwise we use dummy key.
            GameConfigSpecializationKey specializationKey;
            if (gameConfigPatchVersion != ContentHash.None)
            {
                // No patches available? Cannot get key so cannot have config either.
                if (!_patches.TryGetValue(gameConfigPatchVersion, out GameConfigSpecializationPatches patches))
                    return null;

                MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = ExperimentsToExperimentAssignment(experiments);
                specializationKey = patches.CreateKeyFromAssignment(assignment);
            }
            else
            {
                specializationKey = default;
            }

            (ContentHash, ContentHash, GameConfigSpecializationKey) key = (gameConfigBaselineVersion, gameConfigPatchVersion, specializationKey);
            return _gameConfigs.GetValueOrDefault(key);
        }

        /// <summary>
        /// Returns experiment variant assignment. If assignment is empty, returns <c>null</c>.
        /// </summary>
        public static MetaDictionary<PlayerExperimentId, ExperimentVariantId> GetInitialStateExperimentAssignment(EntitySerializedState initialState)
        {
            return ExperimentsToExperimentAssignment(initialState.ActiveExperiments);
        }

        /// <inheritdoc cref="GetInitialStateExperimentAssignment(EntitySerializedState)"/>
        public static MetaDictionary<PlayerExperimentId, ExperimentVariantId> GetInitialStateExperimentAssignment(InitialPlayerState initialState)
        {
            return ExperimentsToExperimentAssignment(initialState.ActiveExperiments);
        }

        static MetaDictionary<PlayerExperimentId, ExperimentVariantId> ExperimentsToExperimentAssignment(EntityActiveExperiment[] experiments)
        {
            if (experiments.Length == 0)
                return null;
            MetaDictionary<PlayerExperimentId, ExperimentVariantId> assignment = new MetaDictionary<PlayerExperimentId, ExperimentVariantId>(capacity: experiments.Length);
            foreach (EntityActiveExperiment experiment in experiments)
                assignment[experiment.ExperimentId] = experiment.VariantId;
            return assignment;
        }
    }
}

// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.InAppPurchase;
using Metaplay.Core.Localization;
using Metaplay.Core.Offers;
using Metaplay.Core.Player;
using System.Collections.Generic;

namespace Metaplay.Core.Config
{
    public interface IGameConfig : IGameConfigDataResolver
    {
        void Import(GameConfigImportParams importParams);
        bool TryImport(GameConfigImportParams importParams, out GameConfigImportExceptions importExceptions);
        void OnConfigEntriesPopulated(GameConfigImportParams importParams, bool isBuildingConfigs = false);
        void BuildTimeValidate(GameConfigValidationResult validationResult);
        ConfigArchiveEntry[] ExportMpcArchiveEntries();
        IEnumerable<(GameConfigEntryInfo EntryInfo, IGameConfigEntry Entry)> GetConfigEntries();

        int SpecializationSpecificDuplicationAmount { get; }
    }

    /// <summary>
    /// Base interface for game-specific SharedGameConfig classes.
    /// </summary>
    public interface ISharedGameConfig : IGameConfig
    {
        IGameConfigLibrary<LanguageId, LanguageInfo> Languages { get; }

        IGameConfigLibrary<InAppProductId, InAppProductInfoBase> InAppProducts { get; }

        IGameConfigLibrary<PlayerSegmentId, PlayerSegmentInfoBase> PlayerSegments { get; }

        IGameConfigLibrary<MetaOfferId, MetaOfferInfoBase> MetaOffers { get; }
        IGameConfigLibrary<MetaOfferGroupId, MetaOfferGroupInfoBase> MetaOfferGroups { get; }
        MetaDictionary<OfferPlacementId, List<MetaOfferGroupInfoBase>> MetaOfferGroupsPerPlacementInMostImportantFirstOrder { get; }
        MetaDictionary<MetaOfferId, List<MetaOfferGroupInfoBase>> MetaOfferContainingGroups { get; }
    }

    /// <summary>
    /// Base interface for game-specific ServerGameConfig classes.
    /// </summary>
    public interface IServerGameConfig : IGameConfig
    {
        IGameConfigLibrary<PlayerExperimentId, PlayerExperimentInfo> PlayerExperiments { get; }
    }
}

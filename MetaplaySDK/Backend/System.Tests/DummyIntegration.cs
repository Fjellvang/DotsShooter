// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using Metaplay.Core.Player;

namespace Metaplay.System.Tests;

[SupportedSchemaVersions(1,1)]
[MetaSerializableDerived(1)]
public class DummyPlayerModel : PlayerModelBase<DummyPlayerModel, PlayerStatisticsCore>
{
    public override EntityId PlayerId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override string PlayerName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public override int PlayerLevel { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    protected override void GameInitializeNewPlayerModel(MetaTime now, ISharedGameConfig gameConfig, EntityId playerId, string name)
    {
        throw new NotImplementedException();
    }

    protected override int GetTicksPerSecond()
    {
        throw new NotImplementedException();
    }
}

public class DummyGlobalOptions : IMetaplayCoreOptionsProvider
{
    /// <summary>
    /// Game-specific constant options for core Metaplay SDK.
    /// </summary>
    public MetaplayCoreOptions Options => new MetaplayCoreOptions(
        projectName:            "SystemTests",
        supportedLogicVersions: new MetaVersionRange(1, 1),
        clientLogicVersion:     1,
        guildInviteCodeSalt:    0x17,
        sharedNamespaces:       new string[] { },
        defaultLanguage:        LanguageId.FromString("en"),
        featureFlags: new MetaplayFeatureFlags
        {
            EnableLocalizations = false
        });
}

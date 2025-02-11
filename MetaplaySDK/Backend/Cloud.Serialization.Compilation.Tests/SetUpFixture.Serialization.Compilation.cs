// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Application;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Metaplay.Cloud.Tests
{
    // Needs to be in a Metaplay namespace for discoverability
    class TestOptions : IMetaplayCoreOptionsProvider
    {
        public MetaplayCoreOptions Options { get; } = new MetaplayCoreOptions(
            projectName: "Metaplay_Cloud_Tests",
            gameMagic: "TEST",
            clientLogicVersion: 1,
            supportedLogicVersions: new MetaVersionRange(1, 1),
            guildInviteCodeSalt: 0x17,
            sharedNamespaces: new string[] {},
            defaultLanguage: LanguageId.FromString("default-lang"),
            featureFlags: new MetaplayFeatureFlags
            {
                EnableLocalizations = true,
#if !METAPLAY_DISABLE_GUILDS
                EnableGuilds = true,
#endif
            });
    }

    [SupportedSchemaVersions(1, 1)]
    [MetaSerializableDerived(1)]
    public class TestPlayerModel : PlayerModelBase<TestPlayerModel, PlayerStatisticsCore>
    {
        public override EntityId PlayerId    { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public override string   PlayerName  { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public override int      PlayerLevel { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        protected override void GameInitializeNewPlayerModel(MetaTime now, ISharedGameConfig gameConfig, EntityId playerId, string name)
        {
            throw new System.NotImplementedException();
        }

        protected override int GetTicksPerSecond()
        {
            throw new System.NotImplementedException();
        }
    }
}

namespace Cloud.Serialization.Compilation.Tests
{
    [SetUpFixture]
    public class SetUpFixture
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            TestHelper.SetupForTests(
                init =>
                {
                    // Suppress scanning serializable types automatically by supplying an empty list of types
                    init.Add(_ => new TestSerializerTypeScanner(new List<Type>()).GetTypeInfo());
                });
        }
    }
}

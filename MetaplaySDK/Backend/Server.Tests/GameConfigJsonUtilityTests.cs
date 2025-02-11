    // This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Localization;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Tests;
using Metaplay.Server.GameConfig;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static System.FormattableString;

namespace Metaplay.Server.Tests;

public class GameConfigJsonUtilityTests
{
    [MetaSerializable]
    public class TestKeyValue : GameConfigKeyValue<TestKeyValue>
    {
        [MetaMember(2)] public int              Int;
        [MetaMember(3)] public Nested           Nested;
        [MetaMember(4)] public List<int>        IntList;
        [MetaMember(5)] public List<List<int>>  NestedIntList;
        [MetaMember(6)] public List<IntVector2> CoordList;
        [MetaMember(7)] public List<string>     StringList;
        [MetaMember(8)] public DeeplyNested     DeeplyNested;
    }

    [MetaSerializable]
    public class Nested
    {
        [MetaMember(1)] public string          StringMember;
        [MetaMember(2)] public List<int>       IntList;
        [MetaMember(3)] public List<List<int>> NestedIntList;
    }

    [MetaSerializable]
    public class DeeplyNested
    {
        [MetaMember(1)]
        public Nested Nested;
    }

    [MetaSerializable]
    public class TestData : IGameConfigData<int>
    {
        public const int TestDataDefaultConfigKey = 1;

        [MetaMember(1)]
        public int ConfigKey
        {
            get;
            set;
        }

        [MetaMember(2)] public int              Int;
        [MetaMember(3)] public Nested           Nested;
        [MetaMember(4)] public List<int>        IntList;
        [MetaMember(5)] public List<List<int>>  NestedIntList;
        [MetaMember(6)] public List<IntVector2> CoordList;
        [MetaMember(7)] public List<string>     StringList;
        [MetaMember(8)] public DeeplyNested     DeeplyNested;

        public TestData(int configKey = TestDataDefaultConfigKey)
        {
            ConfigKey = configKey;
        }

        public TestData() : this(TestDataDefaultConfigKey)
        {
        }
    }

    public class TestServerGameConfig : ServerGameConfigBase
    {
        [GameConfigEntry(PlayerExperimentsEntryName)]
        [GameConfigSyntaxAdapter(headerReplaces: new string[] { "ExperimentId -> ExperimentId #key" })]
        public GameConfigLibrary<PlayerExperimentId, PlayerExperimentInfo> PlayerExperiments { get;  set; }
    }

    public class TestSharedGameConfig : SharedGameConfigBase
    {
        [GameConfigEntry("TestKeyValue")]
        public TestKeyValue TestKeyValue { get; set; }

        [GameConfigEntry(nameof(TestLibrary))]
        public GameConfigLibrary<int, TestData> TestLibrary { get; set; }
    }

    public abstract class ConfigSpec
    {
        public abstract FullGameConfig CreateFullGameConfig();
    }

    public class ConfigSpec<TSharedGameConfig, TServerGameConfig> : ConfigSpec where TSharedGameConfig : SharedGameConfigBase, new() where TServerGameConfig : ServerGameConfigBase, new()
    {
        List<ConfigKVPSpecBase>                 KVPs      = new List<ConfigKVPSpecBase>();
        Dictionary<Type, ConfigLibrarySpecBase> libraries = new Dictionary<Type, ConfigLibrarySpecBase>();

        public abstract class ConfigLibrarySpecBase
        {
            public abstract IGameConfigLibrary BuildLibrary();
            public abstract GameConfigEntryPatch BuildPatch();
        }
        public abstract class ConfigKVPSpecBase
        {
            public abstract GameConfigKeyValue BuildKVP();
            public abstract GameConfigEntryPatch BuildPatch();
        }

        public class ConfigLibrarySpec<TKey, TInfo> : ConfigLibrarySpecBase where TInfo : class, IGameConfigData<TKey>, new()
        {
            List<TInfo> gameConfigData = new List<TInfo>();
            List<TInfo> patches = new List<TInfo>();

            public ConfigLibrarySpec<TKey, TInfo> Add(TInfo obj)
            {
                gameConfigData.Add(obj);
                return this;
            }

            public ConfigLibrarySpec<TKey, TInfo> Patch(TInfo obj)
            {
                patches.Add(obj);
                return this;
            }

            public override IGameConfigLibrary BuildLibrary()
            {
                return GameConfigLibrary<TKey, TInfo>.CreateSolo(gameConfigData.ToMetaDictionary(x => x.ConfigKey, x => x));
            }

            public override GameConfigEntryPatch BuildPatch()
            {
                return new GameConfigLibraryPatch<TKey, TInfo>(patches.Where(x=> gameConfigData.Any(y=> Equals(y.ConfigKey, x.ConfigKey))), patches.Where(x=> gameConfigData.Any(y=> !y.ConfigKey.Equals(x.ConfigKey))));
            }
        }

        public class ConfigKVPSpec<TStructure> : ConfigKVPSpecBase where TStructure : GameConfigKeyValue, new()
        {
            TStructure structure;
            MetaDictionary<string, object> patches = new MetaDictionary<string, object>();

            public ConfigKVPSpec(TStructure structure)
            {
                this.structure = structure;
            }

            public ConfigKVPSpec<TStructure> Patch(string member, object value)
            {
                patches.Add(member, value);
                return this;
            }

            public override GameConfigKeyValue BuildKVP()
            {
                return structure;
            }

            public override GameConfigEntryPatch BuildPatch()
            {
                return new GameConfigStructurePatch<TStructure>(patches);
            }
        }

        public ConfigSpec<TSharedGameConfig, TServerGameConfig> Add<TKey, TInfo>(TInfo info) where TInfo : class, IGameConfigData<TKey>, new()
        {
            if (!libraries.ContainsKey(typeof(TInfo)))
                libraries[typeof(TInfo)] = new ConfigLibrarySpec<TKey, TInfo>();
            ((ConfigLibrarySpec<TKey, TInfo>)libraries[typeof(TInfo)]).Add(info);

            return this;
        }

        public ConfigSpec<TSharedGameConfig, TServerGameConfig> Patch<TKey, TInfo>(TInfo info) where TInfo : class, IGameConfigData<TKey>, new()
        {
            if (!libraries.ContainsKey(typeof(TInfo)))
                libraries[typeof(TInfo)] = new ConfigLibrarySpec<TKey, TInfo>();
            ((ConfigLibrarySpec<TKey, TInfo>)libraries[typeof(TInfo)]).Patch(info);

            return this;
        }

        public ConfigKVPSpec<TStructure> AddKvp<TStructure>(TStructure obj) where TStructure : GameConfigKeyValue, new()
        {
            ConfigKVPSpec<TStructure> specBase = new ConfigKVPSpec<TStructure>(obj);
            KVPs.Add(specBase);
            return specBase;
        }

        public override FullGameConfig CreateFullGameConfig()
        {
            TSharedGameConfig sharedGameConfig = new TSharedGameConfig();
            TServerGameConfig serverGameConfig = new TServerGameConfig();

            MetaDictionary<PlayerExperimentId, PlayerExperimentInfo> playerExperimentInfos = new MetaDictionary<PlayerExperimentId, PlayerExperimentInfo>();

            Dictionary<string, GameConfigEntryPatch> sharedPatches = new Dictionary<string, GameConfigEntryPatch>();
            Dictionary<string, GameConfigEntryPatch> serverPatches = new Dictionary<string, GameConfigEntryPatch>();

            foreach ((Type _, ConfigLibrarySpecBase specBase) in libraries)
            {
                GameConfigEntryPatch patch = specBase.BuildPatch();
                IGameConfigLibrary   lib   = specBase.BuildLibrary();

                foreach ((string libraryName, GameConfigEntryInfo value) in GameConfigRepository.Instance.GetGameConfigTypeInfo(typeof(TSharedGameConfig)).Entries)
                {
                    if (value.MemberInfo.GetDataMemberType() == lib.GetType())
                    {
                        value.MemberInfo.GetDataMemberSetValueOnDeclaringType()(sharedGameConfig, lib);
                        sharedPatches.Add(libraryName, patch);
                    }
                }
                foreach ((string libraryName, GameConfigEntryInfo value) in GameConfigRepository.Instance.GetGameConfigTypeInfo(typeof(TServerGameConfig)).Entries)
                {
                    if (value.MemberInfo.GetDataMemberType() == lib.GetType())
                    {
                        value.MemberInfo.GetDataMemberSetValueOnDeclaringType()(serverGameConfig, lib);
                        serverPatches.Add(libraryName, patch);
                    }
                }
            }

            foreach (ConfigKVPSpecBase specBase in KVPs)
            {
                GameConfigEntryPatch patch = specBase.BuildPatch();
                GameConfigKeyValue   kvp   = specBase.BuildKVP();

                foreach ((string libraryName, GameConfigEntryInfo value) in GameConfigRepository.Instance.GetGameConfigTypeInfo(typeof(TSharedGameConfig)).Entries)
                {
                    if (value.MemberInfo.GetDataMemberType() == kvp.GetType())
                    {
                        value.MemberInfo.GetDataMemberSetValueOnDeclaringType()(sharedGameConfig, kvp);
                        sharedPatches.Add(libraryName, patch);
                    }
                }

                foreach ((string libraryName, GameConfigEntryInfo value) in GameConfigRepository.Instance.GetGameConfigTypeInfo(typeof(TServerGameConfig)).Entries)
                {
                    if (value.MemberInfo.GetDataMemberType() == kvp.GetType())
                    {
                        value.MemberInfo.GetDataMemberSetValueOnDeclaringType()(serverGameConfig, kvp);
                        serverPatches.Add(libraryName, patch);
                    }
                }
            }

            FullGameConfigPatch fullGameConfigPatch = new FullGameConfigPatch(new GameConfigPatch(typeof(TSharedGameConfig), sharedPatches), new GameConfigPatch(typeof(TServerGameConfig), serverPatches));

            PlayerExperimentInfo.Variant variant = new PlayerExperimentInfo.Variant(ExperimentVariantId.FromString("a"), "a")
            {
                ConfigPatch = fullGameConfigPatch
            };

            PlayerExperimentInfo playerExperimentInfo = new PlayerExperimentInfo(PlayerExperimentId.FromString("test.a"), new List<PlayerExperimentInfo.Variant>()
            {
                variant
            });

            playerExperimentInfos[PlayerExperimentId.FromString("test")] = playerExperimentInfo;

            GameConfigRepository.Instance.GetGameConfigTypeInfo(typeof(TServerGameConfig))
                .Entries["PlayerExperiments"]
                .MemberInfo.GetDataMemberSetValueOnDeclaringType()(
                    serverGameConfig,
                    GameConfigLibrary<PlayerExperimentId, PlayerExperimentInfo>.CreateSolo(playerExperimentInfos));

            return new FullGameConfig(sharedGameConfig, serverGameConfig, null);
        }
    }

    internal abstract class Constraint
    {
        protected List<Constraint> constraints = new List<Constraint>();

        public Constraint WithChild(string title)
        {
            ChildConstraint childConstraint = new ChildConstraint(title);
            constraints.Add(childConstraint);
            return childConstraint;
        }

        public Constraint WithValues(Dictionary<string, object> values)
        {
            ValuesConstraint valuesConstraint = new ValuesConstraint(values);
            constraints.Add(valuesConstraint);
            return valuesConstraint;
        }

        public Constraint WithValues(params (string key, object val)[] values)
        {
            ValuesConstraint valuesConstraint = new ValuesConstraint(values.ToDictionary(x=> x.key, x=> x.val));
            constraints.Add(valuesConstraint);
            return valuesConstraint;
        }

        public Constraint WithValue(object value)
        {
            ValueConstraint valueConstraint = new ValueConstraint("Baseline", value);
            constraints.Add(valueConstraint);
            return valueConstraint;
        }

        public Constraint WithValue(string variant, object value)
        {
            ValueConstraint valueConstraint = new ValueConstraint(variant, value);
            constraints.Add(valueConstraint);
            return valueConstraint;
        }

        public virtual bool Evaluate(GameConfigLibraryJsonConversionUtility.ConfigKey root)
        {
            Assert(root);

            foreach (Constraint constraint in constraints)
            {
                if (!constraint.Evaluate(root))
                    return false;
            }

            return true;
        }

        public abstract void Assert(GameConfigLibraryJsonConversionUtility.ConfigKey configKey);
    }

    internal class ChildConstraint : Constraint
    {
        string childTitle;

        public ChildConstraint(string childTitle)
        {
            this.childTitle = childTitle;
        }

        public override void Assert(GameConfigLibraryJsonConversionUtility.ConfigKey configKey)
        {
            NUnit.Framework.Assert.NotNull(configKey.Children, $"Expected {childTitle} to have children, but was null instead");

            GameConfigLibraryJsonConversionUtility.ConfigKey key = configKey.Children.FirstOrDefault(x => x.Title == childTitle);
            if (key == null)
                NUnit.Framework.Assert.Fail($"Expected {configKey.Title} to have child named {childTitle}.");
        }

        public override bool Evaluate(GameConfigLibraryJsonConversionUtility.ConfigKey root)
        {
            Assert(root);

            GameConfigLibraryJsonConversionUtility.ConfigKey child = root.Children.FirstOrDefault(x => x.Title == childTitle);

            foreach (Constraint constraint in constraints)
            {
                if (!constraint.Evaluate(child))
                    return false;
            }

            return true;
        }
    }

    internal class ValuesConstraint : Constraint
    {
        Dictionary<string, object> values;

        public ValuesConstraint(Dictionary<string, object> values)
        {
            this.values = values;
        }

        public override void Assert(GameConfigLibraryJsonConversionUtility.ConfigKey configKey)
        {
            CollectionAssert.AreEquivalent(values, configKey.Values, $"Values of {configKey.Title} are not equal with the expected values.");
        }
    }

    internal class ValueConstraint : Constraint
    {
        string variant;
        object value;

        public ValueConstraint(string variant, object value)
        {
            this.variant   = variant;
            this.value = value;
        }

        public override void Assert(GameConfigLibraryJsonConversionUtility.ConfigKey configKey)
        {
            if (!configKey.Values.ContainsKey(variant))
                NUnit.Framework.Assert.Fail($"Expected {configKey.Title} to have value entry with variant name: {variant}, but no entry was found.");

            NUnit.Framework.Assert.AreEqual(value, configKey.Values[variant], Invariant($"Expected {configKey.Title} with variant {variant} to have value {value}."));
        }
    }

    internal class Evaluator
    {
        protected List<Constraint> constraints = new List<Constraint>();

        public virtual bool Evaluate(GameConfigLibraryJsonConversionUtility.ConfigKey root)
        {
            foreach (Constraint constraint in constraints)
            {
                if (!constraint.Evaluate(root))
                    return false;
            }

            return true;
        }

        public Constraint WithChild(string title)
        {
            ChildConstraint childConstraint = new ChildConstraint(title);
            constraints.Add(childConstraint);
            return childConstraint;
        }
    }

    internal void ConstructAndEvaluate(ConfigSpec spec, string libraryName, Action<Evaluator> eval)
    {
        FullGameConfig                                                       fullGameConfig = spec.CreateFullGameConfig();
        Dictionary<string, GameConfigLibraryJsonConversionUtility.ConfigKey> configKeys     = GameConfigLibraryJsonConversionUtility.ConvertGameConfigToConfigKeys(fullGameConfig, new List<string>(){"test"}, NullLogger.Instance);

        Evaluator evaluator = new Evaluator();
        eval(evaluator);

        if (!configKeys.ContainsKey(libraryName))
            Assert.Fail($"Expected config contain library named {libraryName}, but no item was found.");

        if (!evaluator.Evaluate(configKeys[libraryName]))
            Assert.Fail("Evaluation failed.");
    }

    [Test]
    public void TestLibraryPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { Int = 1 })
            .Patch<int, TestData>(new TestData() { Int = 2 });

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intChild = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Int");
            intChild.WithValue(1);
            intChild.WithValue("test.a", 2);
        });
    }

    [Test]
    public void TestLibrarySubClassPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { Nested = new Nested() { StringMember = "Test" }})
            .Patch<int, TestData>(new TestData(){ Nested = new Nested() { StringMember = "newValue" }});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intChild = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Nested").WithChild("StringMember");
            intChild.WithValue("Test");
            intChild.WithValue("test.a", "newValue");
        });
    }

    [Test]
    public void TestLibraryClassArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4) }})
            .Patch<int, TestData>(new TestData() { CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(3,4), new IntVector2(5,6) }});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild(nameof(TestKeyValue.CoordList));
            intList.WithChild("[0]").WithChild("X").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("Y").WithValues(("Baseline", 2));

            intList.WithChild("[1]").WithChild("X").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[1]").WithChild("Y").WithValues(("Baseline", 3), ("test.a", 4));

            intList.WithChild("[2]").WithChild("X").WithValues(("Baseline", 3), ("test.a", 5));
            intList.WithChild("[2]").WithChild("Y").WithValues(("Baseline", 4), ("test.a", 6));
        });
    }

    [Test]
    public void TestLibraryClassArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4) }})
            .Patch<int, TestData>(new TestData(){ CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4), new IntVector2(5,6) }});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild(nameof(TestKeyValue.CoordList));
            intList.WithChild("[0]").WithChild("X").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("Y").WithValues(("Baseline", 2));

            intList.WithChild("[1]").WithChild("X").WithValues(("Baseline", 2));
            intList.WithChild("[1]").WithChild("Y").WithValues(("Baseline", 3));

            intList.WithChild("[2]").WithChild("X").WithValues(("Baseline", 3));
            intList.WithChild("[2]").WithChild("Y").WithValues(("Baseline", 4));

            intList.WithChild("[3]").WithChild("X").WithValues(("test.a", 5));
            intList.WithChild("[3]").WithChild("Y").WithValues(("test.a", 6));
        });
    }

    [Test]
    public void TestLibraryPrimitiveArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { IntList = new List<int> {1,2,3,4}})
            .Patch<int, TestData>(new TestData() { IntList = new List<int> {1,3,3,5}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestLibraryPrimitiveArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { IntList = new List<int> {1,2,3,4}})
            .Patch<int, TestData>(new TestData() { IntList     = new List<int> {1,3,3,5,7}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestLibraryPrimitiveArrayRemoved()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { IntList = new List<int> {1,2,3,4}})
            .Patch<int, TestData>(new TestData() { IntList     = new List<int> {1,3,3}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4));
        });
    }

    [Test]
    public void TestLibraryNestedPrimitiveArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}})
            .Patch<int, TestData>(new TestData(){ NestedIntList      = new List<List<int>> {new List<int>(){1,3,3,5}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestLibraryDeeplyNestedPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { DeeplyNested   = new DeeplyNested(){ Nested = new Nested() {StringMember = "Test" }}})
            .Patch<int, TestData>(new TestData() {DeeplyNested = new DeeplyNested(){ Nested = new Nested() {StringMember = "newValue" }}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intChild = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("DeeplyNested").WithChild("Nested").WithChild("StringMember");
            intChild.WithValue("Test");
            intChild.WithValue("test.a", "newValue");
        });
    }

    [Test]
    public void TestLibraryNestedPrimitiveArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}})
            .Patch<int, TestData>(new TestData(){ NestedIntList      = new List<List<int>> {new List<int>(){1,3,3,5,7}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[0]").WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestLibrarySubClassArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { Nested = new Nested() { IntList = new List<int> {1,2,3,4}}})
            .Patch<int, TestData>(new TestData() {Nested = new Nested(){ IntList   = new List<int> {1,3,3,5,7}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Nested").WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestLibrarySubClassArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { Nested = new Nested() { IntList = new List<int> {1,2,3,4}}})
            .Patch<int, TestData>(new TestData() {Nested      = new Nested(){ IntList  = new List<int> {1,3,3,5}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Nested").WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestLibrarySubClassNestedArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData() { Nested     = new Nested() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}}})
            .Patch<int, TestData>(new TestData(){ Nested  = new Nested(){ NestedIntList = new List<List<int>> {new List<int>(){1,3,3,5}}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intList = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Nested").WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestLibraryItemAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.Add<int, TestData>(new TestData(1) { Int = 1 })
            .Patch<int, TestData>(new TestData(2) { Int     = 2 });

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestLibrary), (ev) =>
        {
            Constraint intChild = ev.WithChild(TestData.TestDataDefaultConfigKey.ToString(CultureInfo.InvariantCulture)).WithChild("Int");
            intChild.WithValue(1);
            Constraint intChild2 = ev.WithChild("2").WithChild("Int");
            intChild2.WithValue("test.a", 2);
        });
    }

    [Test]
    public void TestKVPPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { Int = 1})
            .Patch("Int", 2);

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intChild = ev.WithChild("Int");
            intChild.WithValue(1);
            intChild.WithValue("test.a", 2);
        });
    }

    [Test]
    public void TestKVPSubClassPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { Nested = new Nested(){ StringMember = "Test" }})
            .Patch("Nested", new Nested() { StringMember = "newValue" });

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intChild = ev.WithChild("Nested").WithChild("StringMember");
            intChild.WithValue("Test");
            intChild.WithValue("test.a", "newValue");
        });
    }

    [Test]
    public void TestKVPDeeplyNestedPrimitiveDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { DeeplyNested = new DeeplyNested(){ Nested = new Nested() {StringMember = "Test" }}})
            .Patch("DeeplyNested", new DeeplyNested(){ Nested     = new Nested() {StringMember = "newValue" }});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intChild = ev.WithChild("DeeplyNested").WithChild("Nested").WithChild("StringMember");
            intChild.WithValue("Test");
            intChild.WithValue("test.a", "newValue");
        });
    }

    [Test]
    public void TestKVPClassArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4) }})
            .Patch(nameof(TestKeyValue.CoordList), new List<IntVector2> { new IntVector2(1,2),  new IntVector2(3,4), new IntVector2(5,6) });

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild(nameof(TestKeyValue.CoordList));
            intList.WithChild("[0]").WithChild("X").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("Y").WithValues(("Baseline", 2));

            intList.WithChild("[1]").WithChild("X").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[1]").WithChild("Y").WithValues(("Baseline", 3), ("test.a", 4));

            intList.WithChild("[2]").WithChild("X").WithValues(("Baseline", 3), ("test.a", 5));
            intList.WithChild("[2]").WithChild("Y").WithValues(("Baseline", 4), ("test.a", 6));
        });
    }

    [Test]
    public void TestKVPClassArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { CoordList = new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4) }})
            .Patch(nameof(TestKeyValue.CoordList), new List<IntVector2> { new IntVector2(1,2),  new IntVector2(2,3), new IntVector2(3,4), new IntVector2(5,6) });

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild(nameof(TestKeyValue.CoordList));
            intList.WithChild("[0]").WithChild("X").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("Y").WithValues(("Baseline", 2));

            intList.WithChild("[1]").WithChild("X").WithValues(("Baseline", 2));
            intList.WithChild("[1]").WithChild("Y").WithValues(("Baseline", 3));

            intList.WithChild("[2]").WithChild("X").WithValues(("Baseline", 3));
            intList.WithChild("[2]").WithChild("Y").WithValues(("Baseline", 4));

            intList.WithChild("[3]").WithChild("X").WithValues(("test.a", 5));
            intList.WithChild("[3]").WithChild("Y").WithValues(("test.a", 6));
        });
    }

    [Test]
    public void TestKVPPrimitiveArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { IntList = new List<int> {1,2,3,4}})
            .Patch("IntList", new List<int> {1,3,3,5});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestKVPPrimitiveArrayRemoved()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { IntList = new List<int> {1,2,3,4}})
            .Patch("IntList", new List<int> {1,3,3});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4));
        });
    }

    [Test]
    public void TestKVPPrimitiveArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { IntList = new List<int> {1,2,3,4}})
            .Patch("IntList", new List<int> {1,3,3,5,7});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestKVPNestedPrimitiveArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}})
            .Patch("NestedIntList", new List<List<int>> {new List<int>(){1,3,3,5}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestKVPNestedPrimitiveArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}})
            .Patch("NestedIntList", new List<List<int>> {new List<int>(){1,3,3,5,7}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[0]").WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestKVPSubClassArrayAdded()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { Nested = new Nested() { IntList = new List<int> {1,2,3,4}}})
            .Patch("Nested",  new Nested(){ IntList   = new List<int> {1,3,3,5,7}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("Nested").WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
            intList.WithChild("[4]").WithValues(("test.a", 7));
        });
    }

    [Test]
    public void TestKVPSubClassArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { Nested = new Nested() { IntList = new List<int> {1,2,3,4}}})
            .Patch("Nested",  new Nested(){ IntList   = new List<int> {1,3,3,5}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("Nested").WithChild("IntList");
            intList.WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }

    [Test]
    public void TestKVPSubClassNestedArrayDiff()
    {
        ConfigSpec<TestSharedGameConfig, TestServerGameConfig> configSpec = new ConfigSpec<TestSharedGameConfig, TestServerGameConfig>();
        configSpec.AddKvp(new TestKeyValue() { Nested = new Nested() { NestedIntList = new List<List<int>> {new List<int>(){1,2,3,4}}}})
            .Patch("Nested",  new Nested(){ NestedIntList = new List<List<int>> {new List<int>(){1,3,3,5}}});

        ConstructAndEvaluate(configSpec, nameof(TestSharedGameConfig.TestKeyValue), (ev) =>
        {
            Constraint intList = ev.WithChild("Nested").WithChild("NestedIntList");
            intList.WithChild("[0]").WithChild("[0]").WithValues(("Baseline", 1));
            intList.WithChild("[0]").WithChild("[1]").WithValues(("Baseline", 2), ("test.a", 3));
            intList.WithChild("[0]").WithChild("[2]").WithValues(("Baseline", 3));
            intList.WithChild("[0]").WithChild("[3]").WithValues(("Baseline", 4), ("test.a", 5));
        });
    }
}

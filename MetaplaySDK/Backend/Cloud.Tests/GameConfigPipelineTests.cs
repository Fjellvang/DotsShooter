// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using Metaplay.Core.Tests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cloud.Tests
{
    [TestFixture(TestFlags.Default)]
    [TestFixture(TestFlags.PrependNoOpColumn)]
    public class GameConfigPipelineTests
    {
        public enum TestFlags
        {
            Default = 0,

            /// <summary>
            /// If enabled, then each test is varied by adding an empty leftmost column to each sheet.
            /// The source column numbers of the existing cells are kept unchanged.
            /// The purpose of this is to test that correct source locations are reported,
            /// in particular that concrete column indexes in `sheet` are not confused with
            /// source column numbers.
            /// </summary>
            PrependNoOpColumn = 1 << 0,
        }

        TestFlags _flags = TestFlags.Default;

        public GameConfigPipelineTests(TestFlags flags)
        {
            _flags = flags;
        }

        public static class Helper
        {
            public static bool SequenceEquals<T>(IEnumerable<T> a, IEnumerable<T> b)
            {
                if (ReferenceEquals(a, b))
                    return true;

                if (a is null || b is null)
                    return false;

                return a.SequenceEqual(b);
            }
        }

        [MetaSerializable]
        public class LeftId : StringId<LeftId> { }

        [MetaSerializable]
        public class LeftInfo : IGameConfigData<LeftId>
        {
            public LeftId ConfigKey => Id;

            [MetaMember(1)] public LeftId Id    { get; set; }
            [MetaMember(2)] public string Name  { get; set; }

            [MetaMember(3)] public Nested Nested { get; set; }
            [MetaMember(4)] public List<int> IntList { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not LeftInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(Name, other.Name)
                    && Equals(Nested, other.Nested)
                    && Helper.SequenceEquals(IntList, other.IntList);
            }

            public override int GetHashCode() => throw new NotSupportedException("do not use");
        }

        [MetaSerializable]
        public class RightId : StringId<RightId> { }

        // \note Used to test logging during transforms
        [MetaSerializable]
        public class RightSourceItem : IGameConfigSourceItem<RightId, RightInfo>
        {
            public RightId ConfigKey => Id;

            [MetaMember(1)] public RightId               Id       { get; set; }
            [MetaMember(2)] public string                Name     { get; set; }
            [MetaMember(3)] public MetaRef<LeftInfo>     Left     { get; set; }
            [MetaMember(4)] public MetaConfigId<LeftInfo> ConfigIdLeft { get; set; }

            public RightInfo ToConfigData(GameConfigBuildLog buildLog)
            {
                if (Name.Contains("EmitWarning"))
                    buildLog.Warning("Item requested to emit a warning message during item transformation");
                if (Name.Contains("EmitInformation"))
                    buildLog.Information("Item requested to emit an information message during item transformation");

                return new RightInfo
                {
                    Id = Id,
                    Name = Name,
                    Left = Left,
                    ConfigIdLeft = ConfigIdLeft,
                };
            }
        }

        [MetaSerializable]
        public class RightInfo : IGameConfigData<RightId>
        {
            public RightId ConfigKey => Id;

            [MetaMember(1)] public RightId               Id       { get; set; }
            [MetaMember(2)] public string                Name     { get; set; }
            [MetaMember(3)] public MetaRef<LeftInfo>     Left     { get; set; }
            [MetaMember(4)] public MetaConfigId<LeftInfo> ConfigIdLeft { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not RightInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(Name, other.Name)
                    && Equals(Left, other.Left);
            }

            public override int GetHashCode() => throw new NotSupportedException("do not use");
        }

        [MetaSerializable]
        public class Nested
        {
            [MetaMember(1)] public string StringMember;

            public override bool Equals(object obj)
            {
                if (obj is not Nested other)
                    return false;

                return Equals(StringMember, other.StringMember);
            }

            public override int GetHashCode() => throw new NotSupportedException("do not use");
        }

        [MetaSerializable]
        public class TestKeyValue : GameConfigKeyValue<TestKeyValue>
        {
            [MetaMember(1)] public MetaRef<LeftInfo> Left;
            [MetaMember(2)] public int Int;
            [MetaMember(3)] public Nested Nested;
            [MetaMember(4)] public List<int> IntList;
            [MetaMember(5)] public List<IntVector2> CoordList;
            [MetaMember(6)] public List<string> StringList;
            [MetaMember(7)] public MetaConfigId<LeftInfo> ConfigIdLeft;
        }

        public class TestSharedGameConfig : SharedGameConfigBase
        {
            [GameConfigEntry("Left")]
            public GameConfigLibrary<LeftId, LeftInfo> Left { get; set; }

            [GameConfigEntry("Right")]
            [GameConfigEntryTransform(typeof(RightSourceItem))]
            public GameConfigLibrary<RightId, RightInfo> Right { get; set; }

            [GameConfigEntry("TestKeyValue")]
            public TestKeyValue TestKeyValue { get; set; }

            public override void BuildTimeValidate(GameConfigValidationResult validationResult)
            {
                base.BuildTimeValidate(validationResult);

                foreach (LeftInfo left in Left.Values)
                    validationResult.Info(nameof(Left), left.ConfigKey.ToString(), $"Test validation message: {left.Name}");
            }
        }

        class TestGameConfigBuild : GameConfigBuildTemplate<TestSharedGameConfig>
        {
            public TestGameConfigBuild()
            {
                SharedConfigType = typeof(TestSharedGameConfig);
            }
        }

        class TestConfigBuildIntegration : GameConfigBuildIntegration
        {
            public UnknownConfigMemberHandling? UnknownMemberHandlingOverride = null;
            public override UnknownConfigMemberHandling UnknownConfigItemMemberHandling => UnknownMemberHandlingOverride ?? base.UnknownConfigItemMemberHandling;

            public override Type GetDefaultGameConfigBuildParametersType() => typeof(DefaultGameConfigBuildParameters);

            public override GameConfigBuild MakeGameConfigBuild(IGameConfigSourceFetcherConfig fetcherConfig, GameConfigBuildDebugOptions debugOpts)
            {
                return new TestGameConfigBuild()
                {
                    SourceFetcherProvider = MakeSourceFetcherProvider(fetcherConfig),
                    DebugOptions          = debugOpts,
                    Integration           = this,
                };
            }
        }

        SpreadsheetContent ParseSpreadsheet(string sheetName, IEnumerable<IEnumerable<string>> rows)
        {
            SpreadsheetContent sheet = GameConfigTestHelper.ParseSpreadsheet(sheetName, rows);

            // See comment on PrependNoOpColumn.
            // \note There's a kludgy check here for TestKeyValue.
            //       Can't actually prepend a column to a GameConfigKeyValue sheet because it has a fixed column schema.
            //       This should change once we merge the implementation of richer GameConfigKeyValue syntax (which always includes a header).
            if ((_flags & TestFlags.PrependNoOpColumn) != 0
                && sheetName != "TestKeyValue")
            {
                for (int rowNdx = 0; rowNdx < sheet.Cells.Count; rowNdx++)
                {
                    List<SpreadsheetCell> row = sheet.Cells[rowNdx];

                    // Leave completely-empty rows unchanged.
                    // Some checks specifically check if a row is completely empty,
                    // at least the empty-header check in GameConfigParsePipeline.PreprocessSpreadsheet;
                    // we don't want to affect that behavior.
                    if (row.Count == 0)
                        continue;

                    bool isCommentRow = row.Count > 0 && row[0].Value.StartsWith("//", StringComparison.Ordinal);
                    string newCellValue = isCommentRow ? "//" : "";
                    row.Insert(0, new SpreadsheetCell(newCellValue, row: rowNdx, column: 0));
                }
            }

            return sheet;
        }

        Task<(TestSharedGameConfig, GameConfigBuildReport)> BuildSharedGameConfigAsync(params SpreadsheetContent[] inputSheets)
        {
            return BuildSharedGameConfigAsync(new TestConfigBuildIntegration(), inputSheets);
        }

        async Task<(TestSharedGameConfig, GameConfigBuildReport)> BuildSharedGameConfigAsync(TestConfigBuildIntegration integration, params SpreadsheetContent[] inputSheetsParam)
        {
            List<SpreadsheetContent> inputSheets = inputSheetsParam.ToList();
            AddDefaultsForMissingSheets(inputSheets);

            try
            {
                // Build the StaticGameConfig ConfigArchive
                DefaultGameConfigBuildParameters buildParams = new DefaultGameConfigBuildParameters()
                {
                    DefaultSource = new StaticDataDictionaryBuildSource(inputSheets.Select(sheet => (sheet.Name, (object)sheet)))
                };

                GameConfigBuild build = integration.MakeGameConfigBuild(null, new GameConfigBuildDebugOptions());
                ConfigArchive staticArchive = await build.CreateArchiveAsync(MetaTime.Now, buildParams, MetaGuid.None, null, printErrorsToConsole: false);

                // Extract SharedGameConfig from StaticGameConfig
                ReadOnlyMemory<byte> sharedArchiveBytes = staticArchive.GetEntryBytes("Shared.mpa");
                ConfigArchive sharedArchive = ConfigArchive.FromBytes(sharedArchiveBytes);

                // Import the shared game config (without variants)
                // \note Must provide the game config type explicitly as it's not the default in the tests
                GameConfigImportParams importParams = GameConfigImportParams.CreateSoloUnpatched(typeof(TestSharedGameConfig), sharedArchive, isBuildingConfigs: false, isConfigBuildParent: false);
                TestSharedGameConfig gameConfig = (TestSharedGameConfig)GameConfigFactory.Instance.ImportGameConfig(importParams);

                // Extract build report from archive
                ReadOnlyMemory<byte> metaDataBytes = staticArchive.GetEntryBytes("_metadata");
                GameConfigMetaData metadata = MetaSerialization.DeserializeTagged<GameConfigMetaData>(metaDataBytes.ToArray(), MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null); // \todo Optimize ToArray() alloc

                return (gameConfig, metadata.BuildReport);
            }
            catch (GameConfigBuildFailed failed)
            {
                return (null, failed.BuildReport);
            }
        }

        /// <summary>
        /// Adds empty-content (just header present) sheets that are missing in <paramref name="inputSheets"/>
        /// but are required to build <see cref="TestSharedGameConfig"/>.
        /// The purpose is to allow omitting irrelevant sheets in test cases, for brevity.
        /// </summary>
        void AddDefaultsForMissingSheets(List<SpreadsheetContent> inputSheets)
        {
            void AddIfMissing(string name, Func<List<string>[]> getRows)
            {
                if (!inputSheets.Any(sheet => sheet.Name == name))
                    inputSheets.Add(ParseSpreadsheet(name, getRows()));
            }

            AddIfMissing("Left", () => new List<string>[]
            {
                new() { "Id #key" },
            });

            AddIfMissing("Right", () => new List<string>[]
            {
                new() { "Id #key" },
            });

            AddIfMissing("TestKeyValue", () => new List<string>[]
            {
                new() { "Member", "Value" },
            });
        }

        [Test]
        public async Task SuccessTest()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name"    },
                    new() { "Left1",   "Left 1"  },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key", "Name",    "Left",  "ConfigIdLeft"     },
                    new() { "Right1",  "Right 1", "Left1", "Left1"        },
                })
            );

            Assert.AreEqual(0, report.GetMessageCountForLevel(GameConfigLogLevel.Warning));
            Assert.AreEqual(0, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            LeftId left1Id = LeftId.FromString("Left1");
            LeftInfo left1 = gameConfig.Left[left1Id];

            Assert.AreEqual(
                new LeftInfo[]
                {
                    new LeftInfo { Id = LeftId.FromString("Left1"), Name = "Left 1" },
                },
                gameConfig.Left.Values);

            Assert.AreEqual(
                new RightInfo[]
                {
                    new RightInfo { Id = RightId.FromString("Right1"), Name = "Right 1", Left = MetaRef<LeftInfo>.FromItem(left1), ConfigIdLeft = MetaConfigId<LeftInfo>.FromKey(left1.ConfigKey) },
                },
                gameConfig.Right.Values);
        }

        [Test]
        public async Task ParserErrorsTest()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name"    },
                    new() { "Left1",   "Left 1"  },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key", "Name",    "Left"     },
                    new() { "***",     "Right 1", "Left1"    }, // bad ids
                    new() { "###",     "Right 1", "Left1"    },
                })
            );

            // Fail with two build errors
            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            // Check build error messages
            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Right.csv:A2", msg0.LocationUrl);
            StringAssert.Contains("ParseError", msg0.Exception);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Right.csv:A3", msg1.LocationUrl);
            StringAssert.Contains("ParseError", msg1.Exception);
        }

        [Test]
        public async Task EmptySheetTest()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    // empty sheet
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { }, // empty header
                    new() { "Right1", "Right 1", "Left1"    },
                })
            );

            //Console.WriteLine("BUILD LOG:\n{0}", string.Join("\n", report.BuildMessages.Select(msg => msg.ToString())));

            // Fail with two build errors
            Assert.IsNull(gameConfig);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            // Check build error messages
            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            StringAssert.Contains("Left.csv", msg0.SourceInfo);
            Assert.AreEqual("Left.csv:", msg0.LocationUrl); // \todo is format for files good?
            StringAssert.Contains("Input sheet is completely empty", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            StringAssert.Contains("Right.csv", msg1.SourceInfo);
            Assert.AreEqual("Right.csv:1:1", msg1.LocationUrl); // \todo is format for files good?
            StringAssert.Contains("Input sheet header row is empty", msg1.Message);
        }

        [Test]
        public async Task DuplicateKeyTest()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "/Variant", "Id #key", "Name"        },
                    new() { "",         "Left1",   "Left 1a"     },
                    new() { "",         "Left2",   "Left 2a"     },
                    new() { "",         "Left1",   "Left 1b"     }, // duplicate key
                    new() { "",         "Left2",   "Left 2b"     }, // duplicate key
                    new() { "A/X",      "Left1",   "Left 1 Xa"   },
                    new() { "A/Y",      "",        "Left 1 Y"    },
                    new() { "A/X",      "",        "Left 1 Xb"   }, // duplicate key/variant
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key", "Name"    },
                    new() { "Right1",  "Right 1" },
                })
            );

            //Console.WriteLine("BUILD LOG:\n{0}", string.Join("\n", report.BuildMessages.Select(msg => msg.ToString())));

            // Check build report
            Assert.IsNull(gameConfig);
            Assert.AreEqual(3, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            // Check build error messages
            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:4:4", msg0.LocationUrl);
            StringAssert.Contains("Duplicate object with key 'Left1', other copy is at ", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:5:5", msg1.LocationUrl);
            StringAssert.Contains("Duplicate object with key 'Left2', other copy is at ", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual("Left.csv:8:8", msg2.LocationUrl);
            StringAssert.Contains("Duplicate object with key 'Left1' (variant 'A/X'), other copy is at ", msg2.Message);
        }

        [Test]
        public async Task BadReferencesTest_Basic()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name"    },
                    new() { "Left1",   "Left 1"  },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key", "Name",    "Left",   "ConfigIdLeft"     },
                    new() { "Right1",  "Right 1", "Left_",  "Left1"        }, // Nonexistent Left MetaRef
                    new() { "Right2",  "Right 2", "Left_2", "Left1"        }, // Nonexistent Left MetaRef
                    new() { "Right3",  "Right 3", "Left1",  "Left_4"       }, // Nonexistent Left MetaConfigId
                    new() { "Right4",  "Right 4", "Left1",  "Left_5"       }, // Nonexistent Left MetaConfigId
                }),
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",   "Value"   },
                    new() { "Left",     "Left_3"  }, // Nonexistent Left MetaRef
                    new() { "ConfigIdLeft", "Left_6"  }, // Nonexistent Left MetaConfigId
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(6, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            {
                GameConfigBuildMessage msg = report.BuildMessages[0];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("Right.csv:2:2", msg.LocationUrl);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_' in 'Right1' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[1];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("Right.csv:3:3", msg.LocationUrl);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_2' in 'Right2' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[2];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("Right.csv:4:4", msg.LocationUrl);
                StringAssert.Contains("Encountered a MetaConfigId<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_4' in 'Right3' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[3];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("Right.csv:5:5", msg.LocationUrl);
                StringAssert.Contains("Encountered a MetaConfigId<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_5' in 'Right4' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[4];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("TestKeyValue.csv:1:1", msg.LocationUrl); \todo Location mapping for GameConfigKeyValue configs doesn't work currently, enable this when it does.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_3' in 'TestKeyValue' (type GameConfigPipelineTests.TestKeyValue)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[5];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("TestKeyValue.csv:2:2", msg.LocationUrl); \todo Location mapping for GameConfigKeyValue configs doesn't work currently, enable this when it does.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                StringAssert.Contains("Encountered a MetaConfigId<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_6' in 'TestKeyValue' (type GameConfigPipelineTests.TestKeyValue)", msg.Message);
            }
        }

        [Test]
        public async Task BadReferencesTest_Variants()
        {
            // \note Existence of variant ids in the PlayerExperiments library is required for a valid config,
            //       but is irrelevant for this test because it is not checked until after MetaRef validity.
            //       This is an implementation detail; change if the implementation ever changes.

            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "/Variant",  "Id #key", "Name"      },
                    new() { "",          "Left1",   "Left 1"    },
                    new() { "A/X",       "LeftX",   "Left 2 x"  },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "/Variant", "Id #key", "Name",      "Left"    },
                    new() { "",         "Right1",  "Right 1",   "Left1"   },
                    new() { "A/X",      "Right1",  "Right 1 x", "Left_"   }, // Nonexistent Left. Reference is in a replacement item
                    new() { "A/Y",      "Right1",  "Right 1 y", "LeftX"   }, // Left only exists in a different variant. Reference is in a replacement item.
                    new() { "A/X",      "Right2",  "Right 2 x", "Left_"   }, // Nonexistent Left. Reference is in an added item.
                    new() { "A/Y",      "Right2",  "Right 2 y", "LeftX"   }, // Left only exists in a different variant. Reference is in an added item.
                    new() { "A/X",      "Right3",  "Right 3 x", "LeftX"   }, // Valid, sanity check - doesn't exist in baseline but does in the same variant.
                }),
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant", "Member",   "Value"    },
                    new() { "",         "Left",     "Left1"     },
                    new() { "A/X",      "Left",     "Left_2"    }, // Nonexistent Left.
                    new() { "A/Y",      "Left",     "LeftX"     }, // Left only exists in a different variant.
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(6, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            {
                GameConfigBuildMessage msg = report.BuildMessages[0];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("Right.csv:3:3", msg.LocationUrl); \todo Location mapping is incorrect (points to baseline) for variant-replaced items, enable this when fixed.
                Assert.AreEqual("Right.csv:2:2", msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/X)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_' in 'Right1' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[1];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("Right.csv:5:5", msg.LocationUrl); \todo Location mapping is missing for variant-appended items, enable this when fixed.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/X)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_' in 'Right2' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[2];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("TestKeyValue.csv:3:3", msg.LocationUrl); \todo Location mapping for GameConfigKeyValue configs doesn't work currently, enable this when it does.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/X)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'Left_2' in 'TestKeyValue' (type GameConfigPipelineTests.TestKeyValue)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[3];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("Right.csv:4:4", msg.LocationUrl); \todo Location mapping is incorrect (points to baseline) for variant-replaced items, enable this when fixed.
                Assert.AreEqual("Right.csv:2:2", msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/Y)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'LeftX' in 'Right1' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[4];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("Right.csv:6:6", msg.LocationUrl); \todo Location mapping is missing for variant-appended items, enable this when fixed.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/Y)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'LeftX' in 'Right2' (type GameConfigPipelineTests.RightInfo)", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[5];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                //Assert.AreEqual("TestKeyValue.csv:4:4", msg.LocationUrl); \todo Location mapping for GameConfigKeyValue configs doesn't work currently, enable this when it does.
                Assert.IsNull(msg.LocationUrl); // \todo See above, remove this when fixed.
                Assert.AreEqual("(A/Y)", msg.VariantId);
                StringAssert.Contains("Encountered a MetaRef<GameConfigPipelineTests.LeftInfo> reference to unknown item 'LeftX' in 'TestKeyValue' (type GameConfigPipelineTests.TestKeyValue)", msg.Message);
            }
        }

        [Test]
        public async Task MissingId()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Name"      },
                    new() { "Left 1"    },
                    new() { "Left 2"    },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("SpreadsheetFile:Left.csv", msg0.SourceInfo);
            StringAssert.Contains("No key columns were specified", msg0.Message);
        }

        [TestCase(null)] // \note Specifically tests that default handling is Error
        [TestCase(UnknownConfigMemberHandling.Error)]
        [TestCase(UnknownConfigMemberHandling.Warning)]
        [TestCase(UnknownConfigMemberHandling.Ignore)]
        public async Task UnknownMemberTest_Simple(UnknownConfigMemberHandling? unknownMemberHandlingOverride)
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                new TestConfigBuildIntegration
                {
                    UnknownMemberHandlingOverride = unknownMemberHandlingOverride
                },
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name",   "NonexistentMember" },
                    new() { "Left1",   "Left 1", "abc"               },
                    new() { "Left2",   "Left 2", ""                  }, // No value on this row -> no error. It's questionable if this is desirable, but this is natural for the current implementation.
                    new() { "Left3",   "Left 3", "def"               },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            // \ntoe When unknownMemberHandlingOverride is not given, we're using default, which is Error.
            UnknownConfigMemberHandling unknownMemberHandling = unknownMemberHandlingOverride ?? UnknownConfigMemberHandling.Error;

            // Check resulting game config.
            // - On Warning and Ignore, build should succeed despite possible build messages.
            // - On Error, build should not succeed.
            if (unknownMemberHandling == UnknownConfigMemberHandling.Warning || unknownMemberHandling == UnknownConfigMemberHandling.Ignore)
            {
                Assert.AreEqual(
                    new LeftInfo[]
                    {
                        new LeftInfo { Id = LeftId.FromString("Left1"), Name = "Left 1" },
                        new LeftInfo { Id = LeftId.FromString("Left2"), Name = "Left 2" },
                        new LeftInfo { Id = LeftId.FromString("Left3"), Name = "Left 3" },
                    },
                    gameConfig.Left.Values);

                Assert.AreEqual(new RightInfo[] {}, gameConfig.Right.Values);
            }
            else
            {
                Assert.IsNull(gameConfig);
            }

            // Check build messages.
            // - On Ignore, should at most have Information messages (which we don't check here as they're unrelated).
            // - On Error and Warning, should have Error or Warning messages respectively.
            if (unknownMemberHandling == UnknownConfigMemberHandling.Ignore)
            {
                Assert.LessOrEqual(report.HighestMessageLevel, GameConfigLogLevel.Information);
            }
            else
            {
                GameConfigLogLevel expectedLogLevel;

                if (unknownMemberHandling == UnknownConfigMemberHandling.Error)
                    expectedLogLevel = GameConfigLogLevel.Error;
                else
                    expectedLogLevel = GameConfigLogLevel.Warning;

                Assert.AreEqual(expectedLogLevel, report.HighestMessageLevel);
                Assert.AreEqual(2, report.GetMessageCountForLevel(expectedLogLevel));

                GameConfigBuildMessage msg0 = report.BuildMessages[0];
                Assert.AreEqual(expectedLogLevel, msg0.Level);
                Assert.AreEqual("Left.csv:C2", msg0.LocationUrl);
                StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.LeftInfo", msg0.Message);

                GameConfigBuildMessage msg1 = report.BuildMessages[1];
                Assert.AreEqual(expectedLogLevel, msg1.Level);
                Assert.AreEqual("Left.csv:C4", msg1.LocationUrl);
                StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.LeftInfo", msg1.Message);
            }
        }

        [Test]
        public async Task UnknownMemberTest_NestedMember()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name",   "Nested.StringMember",  "Nested.NonexistentMember"  },
                    new() { "Left1",   "Left 1", "a",                     "abc"                      },
                    new() { "Left2",   "Left 2", "b",                     ""                         }, // No value on this row -> no error. It's questionable if this is desirable, but this is natural for the current implementation.
                    new() { "Left3",   "Left 3", "c",                     "def"                      },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("Left.csv:D2", msg0.LocationUrl);
            StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.Nested", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Error, msg1.Level);
            Assert.AreEqual("Left.csv:D4", msg1.LocationUrl);
            StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.Nested", msg1.Message);
        }

        [Test]
        public async Task UnknownMemberTest_ListMember()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name",   "IntList.NonexistentMember" },
                    new() { "Left1",   "Left 1", "abc"                       },
                    new() { "Left2",   "Left 2", ""                          }, // No value on this row -> no error. It's questionable if this is desirable, but this is natural for the current implementation.
                    new() { "Left3",   "Left 3", "def"                       },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("Left.csv:C2", msg0.LocationUrl);
            StringAssert.Contains("No member 'NonexistentMember' found in List<Int32>", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Error, msg1.Level);
            Assert.AreEqual("Left.csv:C4", msg1.LocationUrl);
            StringAssert.Contains("No member 'NonexistentMember' found in List<Int32>", msg1.Message);
        }

        [Test]
        public async Task UnknownMemberTest_NonScalarNode()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key", "Name",   "NonexistentObject.A", "NonExistentArrayIndexed[0]", "NonExistentArrayVertical[]" },
                    new() { "Left1",   "Left 1", "abc",                 "def",                        "ghi" },
                    new() { "Left2",   "Left 2", "jkl",                 "",                           "" },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(6, report.GetMessageCountForLevel(GameConfigLogLevel.Error));

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("Left.csv:C2", msg0.LocationUrl);
            StringAssert.Contains("No member 'NonexistentObject' found in GameConfigPipelineTests.LeftInfo", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Error, msg1.Level);
            Assert.AreEqual("Left.csv:D2", msg1.LocationUrl);
            StringAssert.Contains("No member 'NonExistentArrayIndexed' found in GameConfigPipelineTests.LeftInfo", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual(GameConfigLogLevel.Error, msg2.Level);
            Assert.AreEqual("Left.csv:E2", msg2.LocationUrl);
            StringAssert.Contains("No member 'NonExistentArrayVertical' found in GameConfigPipelineTests.LeftInfo", msg2.Message);

            GameConfigBuildMessage msg3 = report.BuildMessages[3];
            Assert.AreEqual(GameConfigLogLevel.Error, msg3.Level);
            Assert.AreEqual("Left.csv:C3", msg3.LocationUrl);
            StringAssert.Contains("No member 'NonexistentObject' found in GameConfigPipelineTests.LeftInfo", msg3.Message);

            GameConfigBuildMessage msg4 = report.BuildMessages[4];
            Assert.AreEqual(GameConfigLogLevel.Error, msg4.Level);
            // \todo Empty collections don't have location info available. Fix this test if that's ever fixed.
            Assert.IsNull(msg4.LocationUrl);
            StringAssert.Contains("No member 'NonExistentArrayIndexed' found in GameConfigPipelineTests.LeftInfo", msg4.Message);

            GameConfigBuildMessage msg5 = report.BuildMessages[5];
            Assert.AreEqual(GameConfigLogLevel.Error, msg5.Level);
            // \todo Empty collections don't have location info available. Fix this test if that's ever fixed.
            Assert.IsNull(msg5.LocationUrl);
            StringAssert.Contains("No member 'NonExistentArrayVertical' found in GameConfigPipelineTests.LeftInfo", msg5.Message);
        }

        /// <summary>
        /// Empty header cells in a non-empty column are not ok .
        /// In contrast, fully-empty columns are ok. (see <see cref="Metaplay.Core.Tests.GameConfigParsingTests.EmptyColumn"/>).
        /// </summary>
        [Test]
        public async Task EmptyHeaderCell()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "",                     "Name",     ""          },
                    new() { "Left1",    "stray content",        "Left 1",   ""          },
                    new() { "Left2",    "more stray content",   "Left 2",   "stray"     },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:B2", msg0.LocationUrl);
            StringAssert.Contains("This column contains nonempty cells, but its header cell is empty", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:D3", msg1.LocationUrl);
            StringAssert.Contains("This column contains nonempty cells, but its header cell is empty", msg1.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_Simple()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "Name",     },
                    new() { "Left1",    "Left 1",               },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:C:C", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'Name'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_ObjectMember()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "Nested.StringMember",  "Nested.StringMember"    },
                    new() { "Left1",    "Left 1",                                                    },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'Nested.StringMember'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_IndexedElement()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[0]", "IntList[0]", },
                    new() { "Left1",    "Left 1",                               },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'IntList[0]'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_IndexedElement_ObjectMember()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[0].Test", "IntList[0].Test", },
                    new() { "Left1",    "Left 1",                                         },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'IntList[0].Test'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_VerticalArray()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[]", "IntList[]",   },
                    new() { "Left1",    "Left 1",                               },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'IntList[]'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_VerticalArray_ObjectMember()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[].Test", "IntList[].Test", },
                    new() { "Left1",    "Left 1",                                       },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'IntList[].Test'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_ScalarVsObject()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "Nested",  "Nested.StringMember"    },
                    new() { "Left1",    "Left 1",                                       },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Conflicting header cells 'Nested' and 'Nested.StringMember'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_VerticalArray_ScalarVsObject()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[]", "IntList[].Test" },
                    new() { "Left1",    "Left 1",                                 },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Conflicting header cells 'IntList[]' and 'IntList[].Test'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_IndexedElement_ScalarVsObject()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[0]", "IntList[0].Test" },
                    new() { "Left1",    "Left 1",                                   },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Conflicting header cells 'IntList[0]' and 'IntList[0].Test'", msg0.Message);
        }

        [Test]
        public async Task ConflictingHeaderNode_Duplicate_And_ScalarVsObject()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "Nested",   "Nested",  "Nested.StringMember"    },
                    new() { "Left1",    "Left 1",                                                   },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:D:D", msg0.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'Nested'", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:E:E", msg1.LocationUrl);
            StringAssert.Contains("Conflicting header cells 'Nested' and 'Nested.StringMember'", msg1.Message);
        }

        [Test]
        public async Task DuplicateScalarColumnInCollectionMultiRepresentation()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[]",   "IntList[0]",  "IntList", "IntList"  },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:F:F", msg0.LocationUrl);
            StringAssert.Contains("Duplicate columns specifying collection 'IntList'", msg0.Message);
        }

        [Test]
        public async Task MultipleCollectionRepresentationsInSingleRow()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",     "IntList[]",   "IntList[0]",  "IntList"     },
                    new() { "Left1",    "Left 1",   "1",           "2",           ""            },
                    new() { "Left2",    "Left 2",   "1",           "",            "3"           },
                    new() { "Left3",    "Left 3",   "",            "2",           "3"           },
                    new() { "Left4",    "Left 4",   "1",           "2",           "3"           },
                    new() { "",         "",         "2",           "",            ""            },
                    new() { "",         "",         "3",           "",            ""            },
                    new() { "Left5",    "Left 5",   "1",           "2",           "3"           },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(5, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:C2:D2", msg0.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:C3:E3", msg1.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual("Left.csv:D4:E4", msg2.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg2.Message);

            GameConfigBuildMessage msg3 = report.BuildMessages[3];
            Assert.AreEqual("Left.csv:C5:E7", msg3.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg3.Message);

            GameConfigBuildMessage msg4 = report.BuildMessages[4];
            Assert.AreEqual("Left.csv:C8:E8", msg4.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg4.Message);
        }

        [Test]
        public async Task ExtraneousContentInScalar()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "IntList[]" },
                    new() { "Left1",    "1 asdf",   },
                    new() { "Left2",    "2, 3",     },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(2, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:B2", msg0.LocationUrl);
            // \note Annoying space after '1' here due to how the lexer's offset is used in GameConfigOutputItemParser.ParseScalar.
            StringAssert.Contains("Extraneous content appears after '1 ' of type Int32: 'asdf'", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:B3", msg1.LocationUrl);
            StringAssert.Contains("Extraneous content appears after '2' of type Int32: ', 3'", msg1.Message);
        }

        [Test]
        public async Task ExtraneousCellForScalarInMultiRowItem()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",                 "IntList[]",    "IntList[0]",   "IntList"   },
                    new() { "Left1",    "left 1",               "1",            "",             ""          },
                    new() { "",         "left 1 extra",         "2",            "",             ""          },
                    new() { "",         "",                     "3",            "",             ""          },
                    new() { "Left2",    "left 2",               "1",            "",             ""          },
                    new() { "",         "",                     "2",            "",             ""          },
                    new() { "",         "left 2 extra",         "3",            "",             ""          },
                    new() { "Left3",    "",                     "",             "",             ""          },
                    new() { "",         "",                     "",             "",             ""          },
                    new() { "",         "left 3 extra",         "",             "",             ""          },
                    new() { "Left4",    "",                     "",             "",             ""          },
                    new() { "",         "left 4 extra",         "",             "",             ""          },
                    new() { "",         "left 4 more extra",    "",             "",             ""          },
                    new() { "Left5",    "left 5",               "",             "",             ""          },
                    new() { "",         "",                     "",             "",             ""          },
                    new() { "",         "",                     "",             "1",            ""          },
                    new() { "Left6",    "left 6",               "",             "",             ""          },
                    new() { "",         "",                     "",             "",             ""          },
                    new() { "",         "",                     "",             "",             "1,2,3"     },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(7, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:B3", msg0.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'Name'.", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual("Left.csv:B7", msg1.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'Name'.", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual("Left.csv:B10", msg2.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'Name'.", msg2.Message);

            GameConfigBuildMessage msg3 = report.BuildMessages[3];
            Assert.AreEqual("Left.csv:B12", msg3.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'Name'.", msg3.Message);

            GameConfigBuildMessage msg4 = report.BuildMessages[4];
            Assert.AreEqual("Left.csv:B13", msg4.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'Name'.", msg4.Message);

            GameConfigBuildMessage msg5 = report.BuildMessages[5];
            Assert.AreEqual("Left.csv:D16", msg5.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'IntList[0]'.", msg5.Message);

            GameConfigBuildMessage msg6 = report.BuildMessages[6];
            Assert.AreEqual("Left.csv:E19", msg6.LocationUrl);
            StringAssert.Contains("Extraneous content in column 'IntList'.", msg6.Message);
        }

        [Test]
        public async Task MultipleLinearCollectionsInSamePath()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name",                 "SomeList[].OtherList[]"    },
                    new() { "Left1",    "left 1",               "1",                        },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("Left.csv:C1", msg0.LocationUrl);
            StringAssert.Contains("At most 1 non-indexed (vertical) collection can be specified in the same header cell, but 'SomeList[].OtherList[]' contains 2: 'SomeList[]', 'OtherList[]'. All except 1 must have explicit indexes.", msg0.Message);
        }

        [Test]
        public async Task ItemTransformLogging()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("Left", new List<string>[]
                {
                    new() { "Id #key",  "Name"   },
                    new() { "Left1",    "left 1" },
                }),
                ParseSpreadsheet("Right", new List<string>[]
                {
                    new() { "Id #key", "Name" },
                    new() { "Right1",  "Nothing" },
                    new() { "Right2",  "EmitWarning" },
                    new() { "Right3",  "EmitInformation" },
                })
            );

            Assert.IsNotNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Warning, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Warning));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Warning, msg0.Level);
            Assert.AreEqual("Right.csv:3:3", msg0.LocationUrl);
            StringAssert.Contains("Item requested to emit a warning message during item transformation", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Information, msg1.Level);
            Assert.AreEqual("Right.csv:4:4", msg1.LocationUrl);
            StringAssert.Contains("Item requested to emit an information message during item transformation", msg1.Message);
        }

        [Test]
        public async Task KeyValue_MissingMemberPathHeader()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Value" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:1:1", msg0.LocationUrl);
            StringAssert.Contains("Header row is missing 'Member'", msg0.Message);
        }

        [Test]
        public async Task KeyValue_MissingMemberValueHeader()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:1:1", msg0.LocationUrl);
            StringAssert.Contains("Header row is missing 'Value'", msg0.Message);
        }

        [Test]
        public async Task KeyValue_DuplicateHeaderCells()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant", "Member", "Value", "/Variant", "Member", "Value" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(3, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:D1", msg0.LocationUrl);
            StringAssert.Contains("Duplicate '/Variant' header cell", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Error, msg1.Level);
            Assert.AreEqual("TestKeyValue.csv:E1", msg1.LocationUrl);
            StringAssert.Contains("Duplicate 'Member' header cell", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual(GameConfigLogLevel.Error, msg2.Level);
            Assert.AreEqual("TestKeyValue.csv:F1", msg2.LocationUrl);
            StringAssert.Contains("Duplicate 'Value' header cell", msg2.Message);
        }

        [Test]
        public async Task KeyValue_EmptyHeaderCell()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",  "",      "Value" },
                    new() { "Int",      "asdf", "123" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:B2", msg0.LocationUrl);
            StringAssert.Contains("This column contains nonempty cells, but its header cell is empty, which is not supported.", msg0.Message);
        }

        [Test]
        public async Task KeyValue_InvalidHeaderCell()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",   "Invalid",  "Value" },
                    new() { "Int",      "",         "123" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:B1", msg0.LocationUrl);
            StringAssert.Contains("Unknown cell 'Invalid' in header row.", msg0.Message);
        }

        [Test]
        public async Task KeyValue_MissingMemberPath()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",   "Value" },
                    new() { "Int",      "123" },
                    new() { "",         "456" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:B3", msg0.LocationUrl);
            StringAssert.Contains("This row contains nonempty cells, but does not specify the member name.", msg0.Message);
        }

        [Test]
        public async Task KeyValue_MissingMemberPathAndVariantId()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant", "Member",   "Value"     },
                    new() { "",         "Int",      "123"       },
                    new() { "A/X",      "",         "456"       },
                    new() { "",         "",         "789"       },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:C4", msg0.LocationUrl);
            StringAssert.Contains("This row contains nonempty cells, but does not specify the member name.", msg0.Message);
        }

        [Test]
        public async Task KeyValue_HeaderErrors()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",               "Value" },
                    new() { "Int",                  "" },
                    new() { "Int",                  "" },
                    new() { "Nested.StringMember",  "" },
                    new() { "Nested.StringMember",  "" },
                    new() { "IntList[]",            "" },
                    new() { "IntList[]",            "" },
                    new() { "StringList[0]",        "" },
                    new() { "StringList[0]",        "" },
                    new() { "CoordList[]",          "" },
                    new() { "CoordList",            "" },
                    new() { "CoordList",            "" },
                    new() { "Test[].Other[]",       "" },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(6, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual(GameConfigLogLevel.Error, msg0.Level);
            Assert.AreEqual("TestKeyValue.csv:A13", msg0.LocationUrl);
            StringAssert.Contains("At most 1 non-indexed (horizontal) collection can be specified in the same header cell, but 'Test[].Other[]' contains 2: 'Test[]', 'Other[]'. All except 1 must have explicit indexes.", msg0.Message);

            GameConfigBuildMessage msg1 = report.BuildMessages[1];
            Assert.AreEqual(GameConfigLogLevel.Error, msg1.Level);
            Assert.AreEqual("TestKeyValue.csv:3:3", msg1.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'Int'", msg1.Message);

            GameConfigBuildMessage msg2 = report.BuildMessages[2];
            Assert.AreEqual(GameConfigLogLevel.Error, msg2.Level);
            Assert.AreEqual("TestKeyValue.csv:5:5", msg2.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'Nested.StringMember'", msg2.Message);

            GameConfigBuildMessage msg3 = report.BuildMessages[3];
            Assert.AreEqual(GameConfigLogLevel.Error, msg3.Level);
            Assert.AreEqual("TestKeyValue.csv:7:7", msg3.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'IntList[]'", msg3.Message);

            GameConfigBuildMessage msg4 = report.BuildMessages[4];
            Assert.AreEqual(GameConfigLogLevel.Error, msg4.Level);
            Assert.AreEqual("TestKeyValue.csv:9:9", msg4.LocationUrl);
            StringAssert.Contains("Duplicate header cell for 'StringList[0]'", msg4.Message);

            GameConfigBuildMessage msg5 = report.BuildMessages[5];
            Assert.AreEqual(GameConfigLogLevel.Error, msg5.Level);
            Assert.AreEqual("TestKeyValue.csv:12:12", msg5.LocationUrl);
            StringAssert.Contains("Duplicate rows specifying collection 'CoordList'", msg5.Message);
        }

        [Test]
        public async Task KeyValue_ExtraneousCellForScalar()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",               "Value"     },
                    new() { "Int",                  "1", "2"    },
                    new() { "IntList[]",            ""          },
                    new() { "IntList",              "3", "4"    },
                    new() { "Test[0]",              "5", "6"    },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(3, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            {
                GameConfigBuildMessage msg = report.BuildMessages[0];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("TestKeyValue.csv:C2", msg.LocationUrl);
                StringAssert.Contains("Extraneous content in row 'Int'", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[1];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("TestKeyValue.csv:C4", msg.LocationUrl);
                StringAssert.Contains("Extraneous content in row 'IntList'", msg.Message);
            }
            {
                GameConfigBuildMessage msg = report.BuildMessages[2];
                Assert.AreEqual(GameConfigLogLevel.Error, msg.Level);
                Assert.AreEqual("TestKeyValue.csv:C5", msg.LocationUrl);
                StringAssert.Contains("Extraneous content in row 'Test[0]'", msg.Message);
            }
        }

        [Test]
        public async Task KeyValue_MultipleCollectionRepresentations()
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",       "Value"             },
                    new() { "IntList[]",    "1",    "2",    "3" },
                    new() { "IntList[0]",   "4"                 },
                    new() { "IntList[1]",   "5"                 },
                    new() { "IntList",      "6, 7, 8"           },
                })
            );

            Assert.IsNull(gameConfig);
            Assert.AreEqual(GameConfigLogLevel.Error, report.HighestMessageLevel);
            Assert.AreEqual(1, report.GetMessageCountForLevel(GameConfigLogLevel.Error));
            Assert.AreEqual(0, report.ValidationMessages.Length);

            GameConfigBuildMessage msg0 = report.BuildMessages[0];
            Assert.AreEqual("TestKeyValue.csv:B2:D5", msg0.LocationUrl);
            StringAssert.Contains("Collection IntList[] specified using multiple representations", msg0.Message);
        }

        [TestCase(null)] // \note Specifically tests that default handling is Error
        [TestCase(UnknownConfigMemberHandling.Error)]
        [TestCase(UnknownConfigMemberHandling.Warning)]
        [TestCase(UnknownConfigMemberHandling.Ignore)]
        public async Task KeyValue_UnknownMemberTest_Simple(UnknownConfigMemberHandling? unknownMemberHandlingOverride)
        {
            (TestSharedGameConfig gameConfig, GameConfigBuildReport report) = await BuildSharedGameConfigAsync(
                new TestConfigBuildIntegration
                {
                    UnknownMemberHandlingOverride = unknownMemberHandlingOverride
                },
                ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",                   "Value",    },
                    new() { "Int",                      "123",      },
                    new() { "NonexistentMember",        "abc",      },
                    new() { "OtherNonexistentMember",   "",         }, // No value given -> no error. It's questionable if this is desirable, but this is natural for the current implementation.
                    new() { "Nested.NonexistentMember", "def",      },
                    new() { "Nested.StringMember",      "ghi",      },
                })
            );

            // \note When unknownMemberHandlingOverride is not given, we're using default, which is Error.
            UnknownConfigMemberHandling unknownMemberHandling = unknownMemberHandlingOverride ?? UnknownConfigMemberHandling.Error;

            // Check resulting game config.
            // - On Warning and Ignore, build should succeed despite possible build messages.
            // - On Error, build should not succeed.
            if (unknownMemberHandling == UnknownConfigMemberHandling.Warning || unknownMemberHandling == UnknownConfigMemberHandling.Ignore)
            {
                Assert.AreEqual(123, gameConfig.TestKeyValue.Int);
                Assert.AreEqual("ghi", gameConfig.TestKeyValue.Nested.StringMember);
            }
            else
            {
                Assert.IsNull(gameConfig);
            }

            // Check build messages.
            // - On Ignore, should at most have Information messages (which we don't check here as they're unrelated).
            // - On Error and Warning, should have Error or Warning messages respectively.
            if (unknownMemberHandling == UnknownConfigMemberHandling.Ignore)
            {
                Assert.LessOrEqual(report.HighestMessageLevel, GameConfigLogLevel.Information);
            }
            else
            {
                GameConfigLogLevel expectedLogLevel;

                if (unknownMemberHandling == UnknownConfigMemberHandling.Error)
                    expectedLogLevel = GameConfigLogLevel.Error;
                else
                    expectedLogLevel = GameConfigLogLevel.Warning;

                Assert.AreEqual(expectedLogLevel, report.HighestMessageLevel);
                Assert.AreEqual(2, report.GetMessageCountForLevel(expectedLogLevel));

                GameConfigBuildMessage msg0 = report.BuildMessages[0];
                Assert.AreEqual(expectedLogLevel, msg0.Level);
                Assert.AreEqual("TestKeyValue.csv:B3", msg0.LocationUrl);
                StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.TestKeyValue", msg0.Message);

                GameConfigBuildMessage msg1 = report.BuildMessages[1];
                Assert.AreEqual(expectedLogLevel, msg1.Level);
                Assert.AreEqual("TestKeyValue.csv:B5", msg1.LocationUrl);
                StringAssert.Contains("No member 'NonexistentMember' found in GameConfigPipelineTests.Nested", msg1.Message);
            }
        }
    }
}

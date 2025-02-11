// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Metaplay.Core.Tests
{
    [TestFixture]
    public class GameConfigParsingTests
    {
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
        public class TestId : StringId<TestId> { }

        [MetaSerializable]
        public class DeeplyNestedInfo
        {
            [MetaMember(1)] public int      Int;
            [MetaMember(2)] public string   Str;

            public override bool Equals(object obj)
            {
                if (obj is not DeeplyNestedInfo other)
                    return false;

                return Equals(Int, other.Int)
                    && Equals(Str, other.Str);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class NestedInfo
        {
            [MetaMember(1)] public int              Amount  = -1;
            [MetaMember(2)] public string           Type;
            [MetaMember(3)] public IntVector2       Coords;
            [MetaMember(4)] public DeeplyNestedInfo Deeply;
            [MetaMember(5)] public int[]            IntArray;
            [MetaMember(6)] public DeeplyNestedInfo[] DeeplyArray;

            public override bool Equals(object obj)
            {
                if (obj is not NestedInfo other)
                    return false;

                return Equals(Amount, other.Amount)
                    && Equals(Type, other.Type)
                    && Equals(Coords, other.Coords)
                    && Equals(Deeply, other.Deeply)
                    && Helper.SequenceEquals(IntArray, other.IntArray)
                    && Helper.SequenceEquals(DeeplyArray, other.DeeplyArray);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorTestInfoConstructorDuplicateParameters : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId                                                    Id      { get; set; }
            [MetaMember(2)] public string                                                    Name    { get; set; }
            [MetaMember(3)] public string                                                    name   { get; set; }

            public ConstructorTestInfoConstructorDuplicateParameters()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorTestInfoConstructorDuplicateParameters(TestId id, string name = "default", string Name = "")
            {
                Id      = id;
                Name    = name;
            }

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorTestInfoConstructorMemberTypeMismatch : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId                                              Id      { get; set; }
            [MetaMember(2)] public string                                              Name    { get; set; }
            [MetaMember(3)] public MetaRef<ConstructorTestInfoConstructorMemberTypeMismatch> MetaRef { get; set; }
            [MetaMember(4)] public string                                              Index   { get; set; }

            public ConstructorTestInfoConstructorMemberTypeMismatch()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorTestInfoConstructorMemberTypeMismatch(TestId id, MetaRef<ConstructorTestInfoConstructorMemberTypeMismatch> metaRef, string name = "default", int index = 1)
            {
                Id      = id;
                Name    = name;
                MetaRef = metaRef;
                Index   = index.ToString(CultureInfo.InvariantCulture);
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorTestInfoConstructorMemberTypeMismatch other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(Name, other.Name)
                    && Equals(MetaRef, other.MetaRef)
                    && Equals(Index, other.Index);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        /// <summary>
        /// Members do not directly match to parameters (neither name nor type);
        /// that should be OK since constructor-based looks only at the constructor,
        /// not actual members.
        /// </summary>
        [MetaSerializable]
        public class ConstructorTestInfoConstructorMemberMismatch : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId   Id          { get; set; }
            [MetaMember(2)] public string   SomeMember  { get; set; }

            public ConstructorTestInfoConstructorMemberMismatch()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorTestInfoConstructorMemberMismatch(TestId id, int someParameter)
            {
                Id = id;
                SomeMember = someParameter.ToString(CultureInfo.InvariantCulture);
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorTestInfoConstructorMemberMismatch other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(SomeMember, other.SomeMember);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId                       Id      { get; set; }
            [MetaMember(2)] public string                       Name    { get; set; }
            [MetaMember(3)] public MetaRef<ConstructorTestInfo> MetaRef { get; set; }
            [MetaMember(4)] public int                          Index   { get; set; }
            [MetaMember(5)] public string                       SomeString { get; set; }

            public ConstructorTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorTestInfo(TestId id, MetaRef<ConstructorTestInfo> metaRef, string name = "default", int index = 1, string someString = null)
            {
                Id      = id;
                Name    = name;
                MetaRef = metaRef;
                Index   = index;
                SomeString = someString;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(Name, other.Name)
                    && Equals(MetaRef, other.MetaRef)
                    && Equals(Index, other.Index)
                    && Equals(SomeString, other.SomeString);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorOptionalNestedTypeParameterTestInfo : IGameConfigData<TestId>
        {
            [MetaSerializable]
            public struct Test
            {
                [MetaMember(1)]
                public int t;

                [MetaGameConfigBuildConstructor]
                public Test(int test = 100)
                {
                    t = test;
                }
            }

            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId Id          { get; set; }
            [MetaMember(2)] public Test   TestStruct  { get; set; }
            [MetaMember(3)] public Test   TestStruct2 { get; set; }

            public ConstructorOptionalNestedTypeParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalNestedTypeParameterTestInfo(TestId id, Test testStruct = default, Test testStruct2 = default)
            {
                Id          = id;
                TestStruct  = testStruct;
                TestStruct2 = testStruct2;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalNestedTypeParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(TestStruct.t, other.TestStruct.t)
                    && Equals(TestStruct2.t, other.TestStruct2.t);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorOptionalStructParameterTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId       Id      { get; set; }
            [MetaMember(2)] public IntVector2   VecA    { get; set; }
            [MetaMember(3)] public IntVector2   VecB    { get; set; }

            public ConstructorOptionalStructParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalStructParameterTestInfo(TestId id, IntVector2 vecA = new IntVector2(), IntVector2 vecB = new IntVector2())
            {
                Id      = id;
                VecA    = vecA;
                VecB    = vecB;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalStructParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(VecA, other.VecA)
                    && Equals(VecB, other.VecB);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public enum TestEnum
        {
            A = 1,
            B = 0,
            C = 2,
        }

        [MetaSerializable]
        public class ConstructorOptionalEnumParameterTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId       Id      { get; set; }
            [MetaMember(2)] public TestEnum     EnumX   { get; set; }
            [MetaMember(3)] public TestEnum     EnumY   { get; set; }
            [MetaMember(4)] public TestEnum     EnumZ   { get; set; }

            public ConstructorOptionalEnumParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalEnumParameterTestInfo(TestId id, TestEnum enumX = TestEnum.A, TestEnum enumY = default, TestEnum enumZ = TestEnum.C)
            {
                Id      = id;
                EnumX   = enumX;
                EnumY   = enumY;
                EnumZ   = enumZ;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalEnumParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(EnumX, other.EnumX)
                    && Equals(EnumY, other.EnumY)
                    && Equals(EnumZ, other.EnumZ);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorOptionalTupleParameterTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId      Id { get; set; }
            [MetaMember(2)] public (bool, int) X  { get; set; } = (true, 123);

            public ConstructorOptionalTupleParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalTupleParameterTestInfo(TestId id, (bool, int) x = default)
            {
                Id = id;
                X  = x;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalTupleParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(X, other.X);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorOptionalNullableParameterTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId Id { get; set; }
            [MetaMember(2)] public int?   X  { get; set; } = 123;
            [MetaMember(3)] public string Y  { get; set; } = "test";
            [MetaMember(4)] public int?   Z  { get; set; } = 123;

            [MetaMember(5)] public TestEnum? NullEnum    { get; set; } = GameConfigParsingTests.TestEnum.C;
            [MetaMember(6)] public TestEnum? DefaultEnum { get; set; } = GameConfigParsingTests.TestEnum.C;

            public ConstructorOptionalNullableParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalNullableParameterTestInfo(TestId id, int? x = null, string y = null, int? z = 123, TestEnum? nullEnum = null, TestEnum? defaultEnum = TestEnum.B)
            {
                Id          = id;
                X           = x;
                Y           = y;
                Z           = z;
                NullEnum    = nullEnum;
                DefaultEnum = defaultEnum;
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalNullableParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(X, other.X)
                    && Equals(Y, other.Y)
                    && Equals(Z, other.Z)
                    && Equals(NullEnum, other.NullEnum)
                    && Equals(DefaultEnum, other.DefaultEnum);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class ConstructorOptionalDecimalParameterTestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId Id               { get; set; }
            [MetaMember(2)] public string XString          { get; set; }
            [MetaMember(3)] public string YString          { get; set; }
            [MetaMember(4)] public string NullDecString    { get; set; }
            [MetaMember(5)] public string DefaultDecString { get; set; }

            public ConstructorOptionalDecimalParameterTestInfo()
            {

            }

            [MetaGameConfigBuildConstructor]
            public ConstructorOptionalDecimalParameterTestInfo(TestId id, decimal x = 123m, decimal y = default, decimal? nullDec = null, decimal? defaultDec = 100.123456m)
            {
                Id               = id;
                XString          = x.ToString(CultureInfo.InvariantCulture);
                YString          = y.ToString(CultureInfo.InvariantCulture);
                NullDecString    = nullDec != null ? Util.ObjectToStringInvariant(nullDec) : null;
                DefaultDecString = Util.ObjectToStringInvariant(defaultDec);
            }

            public override bool Equals(object obj)
            {
                if (obj is not ConstructorOptionalDecimalParameterTestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(XString, other.XString)
                    && Equals(YString, other.YString)
                    && Equals(NullDecString, other.NullDecString)
                    && Equals(DefaultDecString, other.DefaultDecString);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class TestInfo : IGameConfigData<TestId>
        {
            public TestId ConfigKey => Id;

            [MetaMember(1)] public TestId                           Id          { get; set; }
            [MetaMember(2)] public string                           Name        { get; set; }
            [MetaMember(3)] public MetaRef<TestInfo>                Ref         { get; set; }
            [MetaMember(4)] public List<int>                        IntList     { get; set; }
            [MetaMember(5)] public int[]                            IntArray    { get; set; }
            [MetaMember(6)] public NestedInfo                       Nested      { get; set; }
            [MetaMember(7)] public List<NestedInfo>                 NestedList  { get; set; }
            [MetaMember(8)] public List<IntVector2>                 CoordList   { get; set; }
            [MetaMember(9)] public List<string>                     StringList  { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not TestInfo other)
                    return false;

                return Equals(Id, other.Id)
                    && Equals(Name, other.Name)
                    && Equals(Ref, other.Ref)
                    && Helper.SequenceEquals(IntList, other.IntList)
                    && Helper.SequenceEquals(IntArray, other.IntArray)
                    && Equals(Nested, other.Nested)
                    && Helper.SequenceEquals(NestedList, other.NestedList)
                    && Helper.SequenceEquals(CoordList, other.CoordList)
                    && Helper.SequenceEquals(StringList, other.StringList);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        [MetaSerializable]
        public class TestKeyValue : GameConfigKeyValue
        {
            [MetaMember(1)] public TestId           TestId;
            [MetaMember(2)] public int              Int0;
            [MetaMember(3)] public int              Int1;
            [MetaMember(5)] public string           Str0;
            [MetaMember(6)] public string           Str1;
            [MetaMember(7)] public string           Str2;
        }

        [MetaSerializable]
        public class TestKeyValueWithConstructor : GameConfigKeyValue
        {
            [MetaMember(1)] public TestId           TestId { get; }
            [MetaMember(2)] public int              Int0 { get; }
            [MetaMember(3)] public int              Int1 { get; }
            [MetaMember(5)] public string           Str0 { get; }
            [MetaMember(6)] public string           Str1 { get; }
            [MetaMember(7)] public string           Str2 { get; }

            TestKeyValueWithConstructor()
            {
                throw new Exception("should not be used");
            }

            [MetaDeserializationConstructor]
            [MetaGameConfigBuildConstructor]
            public TestKeyValueWithConstructor(TestId testId, int int0, int int1, string str0, string str1, string str2)
            {
                TestId = testId;
                Int0 = int0;
                Int1 = int1;
                Str0 = str0;
                Str1 = str1;
                Str2 = str2;
            }
        }

        [MetaSerializable]
        public class ComplexTestKeyValue : GameConfigKeyValue
        {
            [MetaMember(1)] public int Int;
            [MetaMember(2)] public NestedInfo Nested;
            [MetaMember(3)] public List<int> IntList;
            [MetaMember(4)] public List<int> IntList2;
            [MetaMember(5)] public List<NestedInfo> NestedList;
            [MetaMember(6)] public List<IntVector2> CoordList;
            [MetaMember(7)] public List<string> StringList;
        }

        [MetaSerializable]
        public class ComplexKeyTestInfo : IGameConfigData<(string, int)>
        {
            public (string, int) ConfigKey => (Type, Level);

            [MetaMember(1)] public string   Type    { get; set; }
            [MetaMember(2)] public int      Level   { get; set; }
            [MetaMember(3)] public string   Name    { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not ComplexKeyTestInfo other)
                    return false;

                return Equals(Type, other.Type)
                    && Equals(Level, other.Level)
                    && Equals(Name, other.Name);
            }

            public override int GetHashCode() => throw new NotImplementedException("do not use");

            // ToString for debugging convenience
            public override string ToString() => PrettyPrint.Compact(this).ToString();
        }

        // \note This is required in order for MetaRef<TestInfo> to be valid.
        public class TestSharedGameConfig : SharedGameConfigBase
        {
            [GameConfigEntry("TestItems")]
            public GameConfigLibrary<TestId, TestInfo> TestItems { get; set; }
            [GameConfigEntry("ConstructorTestInfos")]
            public GameConfigLibrary<TestId, ConstructorTestInfo> ConstructorTestInfos { get; set; }
            [GameConfigEntry("ConstructorTestInfoWithDifferentParameters")]
            public GameConfigLibrary<TestId, ConstructorTestInfoConstructorMemberTypeMismatch> ConstructorTestInfoWithDifferentParametersInfos { get; set; }
        }

        // BASIC PARSING

        // \todo [petri] error case: cell parse errors
        // \todo [petri] error case: non-existent members
        // \todo [petri] error case: missing Id (what should this actually do?)

        [Test]
        public void BasicTest()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Name",                "Ref"      },
                    new() { "Item0",   "Some Name",           "RefA"     },
                    new() { "Item1",   "\tAnother Name '!/ ", "OtherRef" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Name = "Some Name",           Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("RefA"))},
                    new TestInfo { Id = TestId.FromString("Item1"), Name = "\tAnother Name '!/ ", Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("OtherRef")) }
                }, items);
        }

        [Test]
        public void BasicConstructorWithDefaultValueParsingTest()
        {
            ConstructorTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Name",                "MetaRef",  "Index",  "SomeString"    },
                    new() { "Item0",   "Some Name",           "RefA",     "0",      ""              },
                    new() { "Item1",   "\tAnother Name '!/ ", "OtherRef", "",       "abc"           },
                }));

            Assert.AreEqual(
                new ConstructorTestInfo[]
                {
                    new ConstructorTestInfo { Id = TestId.FromString("Item0"), Name = "Some Name",             MetaRef = MetaRef<ConstructorTestInfo>.FromKey(TestId.FromString("RefA")),     Index = 0, SomeString = null },
                    new ConstructorTestInfo { Id = TestId.FromString("Item1"), Name = "\tAnother Name '!/ ",   MetaRef = MetaRef<ConstructorTestInfo>.FromKey(TestId.FromString("OtherRef")), Index = 1, SomeString = "abc" }
                }, items);
        }

        [Test]
        public void ConstructorParseValueAsParameterType()
        {
            ConstructorTestInfoConstructorMemberTypeMismatch[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfoConstructorMemberTypeMismatch>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Name",                "MetaRef",  "Index"      },
                    new() { "Item0",   "Some Name",           "RefA",     "0"  },
                    new() { "Item1",   "\tAnother Name '!/ ", "OtherRef" },
                }));

            Assert.AreEqual(
                new ConstructorTestInfoConstructorMemberTypeMismatch[]
                {
                    new ConstructorTestInfoConstructorMemberTypeMismatch { Id = TestId.FromString("Item0"), Name = "Some Name",             MetaRef = MetaRef<ConstructorTestInfoConstructorMemberTypeMismatch>.FromKey(TestId.FromString("RefA")),     Index = "0"},
                    new ConstructorTestInfoConstructorMemberTypeMismatch { Id = TestId.FromString("Item1"), Name = "\tAnother Name '!/ ",   MetaRef = MetaRef<ConstructorTestInfoConstructorMemberTypeMismatch>.FromKey(TestId.FromString("OtherRef")), Index = "1"}
                }, items);
        }

        [Test]
        public void ConstructorParseAccordingToParameter()
        {
            ConstructorTestInfoConstructorMemberMismatch[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfoConstructorMemberMismatch>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "SomeParameter", },
                    new() { "Item0",   "123",           },
                    new() { "Item1",   "456",           },
                }));

            Assert.AreEqual(
                new ConstructorTestInfoConstructorMemberMismatch[]
                {
                    new ConstructorTestInfoConstructorMemberMismatch { Id = TestId.FromString("Item0"), SomeMember = "123" },
                    new ConstructorTestInfoConstructorMemberMismatch { Id = TestId.FromString("Item1"), SomeMember = "456" }
                }, items);
        }

        [Test]
        public void ConstructorTooManyColumns()
        {
            Assert.That(
                () =>
                {
                    ConstructorTestInfoConstructorMemberTypeMismatch[] _ = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfoConstructorMemberTypeMismatch>(
                        new GameConfigParseLibraryPipelineConfig(),
                        GameConfigTestHelper.ParseSpreadsheet(
                            "TestItems",
                            new List<string>[]
                            {
                                new() { "Id #key", "Name",                "MetaRef",  "Index", "Random" },
                                new() { "Item0"  , "Some Name",           "RefA",     "0",     "1"      },
                                new() { "Item1"  , "\tAnother Name '!/ ", "OtherRef", "1"               },
                            }));
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Got 1 errors and 0 warnings while parsing the spreadsheet data:\nError: No member 'Random' found in GameConfigParsingTests.ConstructorTestInfoConstructorMemberTypeMismatch. If the member is meant to be ignored, prefix it with two slashes: // Random\n  Source: Item 'Item0' in file TestItems\n  Link: TestItems:A2:E2"));
        }

        [Test]
        public void ConstructorDuplicateParameters()
        {
            Assert.That(
                () =>
                {
                    ConstructorTestInfoConstructorDuplicateParameters[] _ = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfoConstructorDuplicateParameters>(
                        new GameConfigParseLibraryPipelineConfig(),
                        GameConfigTestHelper.ParseSpreadsheet(
                            "TestItems",
                            new List<string>[]
                            {
                                new() { "Id #key", "Name",      },
                                new() { "Item0"  , "Some Name", },
                            }));
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Got 1 errors and 0 warnings while parsing the spreadsheet data:\nError: Constructor in 'Metaplay.Core.Tests.GameConfigParsingTests+ConstructorTestInfoConstructorDuplicateParameters' has duplicate case-insensitive parameters: 'name', this is not supported for constructor based deserialization.\n  Source: Item 'Item0' in file TestItems\n  Link: TestItems:A2:B2"));
        }

        [Test]
        public void ConstructorWithDuplicateCaseInsensitiveColumns()
        {
            Assert.That(
                () =>
                {
                    ConstructorTestInfo[] _ = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfo>(
                        new GameConfigParseLibraryPipelineConfig(),
                        GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                        {
                            new() { "Id #key", "Name",                "NAME"                    },
                            new() { "Item0",   "Some Name",           "Some Name Duplicate",    },
                        }));
                },
                Throws.Exception.With.Message.StartsWith("Got 1 errors and 0 warnings while parsing the spreadsheet data:\nError: Source data has multiple columns with the same case-insensitive "));
        }

        [Test]
        public void ConstructorMissingColumns()
        {
            Assert.That(
                () =>
                {
                    ConstructorTestInfoConstructorMemberTypeMismatch[] _ = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfoConstructorMemberTypeMismatch>(
                        new GameConfigParseLibraryPipelineConfig(),
                        GameConfigTestHelper.ParseSpreadsheet(
                            "TestItems",
                            new List<string>[]
                            {
                                new() { "Id #key", "Name",                "Index",},
                                new() { "Item0"  , "Some Name",           "0",    },
                            }));
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Got 1 errors and 0 warnings while parsing the spreadsheet data:\nError: Expected value for 'metaRef', but found none, please ensure that parameters exist for all values, and that optional values have a default value in the constructor.\n  Source: Item 'Item0' in file TestItems\n  Link: TestItems:A2:C2"));
        }

        [Test]
        public void ConstructorNestedTypeParameter()
        {
            ConstructorOptionalNestedTypeParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalNestedTypeParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "TestStruct", "TestStruct2.test"   },
                    new() { "Item0",   ""          , "200" },
                }));

            Assert.AreEqual(
                new ConstructorOptionalNestedTypeParameterTestInfo[]
                {
                    new ConstructorOptionalNestedTypeParameterTestInfo { Id = TestId.FromString("Item0"), TestStruct = default, TestStruct2 = new ConstructorOptionalNestedTypeParameterTestInfo.Test(200) },
                }, items);
        }

        [Test]
        public void ConstructorOptionalStructParameter()
        {
            ConstructorOptionalStructParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalStructParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "VecA",      "VecB.X",    "VecB.Y"   },
                    new() { "Item0",   "(1, 2)",    "",         "",         },
                    new() { "Item1",   "",          "3",        "4",        },
                    new() { "Item2",   "",          "",         "",         },
                }));

            Assert.AreEqual(
                new ConstructorOptionalStructParameterTestInfo[]
                {
                    new ConstructorOptionalStructParameterTestInfo { Id = TestId.FromString("Item0"), VecA = new IntVector2(1, 2),  VecB = new IntVector2() },
                    new ConstructorOptionalStructParameterTestInfo { Id = TestId.FromString("Item1"), VecA = new IntVector2(),      VecB = new IntVector2(3, 4) },
                    new ConstructorOptionalStructParameterTestInfo { Id = TestId.FromString("Item2"), VecA = new IntVector2(),      VecB = new IntVector2() },
                }, items);
        }

        [Test]
        public void ConstructorOptionalEnumParameter()
        {
            ConstructorOptionalEnumParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalEnumParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "EnumX",     "EnumY",    "EnumZ"     },
                    new() { "Item0",   "",          "",         "",         },
                    new() { "Item1",   "B",         "C",        "A",        },
                }));

            Assert.AreEqual(
                new ConstructorOptionalEnumParameterTestInfo[]
                {
                    new ConstructorOptionalEnumParameterTestInfo { Id = TestId.FromString("Item0"), EnumX = TestEnum.A, EnumY = TestEnum.B, EnumZ = TestEnum.C },
                    new ConstructorOptionalEnumParameterTestInfo { Id = TestId.FromString("Item1"), EnumX = TestEnum.B, EnumY = TestEnum.C, EnumZ = TestEnum.A },
                }, items);
        }

        [Test]
        public void ConstructorOptionalNullableParameter()
        {
            ConstructorOptionalNullableParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalNullableParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "X",     "Y",  "Z", "NullEnum", "DefaultEnum" },
                    new() { "Item0",   "",      "" ,  "" , ""        , ""            } ,
                }));

            Assert.AreEqual(
                new ConstructorOptionalNullableParameterTestInfo[]
                {
                    new ConstructorOptionalNullableParameterTestInfo
                    {
                        Id = TestId.FromString("Item0"),
                        X  = null,
                        Y  = null,
                        Z  = 123,
                        NullEnum = null,
                        DefaultEnum = TestEnum.B,
                    },
                }, items);
        }

        [Test]
        public void ConstructorOptionalTupleParameter()
        {
            ConstructorOptionalTupleParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalTupleParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "X",},
                    new() { "Item0",   "", },
                }));

            Assert.AreEqual(
                new ConstructorOptionalTupleParameterTestInfo[]
                {
                    new ConstructorOptionalTupleParameterTestInfo { Id = TestId.FromString("Item0"), X = (false, 0) },
                }, items);
        }

        [Test]
        public void ConstructorOptionalDecimalParameter()
        {
            ConstructorOptionalDecimalParameterTestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorOptionalDecimalParameterTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "X",     "Y", "NullDec", "DefaultDec"     },
                    new() { "Item0",   "",      "" , ""       , ""   },
                }));

            Assert.AreEqual(
                new ConstructorOptionalDecimalParameterTestInfo[]
                {
                    new ConstructorOptionalDecimalParameterTestInfo { Id = TestId.FromString("Item0"), XString = "123", YString = "0", NullDecString = null, DefaultDecString = "100.123456"},
                }, items);
        }

        [Test]
        public void ConstructorCantParseParameter()
        {
            Assert.That(
                () =>
                {
                    ConstructorTestInfo[] _ = GameConfigTestHelper.ParseNonVariantItems<TestId, ConstructorTestInfo>(
                        new GameConfigParseLibraryPipelineConfig(),
                        GameConfigTestHelper.ParseSpreadsheet(
                            "TestItems",
                            new List<string>[]
                            {
                                new() { "Id #key", "Name",                "MetaRef",  "Index", },
                                new() { "Item0"  , "Some Name",           "RefA",     "Broken",},
                                new() { "Item1"  , "\tAnother Name '!/ ", "OtherRef", "1"      },
                            }));
                },
                Throws.TypeOf<InvalidOperationException>().With.Message.StartsWith("Got 1 errors and 0 warnings while parsing the spreadsheet data:\nError: Failed to parse 'Index' (of type Int32) in GameConfigParsingTests.ConstructorTestInfo..ctor from string 'Broken'\n  Source: Item 'Item0' in file TestItems\n  Link: TestItems:D2\n  Metaplay.Core.ParseError: Expecting token IntegerLiteral, got token Identifier"));
        }

        [Test]
        public void SlashSlashCommentTest()
        {
            // Test named ('// Name') and anonymous comments ('//') -- '// Name' must not parse into the Name member
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "// Name",             "Ref",      "//"                           },
                    new() { "Item0",   "Some Name",           "RefA",     "This is the first item"       },
                    new() { "Item1",   "\tAnother Name '!/ ", "OtherRef", "Comment for the second item"  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("RefA"))     },
                    new TestInfo { Id = TestId.FromString("Item1"), Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("OtherRef")) }
                }, items);
        }

        [Test]
        public void CommentTagTest()
        {
            // Test named ('#comment:Name') and anonymous comments ('#comment') -- #comment:Name must not parse into the Name member
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "#comment:Name",       "Ref",      "#comment"                     },
                    new() { "Item0",   "Some Name",           "RefA",     "This is the first item"       },
                    new() { "Item1",   "\tAnother Name '!/ ", "OtherRef", "Comment for the second item"  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("RefA"))     },
                    new TestInfo { Id = TestId.FromString("Item1"), Ref = MetaRef<TestInfo>.FromKey(TestId.FromString("OtherRef")) }
                }, items);
        }

        [Test]
        public void EmptyRow()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Name"    },
                    new() {},
                    new() { "Item0",   "Item 0"  },
                    new() { "" },
                    new() { "Item1",   "Item 1"  },
                    new() { "",        ""  },
                    new() { "Item2",   "Item 2"  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Name = "Item 0" },
                    new TestInfo { Id = TestId.FromString("Item1"), Name = "Item 1" },
                    new TestInfo { Id = TestId.FromString("Item2"), Name = "Item 2" },
                }, items);
        }

        [Test]
        public void BasicCommentRow()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key",          "Name"    },
                    new() { "// comment row",   "asdf"},
                    new() { "Item0",            "Item 0"  },
                    new() { "// comment row",   "asdf"},
                    new() { "Item1",            "Item 1"  },
                    new() { "// comment row",   "asdf"},
                    new() { "Item2",            "Item 2"  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Name = "Item 0" },
                    new TestInfo { Id = TestId.FromString("Item1"), Name = "Item 1" },
                    new TestInfo { Id = TestId.FromString("Item2"), Name = "Item 2" },
                }, items);
        }

        [Test]
        public void CommentRowAndColumn()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key",          "// comment column",    "Name",     "// comment column",    "Nested.Type"   },
                    new() { "// comment row",   "asdf",                 "asdf",     "asdf",                 "asdf"          },
                    new() { "Item0",            "asdf",                 "Item 0",   "asdf",                 "type 0"        },
                    new() { "// comment row",   "asdf",                 "asdf",     "asdf",                 "asdf"          },
                    new() { "Item1",            "asdf",                 "Item 1",   "asdf",                 "type 1"        },
                    new() { "// comment row",   "asdf",                 "asdf",     "asdf",                 "asdf"          },
                    new() { "Item2",            "asdf",                 "Item 2",   "asdf",                 "type 2"        },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Name = "Item 0", Nested = new NestedInfo { Type = "type 0" } },
                    new TestInfo { Id = TestId.FromString("Item1"), Name = "Item 1", Nested = new NestedInfo { Type = "type 1" } },
                    new TestInfo { Id = TestId.FromString("Item2"), Name = "Item 2", Nested = new NestedInfo { Type = "type 2" } },
                }, items);
        }

        /// <summary>
        /// Fully-empty columns are ok.
        /// </summary>
        [Test]
        public void EmptyColumn()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key",  "", "Name"    },
                    new() { "Item0",    "", "Item 0"  },
                    new() { "Item1",    "", "Item 1"  },
                    new() { "Item2",    "", "Item 2"  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Name = "Item 0" },
                    new TestInfo { Id = TestId.FromString("Item1"), Name = "Item 1" },
                    new TestInfo { Id = TestId.FromString("Item2"), Name = "Item 2" },
                }, items);
        }

        // IMPLICIT COLLECTIONS

        [Test]
        public void ImplicitCollections()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntList[0]", "IntArray[]" }, // both indexed and vertical collections must produce an empty array/list
                    new() { "Item0",   "",           ""  },
                    new() { "Item1",   "",           ""  },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int>(), IntArray = Array.Empty<int>() },
                    new TestInfo { Id = TestId.FromString("Item1"), IntList = new List<int>(), IntArray = Array.Empty<int>() }
                }, items);
        }

        [Test]
        public void ImplicitCollectionsInObjects()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Nested.Type", "Nested.IntArray[]"    },
                    new() { "Item0",   "Some",        ""                     }, // Type triggers Nested to be created -> Nested.IntArray should be empty
                    new() { "Item1",   "",            ""                     }, // Empty collection doesn't trigger Nested to get allocated
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Nested = new NestedInfo { Type = "Some", IntArray = Array.Empty<int>() } },
                    new TestInfo { Id = TestId.FromString("Item1"), Nested = null },
                }, items);
        }

        [Test]
        public void InheritVariantImplicitCollections()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant", "Id #key", "IntList[0]", "IntArray[]" },
                    new() { "",         "Item0",   "10",         "20"         },
                    new() { "A/X",      "Item0",   "",           ""           }, // empty collections inherit from baseline
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int>{ 10 }, IntArray = new int[] { 20 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int>{ 10 }, IntArray = new int[] { 20 } }),
                }, items);
        }

        // MULTI-REPRESENTATION COLLECTIONS

        [Test]
        public void CollectionMultiRepresentation()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntArray", "IntArray[]", "IntArray[0]",  "IntArray[1]"   },
                    new() { "Item0",   "",         "",           "",             ""              }, // no representation used
                    new() { "Item1",   "[]",       "",           "",             ""              }, // scalar representation used (empty)
                    new() { "Item2",   "",         "10",         "",             ""              }, // vertical representation used
                    new() { "",        "",         "20",         "",             ""              }, // vertical representation used
                    new() { "Item3",   "",         "",           "30",           "40"            }, // indexed representation used
                    new() { "Item4",   "1,2,3",    "",           "",             ""              }, // scalar representation
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntArray = new int[] { } },
                    new TestInfo { Id = TestId.FromString("Item1"), IntArray = new int[] { } },
                    new TestInfo { Id = TestId.FromString("Item2"), IntArray = new int[] { 10, 20 } },
                    new TestInfo { Id = TestId.FromString("Item3"), IntArray = new int[] { 30, 40 } },
                    new TestInfo { Id = TestId.FromString("Item4"), IntArray = new int[] { 1, 2, 3 } },
                }, items);
        }

        [Test]
        public void CollectionVariantOverrides()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant", "Id #key", "IntArray[]", "IntArray", "/:B/X" },
                    new() { "",         "Item0",   "5",          "",         ""      }, // Item0: row variant A/X overrides array with empty one
                    new() { "A/X",      "",        "",           "[]",       ""      },
                    new() { "",         "Item1",   "1",          "",         "[]"    }, // Item1: column variant B/X overrides array with empty one
                    new() { "",         "",        "2",          "",         ""      },
                    new() { "",         "Item2",   "5",          "",         ""      }, // Item2: row variant A/X but no override
                    new() { "",         "",        "6",          "",         ""      },
                    new() { "A/X",      "",        "",           "",         ""      },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), IntArray = new int[] { 5 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), IntArray = new int[] { } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), IntArray = new int[] { 1, 2 } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), IntArray = new int[] { } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item2"), IntArray = new int[] { 5, 6 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item2"), IntArray = new int[] { 5, 6 } }),
                }, items);
        }

        // NESTED TYPES

        [Test]
        public void NestedTypes()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "Nested.Amount", "Nested.Type", "Nested.Coords", "Nested.Deeply.Int" },
                    new() { "Item0",   "10",            "Foo",         "(5, 1)",        "42" },
                    new() { "Item1",   "",              "",            "(1, 3)" },
                    new() { "Item2",   "" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), Nested = new NestedInfo { Amount = 10, Type = "Foo", Coords = new IntVector2(5, 1), Deeply = new DeeplyNestedInfo { Int = 42 } } },
                    new TestInfo { Id = TestId.FromString("Item1"), Nested = new NestedInfo { Amount = -1, Type = null,  Coords = new IntVector2(1, 3),Deeply = null } },
                    new TestInfo { Id = TestId.FromString("Item2"), Nested = null },
                }, items);
        }

        // SINGLE-CELL ARRAYS

        [Test]
        public void SingleCellArray()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntList", "IntArray"      },
                    new() { "Item0",   "10,20",   "5"             },
                    new() { "Item1",   "50",      "100,101,102"   },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int> { 10, 20 }, IntArray = new int[] { 5 } },
                    new TestInfo { Id = TestId.FromString("Item1"), IntList = new List<int> { 50 },     IntArray = new int[] { 100, 101, 102 } },
                    new TestInfo { Id = TestId.FromString("Item2"), IntList = null,                     IntArray = null },
                }, items);
        }

        // HORIZONTAL ARRAYS

        // \todo [petri] error case: negative index: IntList[-1]
        // \todo [petri] edge case: missing/non-ordered indexes: IntList[2], IntList[0]
        // \todo [petri] nested arrays/indexes (eg, NestedList[0].NestedList[1].Member) -- should this be supported?

        [Test]
        public void HorizontalArray_Simple()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntList[0]", "IntList[1]", "IntArray[0]", "IntArray[1]", "IntArray[2]", },
                    new() { "Item0",   "10",         "20",         "5",           "" },
                    new() { "Item1",   "50",         "",           "100",         "101",         "102" },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int> { 10, 20 }, IntArray = new int[] { 5 } },
                    new TestInfo { Id = TestId.FromString("Item1"), IntList = new List<int> { 50 },     IntArray = new int[] { 100, 101, 102 } },
                    new TestInfo { Id = TestId.FromString("Item2"), IntList = new List<int>(),          IntArray = Array.Empty<int>() },
                }, items);
        }

        [Test]
        public void HorizontalArray_Length1()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "StringList[0]" },
                    new() { "Item0",   "abc  , def" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), StringList = new List<string> { "abc  , def" } },
                }, items);
        }

        [Test]
        public void HorizontalArray_Complex()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "CoordList[0]", "CoordList[1]", "CoordList[2]" },
                    new() { "Item0",   "(5, 2)",       "(4, 4)" },
                    new() { "Item1",   "(9, 9)" },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), CoordList = new List<IntVector2>{ new IntVector2(5, 2), new IntVector2(4, 4) } },
                    new TestInfo { Id = TestId.FromString("Item1"), CoordList = new List<IntVector2>{ new IntVector2(9, 9) } },
                    new TestInfo { Id = TestId.FromString("Item2"), CoordList = new List<IntVector2>() },
                }, items);
        }

        [Test]
        public void HorizontalArray_ComplexMembers()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "NestedList[0].Amount", "NestedList[0].Type", "NestedList[1].Amount", "NestedList[1].Type" },
                    new() { "Item0",   "20",                   "Gold",               "5",                    "Gems"               },
                    new() { "Item1",   "100",                  "Gold",               "",                     "Sword"              },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), NestedList = new List<NestedInfo> { new NestedInfo { Amount = 20, Type = "Gold" }, new NestedInfo { Amount = 5, Type = "Gems" } } },
                    new TestInfo { Id = TestId.FromString("Item1"), NestedList = new List<NestedInfo> { new NestedInfo { Amount = 100, Type = "Gold" }, new NestedInfo { Type = "Sword" } } },
                    new TestInfo { Id = TestId.FromString("Item2"), NestedList = new List<NestedInfo>() },
                }, items);
        }

        [Test]
        public void HorizontalArray_ComplexNestedMembers()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "NestedList[0].Amount", "NestedList[0].Type", "NestedList[0].Deeply.Int", "NestedList[1].Amount", "NestedList[1].Type", "NestedList[1].Deeply.Int" },
                    new() { "Item0",   "20",                   "Gold",               "-10",                      "5",                    "Gems",               "-5" },
                    new() { "Item1",   "100",                  "Gold",               "-20",                      "",                     "Sword" },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), NestedList = new List<NestedInfo> { new NestedInfo { Amount = 20, Type = "Gold", Deeply = new() { Int = -10 } }, new NestedInfo { Amount = 5, Type = "Gems", Deeply = new() { Int = -5 } } } },
                    new TestInfo { Id = TestId.FromString("Item1"), NestedList = new List<NestedInfo> { new NestedInfo { Amount = 100, Type = "Gold", Deeply = new() { Int = -20 } }, new NestedInfo { Type = "Sword" } } },
                    new TestInfo { Id = TestId.FromString("Item2"), NestedList = new List<NestedInfo>() },
                }, items);
        }

        // VERTICAL ARRAYS

        // \todo [petri] error case: conflicting columns: NestedList[0].Type, NestedList[0].Type
        // \todo [petri] error case: empty element followed by non-empty

        [Test]
        public void VerticalArray_Simple()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntList[]", "IntArray[]"  },
                    new() { "Item0",   "10",        "5"           },
                    new() { "",        "20",        ""            },
                    new() { "Item1",   "50",        "100"         },
                    new() { "",        "",          "101"         },
                    new() { "",        "",          "102"         },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int> { 10, 20 }, IntArray = new int[] { 5 } },
                    new TestInfo { Id = TestId.FromString("Item1"), IntList = new List<int> { 50 },     IntArray = new int[] { 100, 101, 102 } },
                    new TestInfo { Id = TestId.FromString("Item2"), IntList = new List<int>(),          IntArray = new int[] { } },
                }, items);
        }

        [Test]
        public void VerticalArray_EmptyAndCommentRows()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "IntList[]", "// Dummy"  },
                    new() { "Item0",   "50",        ""          },
                    new() { "",        "",          ""          },
                    new() { "",        "",          ""          },
                    new() { "Item1",   "",          ""          },
                    new() { "",        "",          ""          },
                    new() { "",        "",          ""          },
                    new() { "Item2",   "10",        ""          },
                    new() { "",        "",          "asdf"      },
                    new() { "",        "30",        ""          },
                    new() { "Item3",   "10",        ""          },
                    new() { "",        "",          ""          },
                    new() { "",        "30",        ""          },
                    new() { "Item4",   "10",        ""          },
                    new() {}, // \note Despite being empty, this row should be preserved as it is part of a multi-row array in Item4.
                    new() { "",        "30",        ""          },
                    new() { "Item5",   "10",        ""          },
                    new() { "//" }, // \note This row should not be preserved as it is a comment row, despite being in the middle of multi-row Item5.
                    new() { "",        "30",        ""          },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), IntList = new List<int> { 50 } },
                    new TestInfo { Id = TestId.FromString("Item1"), IntList = new List<int>() },
                    new TestInfo { Id = TestId.FromString("Item2"), IntList = new List<int> { 10, 0, 30 } },
                    new TestInfo { Id = TestId.FromString("Item3"), IntList = new List<int> { 10, 0, 30 } },
                    new TestInfo { Id = TestId.FromString("Item4"), IntList = new List<int> { 10, 0, 30 } },
                    new TestInfo { Id = TestId.FromString("Item5"), IntList = new List<int> { 10, 30 } },
                }, items);
        }

        [Test]
        public void VerticalArray_ComplexMembers()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "NestedList[].Amount", "NestedList[].Type" },
                    new() { "Item0",   "20",                  "Gold" },
                    new() { "",        "5",                   "Gems" },
                    new() { "Item1",   "100",                 "Gold" },
                    new() { "",        "",                    "Sword" },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo { Id = TestId.FromString("Item0"), NestedList = new List<NestedInfo> { new() { Amount = 20, Type = "Gold" }, new() { Amount = 5, Type = "Gems" } } },
                    new TestInfo { Id = TestId.FromString("Item1"), NestedList = new List<NestedInfo> { new() { Amount = 100, Type = "Gold", }, new() { Type = "Sword" } } },
                    new TestInfo { Id = TestId.FromString("Item2"), NestedList = new List<NestedInfo>() },
                }, items);
        }

        [Test]
        public void VerticalArray_ComplexNestedMembers()
        {
            TestInfo[] items = GameConfigTestHelper.ParseNonVariantItems<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key", "NestedList[].Amount", "NestedList[].Type", "NestedList[].Deeply.Int", "NestedList[].Coords.X", "NestedList[].Coords.Y", "NestedList[].IntArray[0]", "NestedList[].IntArray[1]", "NestedList[].DeeplyArray[0].Int", "NestedList[].DeeplyArray[0].Str", "NestedList[].DeeplyArray[1].Str" },
                    new() { "Item0",   "20",                  "Gold",              "-10",                     "12",                    "34",                    "1",                        "2",                        "123",                             "abc",                             "x",                              },
                    new() { "",        "5",                   "Gems",              "-20",                     "56",                    "78",                    "10",                       "20",                       "456",                             "def",                             "y",                              },
                    new() { "Item1",   "100",                 "Gold",              "",                        "21",                    "",                      "",                         "200",                      "",                                "ghi",                             "z",                              },
                    new() { "",        "",                    "Sword",             "",                        "",                      "90",                    "1000",                     "",                         "321",                             "",                                "w",                              },
                    new() { "Item2" },
                }));

            Assert.AreEqual(
                new TestInfo[]
                {
                    new TestInfo
                    {
                        Id = TestId.FromString("Item0"),
                        NestedList = new List<NestedInfo>
                        {
                            new() { Amount = 20, Type = "Gold", Deeply = new() { Int = -10 }, Coords = new() { X = 12, Y = 34 }, IntArray = new int[] { 1, 2 }, DeeplyArray = new DeeplyNestedInfo[]{ new() { Int = 123, Str = "abc" }, new() { Str = "x" } } },
                            new() { Amount = 5, Type = "Gems", Deeply = new() { Int = -20 }, Coords = new() { X = 56, Y = 78 }, IntArray = new int[] { 10, 20 }, DeeplyArray = new DeeplyNestedInfo[]{ new() { Int = 456, Str = "def" }, new() { Str = "y" } } },
                        }
                    },
                    new TestInfo
                    {
                        Id = TestId.FromString("Item1"),
                        NestedList = new List<NestedInfo>
                        {
                            new() { Amount = 100, Type = "Gold", Coords = new() { X = 21 }, IntArray = new int[] { 0, 200 }, DeeplyArray = new DeeplyNestedInfo[]{ new() { Str = "ghi" }, new() { Str = "z" } } },
                            new() { Type = "Sword", Coords = new() { Y = 90 }, IntArray = new int[] { 1000 }, DeeplyArray = new DeeplyNestedInfo[]{ new() { Int = 321 }, new() { Str = "w" } } },
                        }
                    },
                    new TestInfo { Id = TestId.FromString("Item2"), NestedList = new List<NestedInfo>() },
                }, items);
        }

        #region Variants

        [Test]
        public void Variant_Basic_RowOverride()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",             "Nested.Type"                   },
                    new() { "",             "Item0",        "item 0",           "item 0 nested type"            },
                    new() { "A/X",          "",             "item 0 modified",  ""                              },
                    new() { "",             "Item1",        "item 1",           "item 1 nested type"            },
                    new() { "A/Y",          "Item1",        "item 1 modified",  "item 1 nested type modified"   },
                    new() { "A/Y",          "Item0",        "",                 "item 0 nested type modified"   },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type modified" } }),
                }, items);
        }

        [Test]
        public void Variant_Basic_RowOverride_VariantBeforeBaseline()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",             "Nested.Type"                   },
                    new() { "A/Y",          "Item1",        "item 1 modified",  "item 1 nested type modified"   },
                    new() { "A/X",          "Item0",        "item 0 modified",  ""                              },
                    new() { "",             "Item0",        "item 0",           "item 0 nested type"            },
                    new() { "A/Y",          "",             "",                 "item 0 nested type modified"   },
                    new() { "",             "Item1",        "item 1",           "item 1 nested type"            },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type modified" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                }, items);
        }

        [Test]
        public void Variant_Basic_Append()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",             "Nested.Type"                   },
                    new() { "",             "Item0",        "item 0",           "item 0 nested type"            },
                    new() { "A/X",          "Item1",        "appended item",    ""                              },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "appended item",     Nested = null }),
                }, items);
        }

        [Test]
        public void Variant_Basic_ColumnOverride()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key",      "Name",     "/:A/X",            "/:A/Y",            "Nested.Type",          "/:A/Y" },
                    new() { "Item0",        "item 0",   "item 0 modified",  "",                 "item 0 nested type",   "item 0 nested type modified" },
                    new() { "Item1",        "item 1",   "",                 "item 1 modified",  "item 1 nested type",   "item 1 nested type modified" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type modified" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                }, items);
        }

        [Test]
        public void Variant_Basic_RowOverrideWithMultipleVariantsPerRow()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",             "Nested.Type"                   },
                    new() { "",             "Item0",        "item 0",           "item 0 nested type"            },
                    new() { "A/X, A/Y",     "",             "item 0 modified",  ""                              },
                    new() { "",             "Item1",        "item 1",           "item 1 nested type"            },
                    new() { "A/Y, A/X",     "Item1",        "item 1 modified",  "item 1 nested type modified"   },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                }, items);
        }

        [Test]
        public void Variant_Basic_ColumnOverrideWithMultipleVariantsPerColumn()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "Id #key",      "Name",     "/:A/X, A/Y",       "Nested.Type",          "/:A/Y" },
                    new() { "Item0",        "item 0",   "item 0 modified",  "item 0 nested type",   "item 0 nested type modified" },
                    new() { "Item1",        "item 1",   "item 1 modified",  "item 1 nested type",   "item 1 nested type modified" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type modified" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                }, items);
        }

        [Test]
        public void Variant_Basic_RowAndColumnOverride()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",             "/:A/X",            "Nested.Type"                   },
                    new() { "",             "Item0",        "item 0",           "item 0 modified",  "item 0 nested type"            },
                    new() { "",             "Item1",        "item 1",           "",                 "item 1 nested type"            },
                    new() { "A/Y",          "Item1",        "item 1 modified",  "",                 "item 1 nested type modified"   },
                    new() { "A/Y",          "Item0",        "",                 "",                 "item 0 nested type modified"   },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0 modified",   Nested = new NestedInfo { Type = "item 0 nested type" } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Type = "item 1 nested type" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1 modified",   Nested = new NestedInfo { Type = "item 1 nested type modified" } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Type = "item 0 nested type modified" } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideNestedPartially()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "Nested.Amount",    "Nested.Type",                  "/:B/X" },
                    new() { "",             "Item0",        "item 0",   "123",              "item 0 nested type",           "item 0 nested type modified B" },
                    new() { "A/X",          "Item0",        "",         "",                 "item 0 nested type modified A",  "" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Type = "item 0 nested type" } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Type = "item 0 nested type modified B" } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Type = "item 0 nested type modified A" } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideDeeplyNestedPartially()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "Nested.Amount",    "Nested.Deeply.Int",    "Nested.Deeply.Str",        "/:B/X" },
                    new() { "",             "Item0",        "item 0",   "123",              "456",                  "item 0 deep",              "item 0 deep modified B" },
                    new() { "A/X",          "Item0",        "",         "",                 "",                     "item 0 deep modified A",   "" },
                    new() { "",             "Item1",        "item 1",   "",                 "",                     "item 1 deep",              "item 1 deep modified B" },
                    new() { "A/X",          "Item1",        "",         "",                 "",                     "item 1 deep modified A",   "" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Deeply = new DeeplyNestedInfo { Int = 456, Str = "item 0 deep" } } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Deeply = new DeeplyNestedInfo { Int = 456, Str = "item 0 deep modified B" } } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123, Deeply = new DeeplyNestedInfo { Int = 456, Str = "item 0 deep modified A" } } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Deeply = new DeeplyNestedInfo { Str = "item 1 deep" } } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Deeply = new DeeplyNestedInfo { Str = "item 1 deep modified B" } } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            Nested = new NestedInfo { Deeply = new DeeplyNestedInfo { Str = "item 1 deep modified A" } } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideNestedThatIsNotInBaseline()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "Nested.Amount",    "/:B/X" },
                    new() { "",             "Item0",        "item 0",   "",                 "456" },
                    new() { "A/X",          "Item0",        "",         "123",              "" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = null }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 456 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            Nested = new NestedInfo { Amount = 123 } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideArray_Simple_Vertical()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "IntArray[]",   "/:B/X",    "/:B/Y",    "/:B/Z",    "/:B/W" },
                    new() { "",             "Item0",        "item 0",   "1",            "110",      "1110",     "",         "111110" },
                    new() { "",             "",             "",         "2",            "220",      "",         "22220",    "222220" },
                    new() { "",             "",             "",         "3",            "",         "",         "",         "333330" },
                    new() { "",             "",             "",         "",             "",         "",         "",         "444440" },
                    new() { "A/X",          "Item0",        "",         "11",           "",         "",         "",         "" },
                    new() { "",             "",             "",         "22",           "",         "",         "",         "" },
                    new() { "A/Y",          "Item0",        "",         "111",          "",         "",         "",         "" },
                    new() { "",             "",             "",         "",             "",         "",         "",         "" },
                    new() { "A/Z",          "Item0",        "",         "",             "",         "",         "",         "" },
                    new() { "",             "",             "",         "2222",         "",         "",         "",         "" },
                    new() { "",             "",             "",         "",             "",         "",         "",         "" },
                    new() { "A/W",          "Item0",        "",         "11111",        "",         "",         "",         "" },
                    new() { "",             "",             "",         "22222",        "",         "",         "",         "" },
                    new() { "",             "",             "",         "33333",        "",         "",         "",         "" },
                    new() { "",             "",             "",         "44444",        "",         "",         "",         "" },
                    new() { "",             "Item1",        "item 1",   "",             "1230",     "",         "",         "" },
                    new() { "",             "",             "",         "",             "4560",     "",         "",         "" },
                    new() { "A/X",          "Item1",        "",         "123",          "",         "",         "",         "" },
                    new() { "",             "",             "",         "456",          "",         "",         "",         "" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 1, 2, 3 } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 110, 220 } }),
                    ("B/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 1110 } }),
                    ("B/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 0, 22220 } }),
                    ("B/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 111110, 222220, 333330, 444440 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 11, 22 } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 111 } }),
                    ("A/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 0, 2222 } }),
                    ("A/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 11111, 22222, 33333, 44444 } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { 1230, 4560 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { 123, 456 } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideArray_Simple_Horizontal()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "IntArray[0]",  "/:B/X","/:B/Y","/:B/W",    "IntArray[1]",  "/:B/X","/:B/Z","/:B/W",    "IntArray[2]",  "/:B/W",    "IntArray[3]",  "/:B/W"     },
                    new() { "",             "Item0",        "item 0",   "1",            "110","1110","111110",      "2",            "220","22220","222220",     "3",            "333330",   "",             "444440"    },
                    new() { "A/X",          "Item0",        "",         "11",           "","","",                   "22",           "","","",                   "",             "",         "",             ""          },
                    new() { "A/Y",          "Item0",        "",         "111",          "","","",                   "",             "","","",                   "",             "",         "",             ""          },
                    new() { "A/Z",          "Item0",        "",         "",             "","","",                   "2222",         "","","",                   "",             "",         "",             ""          },
                    new() { "A/W",          "Item0",        "",         "11111",        "","","",                   "22222",        "","","",                   "33333",        "",         "44444",        ""          },
                    new() { "",             "Item1",        "item 1",   "",             "1230","","",               "",             "4560","","",               "",             "",         "",             ""          },
                    new() { "A/X",          "Item1",        "",         "123",          "","","",                   "456",          "","","",                   "",             "",         "",             ""          },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 1, 2, 3 } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 110, 220 } }),
                    ("B/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 1110 } }),
                    ("B/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 111110, 222220, 333330, 444440 } }),
                    ("B/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 0, 22220 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 11, 22 } }),
                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 111 } }),
                    ("A/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 0, 2222 } }),
                    ("A/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            IntArray = new int[] { 11111, 22222, 33333, 44444 } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { } }),
                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { 1230, 4560 } }),
                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            IntArray = new int[] { 123, 456 } }),
                }, items);
        }

        [Test]
        public void Variant_OverrideArray_Complex_Vertical()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "NestedList[].Amount",  "/:B/X", "/:B/Y", "/:B/Z", "/:B/W", "NestedList[].Type",    "/:B/X", "/:B/Y", "/:B/Z", "/:B/W"  },
                    new() { "",             "Item0",        "item 0",   "1",                    "110","1110","","111110",           "a",                    "xx0","","","xxxxx0"                 },
                    new() { "",             "",             "",         "2",                    "220","","22220","222220",          "b",                    "","","",""                         },
                    new() { "",             "",             "",         "3",                    "","","","",                        "c",                    "zz0","","zzzz0","zzzzz0"            },
                    new() { "",             "",             "",         "",                     "","","","444440",                  "",                     "","","",""                         },
                    new() { "A/X",          "Item0",        "",         "11",                   "","","","",                        "xx",                   "","","",""                         },
                    new() { "",             "",             "",         "22",                   "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "zz",                   "","","",""                         },
                    new() { "A/Y",          "Item0",        "",         "111",                  "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "",                     "","","",""                         },
                    new() { "A/Z",          "Item0",        "",         "",                     "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "2222",                 "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "zzzz",                 "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "",                     "","","",""                         },
                    new() { "A/W",          "Item0",        "",         "11111",                "","","","",                        "xxxxx",                "","","",""                         },
                    new() { "",             "",             "",         "22222",                "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "zzzzz",                "","","",""                         },
                    new() { "",             "",             "",         "44444",                "","","","",                        "",                     "","","",""                         },
                    new() { "",             "Item1",        "item 1",   "",                     "1230","","","",                    "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "",                     "xyz0","","",""                     },
                    new() { "A/X",          "Item1",        "",         "123",                  "","","","",                        "",                     "","","",""                         },
                    new() { "",             "",             "",         "",                     "","","","",                        "xyz",                  "","","",""                         },
                }));

            // \note Config handling may output the variants in non-intuitive order
            //       when a column override has an empty value and a following column override has a nonempty value,
            //       such as B/Z and B/W here.
            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1, Type = "a" },
                                                                                                                                            new NestedInfo{ Amount = 2, Type = "b" },
                                                                                                                                            new NestedInfo{ Amount = 3, Type = "c" } } }),

                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 110, Type = "xx0" },
                                                                                                                                            new NestedInfo{ Amount = 220             },
                                                                                                                                            new NestedInfo{               Type = "zz0" } } }),

                    ("B/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1110 } } }),

                    ("B/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 111110, Type = "xxxxx0" },
                                                                                                                                            new NestedInfo{ Amount = 222220                 },
                                                                                                                                            new NestedInfo{                  Type = "zzzzz0" },
                                                                                                                                            new NestedInfo{ Amount = 444440                 } } }),

                    ("B/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { null,
                                                                                                                                            new NestedInfo{ Amount = 22220              },
                                                                                                                                            new NestedInfo{               Type = "zzzz0" } } }),

                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 11, Type = "xx" },
                                                                                                                                            new NestedInfo{ Amount = 22             },
                                                                                                                                            new NestedInfo{              Type = "zz" } } }),

                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 111 } } }),

                    ("A/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { null,
                                                                                                                                            new NestedInfo{ Amount = 2222              },
                                                                                                                                            new NestedInfo{              Type = "zzzz" } } }),

                    ("A/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 11111, Type = "xxxxx" },
                                                                                                                                            new NestedInfo{ Amount = 22222                 },
                                                                                                                                            new NestedInfo{                 Type = "zzzzz" },
                                                                                                                                            new NestedInfo{ Amount = 44444                 } } }),

                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = null }),

                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1230              },
                                                                                                                                            new NestedInfo{              Type = "xyz0" } } }),

                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 123              },
                                                                                                                                            new NestedInfo{              Type = "xyz" } } }),

                }, items);
        }

        [Test]
        public void Variant_OverrideArray_Complex_Horizontal()
        {
            (string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Variant",     "Id #key",      "Name",     "NestedList[0].Amount", "/:B/X","/:B/Y","/:B/W",    "NestedList[0].Type", "/:B/X","/:B/W",
                                                                        "NestedList[1].Amount", "/:B/X","/:B/Z","/:B/W",    "NestedList[1].Type", "/:B/X",
                                                                        "NestedList[2].Amount",                             "NestedList[2].Type", "/:B/X","/:B/Z","/:B/W",
                                                                        "NestedList[3].Amount", "/:B/W",                    "NestedList[3].Type" },

                    new() { "",             "Item0",        "item 0",   "1", "110","1110","111110",                         "a", "xx0","xxxxx0",
                                                                        "2", "220", "22220", "222220",                      "b", "",
                                                                        "3",                                                "c", "zz0", "zzzz0", "zzzzz0",
                                                                        "", "444440",                                       "" },

                    new() { "A/X",          "Item0",        "",         "11", "","","",                                     "xx", "","",
                                                                        "22", "","","",                                     "", "",
                                                                        "",                                                 "zz", "","","" },

                    new() { "A/Y",          "Item0",        "",         "111", "","","",                                    "", "","",
                                                                        "", "","","",                                       "", "" },

                    new() { "A/Z",          "Item0",        "",         "", "","","",                                       "", "","",
                                                                        "2222", "","","",                                   "", "",
                                                                        "",                                                 "zzzz", "","","",
                                                                        "", "",                                             "" },

                    new() { "A/W",          "Item0",        "",         "11111", "","","",                                  "xxxxx", "","",
                                                                        "22222", "","","",                                  "", "",
                                                                        "",                                                 "zzzzz", "","","",
                                                                        "44444", "",                                        "" },

                    new() { "",             "Item1",        "item 1",   "", "1230","","",                                   "", "","",
                                                                        "", "","","",                                       "", "xyz0" },

                    new() { "A/X",          "Item1",        "",         "123", "","","",                                    "", "","",
                                                                        "", "","","",                                       "xyz", "" },
                }));

            Assert.AreEqual(
                new (string, TestInfo)[]
                {
                    (null,  new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1, Type = "a" },
                                                                                                                                            new NestedInfo{ Amount = 2, Type = "b" },
                                                                                                                                            new NestedInfo{ Amount = 3, Type = "c" } } }),

                    ("B/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 110, Type = "xx0" },
                                                                                                                                            new NestedInfo{ Amount = 220             },
                                                                                                                                            new NestedInfo{               Type = "zz0" } } }),

                    ("B/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1110 } } }),

                    ("B/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 111110, Type = "xxxxx0" },
                                                                                                                                            new NestedInfo{ Amount = 222220                 },
                                                                                                                                            new NestedInfo{                  Type = "zzzzz0" },
                                                                                                                                            new NestedInfo{ Amount = 444440                 } } }),

                    ("B/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { null,
                                                                                                                                            new NestedInfo{ Amount = 22220              },
                                                                                                                                            new NestedInfo{               Type = "zzzz0" } } }),


                    ("A/X", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 11, Type = "xx" },
                                                                                                                                            new NestedInfo{ Amount = 22             },
                                                                                                                                            new NestedInfo{              Type = "zz" } } }),

                    ("A/Y", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 111 } } }),

                    ("A/Z", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { null,
                                                                                                                                            new NestedInfo{ Amount = 2222              },
                                                                                                                                            new NestedInfo{              Type = "zzzz" } } }),

                    ("A/W", new TestInfo { Id = TestId.FromString("Item0"), Name = "item 0",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 11111, Type = "xxxxx" },
                                                                                                                                            new NestedInfo{ Amount = 22222                 },
                                                                                                                                            new NestedInfo{                 Type = "zzzzz" },
                                                                                                                                            new NestedInfo{ Amount = 44444                 } } }),
                    (null,  new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = null }),

                    ("B/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 1230              },
                                                                                                                                            new NestedInfo{              Type = "xyz0" } } }),

                    ("A/X", new TestInfo { Id = TestId.FromString("Item1"), Name = "item 1",            NestedList = new List<NestedInfo> { new NestedInfo{ Amount = 123              },
                                                                                                                                            new NestedInfo{              Type = "xyz" } } }),

                }, items);
        }

        #endregion

        #region GameConfigKeyValue

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_Simple(Type keyValueType)
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Member",   "Value" },
                    new() { "Str1",     "test string 1" },
                    new() { "Str2" },
                    new() { "Int0",     "123" },
                    new() { "Int1",     "456" },
                    new() { "TestId",   "TestIdValue" },
                }));

            Assert.That(members, Is.EqualTo(new (string, object)[]
            {
                ("Str1",    "test string 1"),
                ("Int0",    123),
                ("Int1",    456),
                ("TestId",  TestId.FromString("TestIdValue")),
            }).IgnoreCase);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_Simple_DifferentColumnOrder(Type keyValueType)
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Value",         "Member", },
                    new() { "test string 1", "Str1",    },
                    new() { "123",           "Int0",    },
                    new() { "456",           "Int1",    },
                    new() { "TestIdValue",   "TestId",  },
                }));

            Assert.That(members, Is.EqualTo(new (string, object)[]
            {
                ("Str1",    "test string 1"),
                ("Int0",    123),
                ("Int1",    456),
                ("TestId",  TestId.FromString("TestIdValue")),
            }).IgnoreCase);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_Simple_Variants(Type keyValueType)
        {
            (string VariantIdMaybe, string MemberName, object Value)[] members = GameConfigTestHelper.ParseKeyValueMembersWithVariants(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant",     "Member",   "Value" },

                    new() { "",             "Str1",     "test string 1" },
                    new() { "A/X",          "",         "test string 1 x" },
                    new() { "A/Y",          "Str1",     "test string 1 y" },
                    new() { "A/Z",          "Str1" },

                    new() { "A/X",          "Int0",     "1230" },
                    new() { "",             "Int0",     "123" },
                    new() { "A/Y",          "",         "12300" },

                    new() { "",             "Int1",     "456" },

                    new() { "A/W",          "Str1",     "test string 1 w" },

                    new() { "A/Z",          "Int0",     "123000" },
                    new() { "A/W",          "Int0" },

                    new() { "A/X",          "Str0",     "test string 0 x" },

                    new() { "A/X",          "Str2" },
                    new() { "",             "Str2" },
                }));

            Assert.That(members, Is.EqualTo(new (string, string, object)[]
            {
                (null,      "Str1",     "test string 1"),
                (null,      "Int0",     123),
                (null,      "Int1",     456),

                ("A/X",     "Str1",     "test string 1 x"),
                ("A/X",     "Int0",     1230),
                ("A/X",     "Str0",     "test string 0 x"),

                ("A/Y",     "Str1",     "test string 1 y"),
                ("A/Y",     "Int0",     12300),

                ("A/W",     "Str1",     "test string 1 w"),

                ("A/Z",     "Int0",     123000),
            }).IgnoreCase);
        }

        [Test]
        public void KeyValue_Complex()
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "Member",                "Value"                             },
                    new(){ "Int",                   "123"                               },
                    new(){ ""                                                           },
                    new(){ "Nested.Amount",         "10"                                },
                    new(){ "Nested.Type",           "abc"                               },
                    new(){ "Nested.IntArray[0]",    "1"                                 },
                    new(){ "Nested.IntArray[1]",    "2"                                 },
                    new(){                                                              },
                    new(){ "IntList[]",             "1",            "2",        "3"     },
                    new(){ "IntList2[]"                                                 },
                    new(){ "CoordList[].X",         "1",            "3",        "5"     },
                    new(){ "CoordList[].Y",         "2",            "4",        "6"     },
                    new(){ "StringList[0]",         "abc"                               },
                    new(){ "StringList[1]",         "def"                               },
                    new(){ "NestedList[0].Amount",  "1"                                 },
                    new(){ "NestedList[0].Type",    "a"                                 },
                    new(){ "NestedList[1].Amount",  "2"                                 },
                    new(){ "NestedList[1].Type",    "b"                                 },
                }));

            AssertCompat.AreEqual(
                new (string, object)[]
                {
                    ("Int",         123),
                    ("Nested",      new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1, 2 } }),
                    ("IntList",     new List<int> { 1, 2, 3 }),
                    ("IntList2",    new List<int> { }),
                    ("CoordList",   new List<IntVector2> { new IntVector2(1, 2), new IntVector2(3, 4), new IntVector2(5, 6)}),
                    ("StringList",  new List<string> { "abc", "def" }),
                    ("NestedList",  new List<NestedInfo> { new NestedInfo { Amount = 1, Type = "a" }, new NestedInfo { Amount = 2, Type = "b" } }),
                }, members);
        }

        [Test]
        public void KeyValue_Complex_HorizontalArray()
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "Member",                            "Value",    "",         },
                    new(){ "NestedList[].Type",                 "a",        "b",        },
                    new(){ "NestedList[].Deeply.Int",           "123",      "456",      },
                    new(){ "NestedList[].Coords.X",             "12",       "",         },
                    new(){ "NestedList[].Coords.Y",             "",         "78",       },
                    new(){ "NestedList[].IntArray[0]",          "10",       "20",       },
                    new(){ "NestedList[].IntArray[1]",          "",         "40",       },
                    new(){ "NestedList[].DeeplyArray[0].Int",   "100",      "200",      },
                    new(){ "NestedList[].DeeplyArray[0].Str",   "abc",      "def",      },
                    new(){ "NestedList[].DeeplyArray[1].Str",   "x",        "",         },
                    new(){ "NestedList[].DeeplyArray[2].Int",   "",         "20000",    },
                }));

            AssertCompat.AreEqual(
                new (string, object)[]
                {
                    ("NestedList", new List<NestedInfo>
                        {
                            new() { Type = "a", Deeply = new() { Int = 123 }, Coords = new() { X = 12 }, IntArray = new int[] { 10 }, DeeplyArray = new DeeplyNestedInfo[] { new() { Int = 100, Str = "abc" }, new() { Str = "x" } } },
                            new() { Type = "b", Deeply = new() { Int = 456 }, Coords = new() { Y = 78 }, IntArray = new int[] { 20, 40 }, DeeplyArray = new DeeplyNestedInfo[] { new() { Int = 200, Str = "def" }, null, new() { Int = 20000 } } },
                        }),
                }, members);
        }

        [Test]
        public void KeyValue_Complex_Comments()
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "Member",                "// comment column",     "Value",   "// comment column",                            },
                    new(){ "Int",                   "asdf",                  "123",     "asdf",                                         },
                    new(){ "",                      "asdf",                  "",        "asdf",                                         },
                    new(){ "Nested.Amount",         "asdf",                  "10",      "asdf",                                         },
                    new(){ "Nested.Type",           "asdf",                  "abc",     "asdf",                                         },
                    new(){ "Nested.IntArray[0]",    "asdf",                  "1",       "asdf",                                         },
                    new(){ "Nested.IntArray[1]",    "asdf",                  "2",       "asdf",                                         },
                    new(){                                                                                                              },
                    new(){ "IntList[]",             "asdf",                  "1",       "asdf",                     "2",        "3"     },
                    new(){ "IntList2[]",            "asdf",                  "",        "asdf",                                         },
                    new(){ "CoordList[].X",         "asdf",                  "1",      "asdf",                      "3",        "5"     },
                    new(){ "//comment row",         "asdf",                  "asdf",    "asdf",                     "asdf",     "asdf"  },
                    new(){ "CoordList[].Y",         "asdf",                  "2",     "asdf",                       "4",        "6"     },
                    new(){ "StringList[0]",         "asdf",                  "abc",     "asdf",                                         },
                    new(){ "StringList[1]",         "asdf",                  "def",     "asdf",                                         },
                    new(){ "//comment row"                                                                                              },
                    new(){ "NestedList[0].Amount",  "asdf",                  "1",       "asdf",                                         },
                    new(){ "NestedList[0].Type",    "asdf",                  "a",       "asdf",                                         },
                    new(){ "NestedList[1].Amount",  "asdf",                  "2",       "asdf",                                         },
                    new(){ "//comment row",         "asdf",                  "asdf",    "asdf",                     "asdf",     "asdf"  },
                    new(){ "NestedList[1].Type",    "asdf",                  "b",       "asdf",                                         },
                }));

            AssertCompat.AreEqual(
                new (string, object)[]
                {
                    ("Int",         123),
                    ("Nested",      new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1, 2 } }),
                    ("IntList",     new List<int> { 1, 2, 3 }),
                    ("IntList2",    new List<int> { }),
                    ("CoordList",   new List<IntVector2> { new IntVector2(1, 2), new IntVector2(3, 4), new IntVector2(5, 6)}),
                    ("StringList",  new List<string> { "abc", "def" }),
                    ("NestedList",  new List<NestedInfo> { new NestedInfo { Amount = 1, Type = "a" }, new NestedInfo { Amount = 2, Type = "b" } }),
                }, members);
        }

        [Test]
        public void KeyValue_Complex_Comments_DifferentColumnOrder()
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "",              "// comment column",     "Value",   "// comment column",    "",         "",      "Member",                "// comment column", },
                    new(){ "",              "asdf",                  "123",     "asdf",                 "",         "",      "Int",                   "asdf",              },
                    new(){ "",              "asdf",                  "",        "asdf",                 "",         "",      "",                      "asdf",              },
                    new(){ "",              "asdf",                  "10",      "asdf",                 "",         "",      "Nested.Amount",         "asdf",              },
                    new(){ "",              "asdf",                  "abc",     "asdf",                 "",         "",      "Nested.Type",           "asdf",              },
                    new(){ "",              "asdf",                  "1",       "asdf",                 "",         "",      "Nested.IntArray[0]",    "asdf",              },
                    new(){ "",              "asdf",                  "2",       "asdf",                 "",         "",      "Nested.IntArray[1]",    "asdf",              },
                    new(){                                                                                                                                                 },
                    new(){ "",              "asdf",                  "1",       "asdf",                 "2",        "3",     "IntList[]",             "asdf",              },
                    new(){ "",              "asdf",                  "",        "asdf",                 "",         "",      "IntList2[]",            "asdf",              },
                    new(){ "",              "asdf",                  "1",       "asdf",                 "3",        "5",     "CoordList[].X",         "asdf",              },
                    new(){ "//comment row", "asdf",                  "asdf",    "asdf",                 "asdf",     "asdf",  "asdf",                  "asdf",              },
                    new(){ "",              "asdf",                  "2",       "asdf",                 "4",        "6",     "CoordList[].Y",         "asdf",              },
                    new(){ "",              "asdf",                  "abc",     "asdf",                 "",         "",      "StringList[0]",         "asdf",              },
                    new(){ "",              "asdf",                  "def",     "asdf",                 "",         "",      "StringList[1]",         "asdf",              },
                    new(){ "//comment row", ""                                                                                                                             },
                    new(){ "",              "asdf",                  "1",       "asdf",                 "",         "",      "NestedList[0].Amount",  "asdf",              },
                    new(){ "",              "asdf",                  "a",       "asdf",                 "",         "",      "NestedList[0].Type",    "asdf",              },
                    new(){ "",              "asdf",                  "2",       "asdf",                 "",         "",      "NestedList[1].Amount",  "asdf",              },
                    new(){ "//comment row", "asdf",                  "asdf",    "asdf",                 "asdf",     "asdf",  "asdf",                  "asdf",              },
                    new(){ "",              "asdf",                  "b",       "asdf",                 "",         "",      "NestedList[1].Type",    "asdf",              },
                }));

            AssertCompat.AreEqual(
                new (string, object)[]
                {
                    ("Int",         123),
                    ("Nested",      new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1, 2 } }),
                    ("IntList",     new List<int> { 1, 2, 3 }),
                    ("IntList2",    new List<int> { }),
                    ("CoordList",   new List<IntVector2> { new IntVector2(1, 2), new IntVector2(3, 4), new IntVector2(5, 6)}),
                    ("StringList",  new List<string> { "abc", "def" }),
                    ("NestedList",  new List<NestedInfo> { new NestedInfo { Amount = 1, Type = "a" }, new NestedInfo { Amount = 2, Type = "b" } }),
                }, members);
        }

        [Test]
        public void KeyValue_Complex_Variants()
        {
            (string VariantIdMaybe, string MemberName, object Value)[] members = GameConfigTestHelper.ParseKeyValueMembersWithVariants<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "/Variant",  "Member",               "Value"                                         },
                    new(){ "",          "Int",                  "123"                                           },
                    new(){ "A/X",       "",                     "1230",                                         },
                    new(){ "A/Y",       "Int",                  "12300",                                        },
                    new(){ "A/Z",       "",                     "",                                             },
                    new(){ "A/P",       "Int",                  "",                                             },
                    new(){ "",          "Nested.Amount",        "10"                                            },
                    new(){ "A/X",       "",                     "100",                                          },
                    new(){ "A/Y",       "Nested.Type",          "abc y",                                        },
                    new(){ "",          "Nested.Type",          "abc"                                           },
                    new(){ "A/X",       "",                     "abc x",                                        },
                    new(){ "A/P",       "",                     "abc p",                                        },
                    new(){ "A/Y",       "Nested.Amount",        "1000",                                         },
                    new(){ "",          "Nested.IntArray[0]",   "1"                                             },
                    new(){ "A/X",       "",                     "10"                                            },
                    new(){ "A/Y",       "",                     ""                                              },
                    new(){ "A/Z",       "Nested.IntArray[0]",   "1000"                                          },
                    new(){ "",          "Nested.IntArray[1]",   "2"                                             },
                    new(){ "",                                                                                  },
                    new(){ "",          "IntList[]",            "1",            "2",        "3"                 },
                    new(){ "A/X",       "",                     "10",           "20"                            },
                    new(){ "A/Y",       "IntList[]",            "100"                                           },
                    new(){ "A/Z",       "IntList[]",                                                            },
                    new(){ "A/P",       "IntList[0]",           "10000"                                         },
                    new(){ "A/P",       "IntList[1]",           "20000"                                         },
                    new(){ "A/Q",       "IntList",              "100000, 200000"                                },
                    new(){ "",          "IntList2[]",           "",                                             },
                    new(){ "A/X",       "IntList2[]",           "",                                             },
                    new(){ "A/Y",       "",                     "100",         "200"                            },
                    new(){ "",          "CoordList[].X",        "1",           "3",             "5"             },
                    new(){ "A/X",       "CoordList[].X",        "11",          "33",            "55"            },
                    new(){ "A/Y",       "",                     "111",         "333",           "555"           },
                    new(){ "",          "CoordList[].Y",        "2",           "4",             "6"             },
                    new(){ "A/X",       "CoordList[].Y",        "22",          "44",            "66"            },
                    new(){ "A/Z",       "CoordList",            "(1111,2222),(3333,4444), (5555, 6666)"         },
                    new(){ "",          "StringList[0]",        "abc"                                           },
                    new(){ "A/X",       "",                     "abc x"                                         },
                    new(){ "",          "StringList[1]",        "def"                                           },
                    new(){ "A/Y",       "",                     "def y"                                         },
                    new(){ "A/Z",       "StringList[1]",        "def z"                                         },
                    new(){ "A/P",       "StringList",           "[]"                                            },
                    new(){ "A/Q",       "StringList",           ""                                              },
                    new(){ "",          "NestedList[0].Amount", "1"                                             },
                    new(){ "A/X",       "NestedList[1].Amount", "22"                                            },
                    new(){ "",          "NestedList[0].Type",   "a"                                             },
                    new(){ "",          "NestedList[1].Amount", "2"                                             },
                    new(){ "",          "NestedList[1].Type",   "b"                                             },
                    new(){ "A/X",       "NestedList[0].Type",   "a x"                                           },
                }));

            AssertCompat.AreEqual(
                new (string, string, object)[]
                {
                    (null,  "Int",          123),
                    (null,  "Nested",       new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1, 2 } }),
                    (null,  "IntList",      new List<int> { 1, 2, 3 }),
                    (null,  "IntList2",     new List<int> { }),
                    (null,  "CoordList",    new List<IntVector2> { new IntVector2(1, 2), new IntVector2(3, 4), new IntVector2(5, 6)}),
                    (null,  "StringList",   new List<string> { "abc", "def" }),
                    (null,  "NestedList",   new List<NestedInfo> { new NestedInfo { Amount = 1, Type = "a" }, new NestedInfo { Amount = 2, Type = "b" } }),
                    ("A/X", "Int",          1230),
                    ("A/X", "Nested",       new NestedInfo { Amount = 100, Type = "abc x", IntArray = new int[] { 10 } }),
                    ("A/X", "IntList",      new List<int> { 10, 20 }),
                    ("A/X", "CoordList",    new List<IntVector2> { new IntVector2(11, 22), new IntVector2(33, 44), new IntVector2(55, 66)}),
                    ("A/X", "StringList",   new List<string> { "abc x" }),
                    ("A/X", "NestedList",   new List<NestedInfo> { new NestedInfo { Type = "a x" }, new NestedInfo { Amount = 22 } }),
                    ("A/Y", "Int",          12300),
                    ("A/Y", "Nested",       new NestedInfo { Amount = 1000, Type = "abc y", IntArray = new int[] { 1, 2 } }),
                    ("A/Y", "IntList",      new List<int> { 100 }),
                    ("A/Y", "IntList2",     new List<int> { 100, 200 }),
                    ("A/Y", "CoordList",    new List<IntVector2> { new IntVector2(111, 0), new IntVector2(333, 0), new IntVector2(555, 0)}),
                    ("A/Y", "StringList",   new List<string> { null, "def y" }),
                    ("A/P", "Nested",       new NestedInfo { Amount = 10, Type = "abc p", IntArray = new int[] { 1, 2 } }),
                    ("A/P", "IntList",      new List<int> { 10000, 20000 }),
                    ("A/P", "StringList",   new List<string> { }),
                    ("A/Z", "Nested",       new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1000 } }),
                    ("A/Z", "CoordList",    new List<IntVector2> { new IntVector2(1111, 2222), new IntVector2(3333, 4444), new IntVector2(5555, 6666)}),
                    ("A/Z", "StringList",   new List<string> { null, "def z" }),
                    ("A/Q", "IntList",      new List<int> { 100000, 200000 }),
                }, members);
        }

        [Test]
        public void KeyValue_Complex_Variants_DifferentColumnOrder()
        {
            (string VariantIdMaybe, string MemberName, object Value)[] members = GameConfigTestHelper.ParseKeyValueMembersWithVariants<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "Value",             "",         "",                 "Member",                   "/Variant",  },
                    new(){ "123",               "",         "",                 "Int",                      "",          },
                    new(){ "1230",              "",         "",                 "",                         "A/X",       },
                    new(){ "12300",             "",         "",                 "Int",                      "A/Y",       },
                    new(){ "",                  "",         "",                 "",                         "A/Z",       },
                    new(){ "",                  "",         "",                 "Int",                      "A/P",       },
                    new(){ "10",                "",         "",                 "Nested.Amount",            "",          },
                    new(){ "100",               "",         "",                 "",                         "A/X",       },
                    new(){ "abc y",             "",         "",                 "Nested.Type",              "A/Y",       },
                    new(){ "abc",               "",         "",                 "Nested.Type",              "",          },
                    new(){ "abc x",             "",         "",                 "",                         "A/X",       },
                    new(){ "abc p",             "",         "",                 "",                         "A/P",       },
                    new(){ "1000",              "",         "",                 "Nested.Amount",            "A/Y",       },
                    new(){ "1",                 "",         "",                 "Nested.IntArray[0]",       "",          },
                    new(){ "10",                "",         "",                 "",                         "A/X",       },
                    new(){ "",                  "",         "",                 "",                         "A/Y",       },
                    new(){ "1000",              "",         "",                 "Nested.IntArray[0]",       "A/Z",       },
                    new(){ "2",                 "",         "",                 "Nested.IntArray[1]",       "",          },
                    new(){ "",                  "",         "",                                             "",          },
                    new(){ "1",                 "2",        "3",                "IntList[]",                "",          },
                    new(){ "10",                "20",       "",                 "",                         "A/X",       },
                    new(){ "100",               "",         "",                 "IntList[]",                "A/Y",       },
                    new(){ "",                  "",         "",                 "IntList[]",                "A/Z",       },
                    new(){ "10000",             "",         "",                 "IntList[0]",               "A/P",       },
                    new(){ "20000",             "",         "",                 "IntList[1]",               "A/P",       },
                    new(){ "100000, 200000",    "",         "",                 "IntList",                  "A/Q",       },
                    new(){ "",                  "",         "",                 "IntList2[]",               "",          },
                    new(){ "",                  "",         "",                 "IntList2[]",               "A/X",       },
                    new(){ "100",               "200",      "",                 "",                         "A/Y",       },
                    new(){ "1",                 "3",        "5",                "CoordList[].X",            "",          },
                    new(){ "11",                "33",       "55",               "CoordList[].X",            "A/X",       },
                    new(){ "111",               "333",      "555",              "",                         "A/Y",       },
                    new(){ "2",                 "4",        "6",                "CoordList[].Y",            "",          },
                    new(){ "22",                "44",       "66",               "CoordList[].Y",            "A/X",       },
                    new(){ "(1111,2222),(3333,4444), (5555, 6666)", "", "",     "CoordList",                "A/Z",       },
                    new(){ "abc",               "",         "",                 "StringList[0]",            "",          },
                    new(){ "abc x",             "",         "",                 "",                         "A/X",       },
                    new(){ "def",               "",         "",                 "StringList[1]",            "",          },
                    new(){ "def y",             "",         "",                 "",                         "A/Y",       },
                    new(){ "def z",             "",         "",                 "StringList[1]",            "A/Z",       },
                    new(){ "[]",                "",         "",                 "StringList",               "A/P",       },
                    new(){ "",                  "",         "",                 "StringList",               "A/Q",       },
                    new(){ "1",                 "",         "",                 "NestedList[0].Amount",     "",          },
                    new(){ "22",                "",         "",                 "NestedList[1].Amount",     "A/X",       },
                    new(){ "a",                 "",         "",                 "NestedList[0].Type",       "",          },
                    new(){ "2",                 "",         "",                 "NestedList[1].Amount",     "",          },
                    new(){ "b",                 "",         "",                 "NestedList[1].Type",       "",          },
                    new(){ "a x",               "",         "",                 "NestedList[0].Type",       "A/X",       },
                }));

            AssertCompat.AreEqual(
                new (string, string, object)[]
                {
                    (null,  "Int",          123),
                    (null,  "Nested",       new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1, 2 } }),
                    (null,  "IntList",      new List<int> { 1, 2, 3 }),
                    (null,  "IntList2",     new List<int> { }),
                    (null,  "CoordList",    new List<IntVector2> { new IntVector2(1, 2), new IntVector2(3, 4), new IntVector2(5, 6)}),
                    (null,  "StringList",   new List<string> { "abc", "def" }),
                    (null,  "NestedList",   new List<NestedInfo> { new NestedInfo { Amount = 1, Type = "a" }, new NestedInfo { Amount = 2, Type = "b" } }),
                    ("A/X", "Int",          1230),
                    ("A/X", "Nested",       new NestedInfo { Amount = 100, Type = "abc x", IntArray = new int[] { 10 } }),
                    ("A/X", "IntList",      new List<int> { 10, 20 }),
                    ("A/X", "CoordList",    new List<IntVector2> { new IntVector2(11, 22), new IntVector2(33, 44), new IntVector2(55, 66)}),
                    ("A/X", "StringList",   new List<string> { "abc x" }),
                    ("A/X", "NestedList",   new List<NestedInfo> { new NestedInfo { Type = "a x" }, new NestedInfo { Amount = 22 } }),
                    ("A/Y", "Int",          12300),
                    ("A/Y", "Nested",       new NestedInfo { Amount = 1000, Type = "abc y", IntArray = new int[] { 1, 2 } }),
                    ("A/Y", "IntList",      new List<int> { 100 }),
                    ("A/Y", "IntList2",     new List<int> { 100, 200 }),
                    ("A/Y", "CoordList",    new List<IntVector2> { new IntVector2(111, 0), new IntVector2(333, 0), new IntVector2(555, 0)}),
                    ("A/Y", "StringList",   new List<string> { null, "def y" }),
                    ("A/P", "Nested",       new NestedInfo { Amount = 10, Type = "abc p", IntArray = new int[] { 1, 2 } }),
                    ("A/P", "IntList",      new List<int> { 10000, 20000 }),
                    ("A/P", "StringList",   new List<string> { }),
                    ("A/Z", "Nested",       new NestedInfo { Amount = 10, Type = "abc", IntArray = new int[] { 1000 } }),
                    ("A/Z", "CoordList",    new List<IntVector2> { new IntVector2(1111, 2222), new IntVector2(3333, 4444), new IntVector2(5555, 6666)}),
                    ("A/Z", "StringList",   new List<string> { null, "def z" }),
                    ("A/Q", "IntList",      new List<int> { 100000, 200000 }),
                }, members);
        }

        [Test]
        public void KeyValue_OmittedCollectionElement()
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers<ComplexTestKeyValue>(
                new GameConfigParseKeyValuePipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexTestKeyValue", new List<string>[]
                {
                    new(){ "Member",                "Value"                             },
                    new(){ "IntList[]",             "",             "2",        "3"     },
                    new(){ "IntList2[1]",           "20",                               },
                    new(){ "IntList2[2]",           "30",                               },
                }));

            AssertCompat.AreEqual(
                new (string, object)[]
                {
                    ("IntList",     new List<int> { 0, 2, 3 }),
                    ("IntList2",    new List<int> { 0, 20, 30 }),
                }, members);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_SyntaxAdapter_NoHeader(Type keyValueType)
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(syntaxAdapterAttribs: new GameConfigSyntaxAdapterAttribute[] { new GameConfigSyntaxAdapterAttribute(ensureHasKeyValueSheetHeader: true) }),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "Str0",     "str", "comment" },
                    new() { "Int0",     "123", "comment" },
                }));

            Assert.That(members, Is.EqualTo(new (string, object)[]
            {
                ("Str0",    "str"),
                ("Int0",    123),
            }).IgnoreCase);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_SyntaxAdapter_EmptyHeader(Type keyValueType)
        {
            (string MemberName, object Value)[] members = GameConfigTestHelper.ParseNonVariantKeyValueMembers(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(syntaxAdapterAttribs: new GameConfigSyntaxAdapterAttribute[] { new GameConfigSyntaxAdapterAttribute(ensureHasKeyValueSheetHeader: true) }),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() {},
                    new() { "Str0",     "str", "comment" },
                    new() { "Int0",     "123", "comment" },
                }));

            Assert.That(members, Is.EqualTo(new (string, object)[]
            {
                ("Str0",    "str"),
                ("Int0",    123),
            }).IgnoreCase);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_SyntaxAdapter_LegacyVariantHeader(Type keyValueType)
        {
            (string VariantIdMaybe, string MemberName, object Value)[] members = GameConfigTestHelper.ParseKeyValueMembersWithVariants(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(syntaxAdapterAttribs: new GameConfigSyntaxAdapterAttribute[] { new GameConfigSyntaxAdapterAttribute(ensureHasKeyValueSheetHeader: true) }),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant"                                  },
                    new() { "",         "Str0",     "str",  "comment"   },
                    new() { "",         "Int0",     "123",  "comment"   },
                    new() { "A/X",      "Int0",     "1230", "comment"   },
                }));

            Assert.That(members, Is.EqualTo(new (string, string, object)[]
            {
                (null,  "Str0",    "str"),
                (null,  "Int0",    123),
                ("A/X", "Int0",    1230),
            }).IgnoreCase);
        }

        [TestCase(typeof(TestKeyValue))]
        [TestCase(typeof(TestKeyValueWithConstructor), Ignore = "Building GameConfig KeyValues is currently not supported")]
        public void KeyValue_SyntaxAdapter_AlreadyHasNewHeader(Type keyValueType)
        {
            (string VariantIdMaybe, string MemberName, object Value)[] members = GameConfigTestHelper.ParseKeyValueMembersWithVariants(keyValueType,
                new GameConfigParseKeyValuePipelineConfig(syntaxAdapterAttribs: new GameConfigSyntaxAdapterAttribute[] { new GameConfigSyntaxAdapterAttribute(ensureHasKeyValueSheetHeader: true) }),
                GameConfigTestHelper.ParseSpreadsheet("TestKeyValue", new List<string>[]
                {
                    new() { "/Variant", "Member",   "Value",    "//"        },
                    new() { "",         "Str0",     "str",      "comment"   },
                    new() { "",         "Int0",     "123",      "comment"   },
                    new() { "A/X",      "Int0",     "1230",     "comment"   },
                }));

            Assert.That(members, Is.EqualTo(new (string, string, object)[]
            {
                (null,  "Str0",    "str"),
                (null,  "Int0",    123),
                ("A/X", "Int0",    1230),
            }).IgnoreCase);
        }

        #endregion

        [Test]
        public void Aliases()
        {
            (List<TestId> Aliases, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithAliases<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Aliases",                     "Id #key",  "Name"    },
                    new() { "Item0Alias",                   "Item0",    "item 0", },
                    new() { "",                             "Item1",    "item 1"  },
                    new() { "Item2Alias, Item2OtherAlias",  "Item2",    "item 2"  },
                }));

            AssertCompat.AreEqual(
                new (List<TestId>, TestInfo)[]
                {
                    (new List<TestId>{ TestId.FromString("Item0Alias") },
                     new TestInfo{ Id = TestId.FromString("Item0"), Name = "item 0" }),

                    (null,
                     new TestInfo{ Id = TestId.FromString("Item1"), Name = "item 1" }),

                    (new List<TestId>{ TestId.FromString("Item2Alias"), TestId.FromString("Item2OtherAlias") },
                     new TestInfo{ Id = TestId.FromString("Item2"), Name = "item 2" }),
                }, items);
        }

        [Test]
        public void AliasesAndVariants()
        {
            // \note Aliases defined in variant rows are not a very useful feature since it has the same effect
            //       as just defining them in the baseline row (aliases are not per-variant).
            //       Supporting it anyway for now, for compatibility.

            (List<TestId> Aliases, string VariantIdMaybe, TestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithAliasesAndVariants<TestId, TestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("TestItems", new List<string>[]
                {
                    new() { "/Aliases",                                         "/Variant",     "Id #key",  "Name"    },
                    new() { "Item0Alias",                                       "",             "Item0",    "item 0", },
                    new() { "Item0AliasInVariant",                              "X/Y",          "",         "item 0 modified", },
                    new() { "",                                                 "",             "Item1",    "item 1"  },
                    new() { "Item1AliasInVariant,Item1OtherAliasInVariant",     "X/Y",          "Item1",    "item 1 modified"  },
                    new() { "Item2Alias, Item2OtherAlias",                      "",             "Item2",    "item 2"  },
                    new() { "",                                                 "X/Y",          "",         "item 2 modified"  },
                }));

            AssertCompat.AreEqual(
                new (List<TestId>, string, TestInfo)[]
                {
                    (new List<TestId>{ TestId.FromString("Item0Alias") },
                     null,
                     new TestInfo{ Id = TestId.FromString("Item0"), Name = "item 0" }),

                    (new List<TestId>{ TestId.FromString("Item0AliasInVariant") },
                     "X/Y",
                     new TestInfo{ Id = TestId.FromString("Item0"), Name = "item 0 modified" }),

                    (null,
                     null,
                     new TestInfo{ Id = TestId.FromString("Item1"), Name = "item 1" }),

                    (new List<TestId>{ TestId.FromString("Item1AliasInVariant"), TestId.FromString("Item1OtherAliasInVariant") },
                     "X/Y",
                     new TestInfo{ Id = TestId.FromString("Item1"), Name = "item 1 modified" }),

                    (new List<TestId>{ TestId.FromString("Item2Alias"), TestId.FromString("Item2OtherAlias") },
                     null,
                     new TestInfo{ Id = TestId.FromString("Item2"), Name = "item 2" }),

                    (null,
                     "X/Y",
                     new TestInfo{ Id = TestId.FromString("Item2"), Name = "item 2 modified" }),
                }, items);
        }

        /// <summary>
        /// Tests the inherit-from-above behavior of #key columns.
        /// </summary>
        [Test]
        public void ComplexKeyInheritance()
        {
            (string VariantIdMaybe, ComplexKeyTestInfo Item)[] items = GameConfigTestHelper.ParseItemsWithVariants<(string, int), ComplexKeyTestInfo>(
                new GameConfigParseLibraryPipelineConfig(),
                GameConfigTestHelper.ParseSpreadsheet("ComplexKeyTestItems", new List<string>[]
                {
                    new() { "/Variant",         "Type #key",        "Level #key",   "Name"              },

                    new() { "",                 "Type0",            "1",            "Type0_1"           },
                    new() { "",                 "Type0",            "2",            "Type0_2"           },
                    new() { "",                 "Type0",            "3",            "Type0_3"           },
                    new() { "A/X",              "Type0",            "1",            "Type0_1 A/X"       },
                    new() { "A/X",              "Type0",            "2",            "Type0_2 A/X"       },
                    new() { "A/X",              "Type0",            "3",            "Type0_3 A/X"       },

                    new() { "",                 "Type1",            "1",            "Type1_1"           },
                    new() { "",                 "",                 "2",            "Type1_2"           },
                    new() { "",                 "",                 "3",            "Type1_3"           },
                    new() { "A/X",              "",                 "1",            "Type1_1 A/X"       },
                    new() { "A/X",              "",                 "2",            "Type1_2 A/X"       },
                    new() { "A/X",              "",                 "3",            "Type1_3 A/X"       },

                    new() { "",                 "Type2",            "1",            "Type2_1"           },
                    new() { "",                 "Type3",            "",             "Type3_1"           },
                    new() { "",                 "Type4",            "",             "Type4_1"           },
                    new() { "A/X",              "Type2",            "",             "Type2_1 A/X"       },
                    new() { "A/X",              "Type3",            "",             "Type3_1 A/X"       },
                    new() { "A/X",              "Type4",            "",             "Type4_1 A/X"       },

                    new() { "",                 "Type5",            "1",            "Type5_1"           },
                    new() { "",                 "",                 "2",            "Type5_2"           },
                    new() { "",                 "",                 "3",            "Type5_3"           },
                    new() { "A/X",              "Type6",            "1",            "Type6_1 A/X"       },
                    new() { "A/X",              "",                 "2",            "Type6_2 A/X"       },
                    new() { "A/X",              "",                 "3",            "Type6_3 A/X"       },

                    new() { "",                 "Type7",            "1",            "Type7_1"           },
                    new() { "A/X",              "",                 "",             "Type7_1 A/X"       },
                    new() { "A/Y",              "",                 "",             "Type7_1 A/Y"       },
                    new() { "A/Z",              "",                 "",             "Type7_1 A/Z"       },
                }));

            Assert.AreEqual(
                new (string, ComplexKeyTestInfo)[]
                {
                    (null,          new ComplexKeyTestInfo { Type = "Type0", Level = 1, Name = "Type0_1" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type0", Level = 2, Name = "Type0_2" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type0", Level = 3, Name = "Type0_3" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type0", Level = 1, Name = "Type0_1 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type0", Level = 2, Name = "Type0_2 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type0", Level = 3, Name = "Type0_3 A/X" }),

                    (null,          new ComplexKeyTestInfo { Type = "Type1", Level = 1, Name = "Type1_1" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type1", Level = 2, Name = "Type1_2" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type1", Level = 3, Name = "Type1_3" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type1", Level = 1, Name = "Type1_1 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type1", Level = 2, Name = "Type1_2 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type1", Level = 3, Name = "Type1_3 A/X" }),

                    (null,          new ComplexKeyTestInfo { Type = "Type2", Level = 1, Name = "Type2_1" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type3", Level = 1, Name = "Type3_1" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type4", Level = 1, Name = "Type4_1" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type2", Level = 1, Name = "Type2_1 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type3", Level = 1, Name = "Type3_1 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type4", Level = 1, Name = "Type4_1 A/X" }),

                    (null,          new ComplexKeyTestInfo { Type = "Type5", Level = 1, Name = "Type5_1" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type5", Level = 2, Name = "Type5_2" }),
                    (null,          new ComplexKeyTestInfo { Type = "Type5", Level = 3, Name = "Type5_3" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type6", Level = 1, Name = "Type6_1 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type6", Level = 2, Name = "Type6_2 A/X" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type6", Level = 3, Name = "Type6_3 A/X" }),

                    (null,          new ComplexKeyTestInfo { Type = "Type7", Level = 1, Name = "Type7_1" }),
                    ("A/X",         new ComplexKeyTestInfo { Type = "Type7", Level = 1, Name = "Type7_1 A/X" }),
                    ("A/Y",         new ComplexKeyTestInfo { Type = "Type7", Level = 1, Name = "Type7_1 A/Y" }),
                    ("A/Z",         new ComplexKeyTestInfo { Type = "Type7", Level = 1, Name = "Type7_1 A/Z" }),
                }, items);
        }

        // \todo [petri] add more tests: from-above inheritance? others?
    }
}

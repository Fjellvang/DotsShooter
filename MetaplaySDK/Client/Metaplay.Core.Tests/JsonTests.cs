// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Json;
using Metaplay.Core.Math;
using Metaplay.Core.Schedule;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Tests
{
    public class ImportTest
    {
        public string           String      { get; set; }
        public int              Integer     { get; set; }
        public F64              Fixed64     { get; set; }
        public F32              Fixed32     { get; set; }
        public TestStringId     StringId    { get; set; }
    }

    [TestFixture]
    public class JsonTests
    {
#if !METAPLAY_USE_LEGACY_FIXED_POINT_PARSING // only work with the new parser
        [Test]
        public void JsonImport()
        {
            F64 f64 = F64.Ratio100(12_52);
            F32 f32 = F32.Ratio(-1, 1000);
            string input = $"{{\"String\":\"SomeString\",\"Integer\":123,\"Fixed64\":{f64},\"Fixed32\":{f32},\"StringId\":\"SomeStringId\"}}";
            ImportTest test = JsonSerialization.Deserialize<ImportTest>(input);

            Assert.AreEqual("SomeString", test.String);
            Assert.AreEqual(123, test.Integer);
            Assert.AreEqual(f64, test.Fixed64);
            Assert.AreEqual(f32, test.Fixed32);
            Assert.AreEqual("SomeStringId", test.StringId.ToString());
        }
#endif

#if !METAPLAY_USE_LEGACY_FIXED_POINT_PARSING && !METAPLAY_USE_LEGACY_FIXED_POINT_FORMATTING // rountrip tests require both parsing and formatting to be new implementations
        [Test]
        public void TestF32()
        {
            void Test(F32 value)
            {
                string str = value.ToString();
                F32 parsed = JsonSerialization.Deserialize<F32>(str);
                Assert.AreEqual(value, parsed);
            }

            Test(F32.Ratio100(12_34));
            Test(F32.Ratio100(-12_34));
            Test(F32.Ratio100(12_00));
            Test(F32.Ratio100(-12_00));
            Test(F32.Ratio100(12_00));
            Test(F32.Ratio100(-12_00));
            Test(F32.Zero);
            Test(F32.Zero);

            Assert.AreEqual(F32.Zero, JsonSerialization.Deserialize<F32>("0"));
            Assert.AreEqual(F32.Zero, JsonSerialization.Deserialize<F32>("0.0"));
            Assert.AreEqual(F32.Zero, JsonSerialization.Deserialize<F32>("0.0000"));
        }
#endif

#if !METAPLAY_USE_LEGACY_FIXED_POINT_PARSING && !METAPLAY_USE_LEGACY_FIXED_POINT_FORMATTING // rountrip tests require both parsing and formatting to be new implementations
        [Test]
        public void TestF64()
        {
            void Test(F64 reference)
            {
                string str = reference.ToString();
                F64 parsed = JsonSerialization.Deserialize<F64>(str);
                Assert.AreEqual(reference, parsed);
            }

            Test(F64.Ratio100(12_34));
            Test(F64.Ratio100(-12_34));
            Test(F64.Ratio100(12_00));
            Test(F64.Ratio100(-12_00));
            Test(F64.Ratio100(12_00));
            Test(F64.Ratio100(-12_00));
            Test(F64.Zero);

            Assert.AreEqual(F64.Zero, JsonSerialization.Deserialize<F64>("0"));
            Assert.AreEqual(F64.Zero, JsonSerialization.Deserialize<F64>("0.0"));
            Assert.AreEqual(F64.Zero, JsonSerialization.Deserialize<F64>("0.0000"));
        }
#endif

        [Test]
        public void SerializeStringId()
        {
            Assert.AreEqual("\"SomeStringId\"", JsonSerialization.SerializeToString(TestStringId.FromString("SomeStringId")));
        }

        [Test]
        public void SerializeMetaTime()
        {
            Assert.AreEqual("\"1970-01-01T00:00:00.0000000Z\"", JsonSerialization.SerializeToString(MetaTime.Epoch));
            Assert.AreEqual("\"2021-12-31T23:59:59.9990000Z\"", JsonSerialization.SerializeToString(MetaTime.FromDateTime(new DateTime(2021, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc))));
        }

        [Test]
        public void DeserializeMetaTime()
        {
            Assert.AreEqual(MetaTime.Epoch, JsonSerialization.Deserialize<MetaTime>("\"1970-01-01T00:00:00.0000000Z\""));
            Assert.AreEqual(MetaTime.FromDateTime(new DateTime(2021, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc)), JsonSerialization.Deserialize<MetaTime>("\"2021-12-31T23:59:59.9990000Z\""));
        }

        [Test]
        public void SerializeMetaDuration()
        {
            Assert.AreEqual("\"0.00:00:00.0000000\"", JsonSerialization.SerializeToString(MetaDuration.Zero));
            Assert.AreEqual("\"0.00:00:00.0010000\"", JsonSerialization.SerializeToString(MetaDuration.FromMilliseconds(1)));
            Assert.AreEqual("\"0.00:00:01.0000000\"", JsonSerialization.SerializeToString(MetaDuration.FromSeconds(1)));
            Assert.AreEqual("\"0.00:01:00.0000000\"", JsonSerialization.SerializeToString(MetaDuration.FromMinutes(1)));
            Assert.AreEqual("\"0.01:00:00.0000000\"", JsonSerialization.SerializeToString(MetaDuration.FromHours(1)));
            Assert.AreEqual("\"111.01:00:00.0000000\"", JsonSerialization.SerializeToString(MetaDuration.FromHours(24 * 111 + 1)));
        }

        [Test]
        public void DeserializeMetaDuration()
        {
            Assert.AreEqual(MetaDuration.Zero, JsonSerialization.Deserialize<MetaDuration>("\"0.00:00:00.0000000\""));
            Assert.AreEqual(MetaDuration.FromMilliseconds(1), JsonSerialization.Deserialize<MetaDuration>("\"0.00:00:00.0010000\""));
            Assert.AreEqual(MetaDuration.FromSeconds(1), JsonSerialization.Deserialize<MetaDuration>("\"0.00:00:01.0000000\""));
            Assert.AreEqual(MetaDuration.FromMinutes(1), JsonSerialization.Deserialize<MetaDuration>("\"0.00:01:00.0000000\""));
            Assert.AreEqual(MetaDuration.FromHours(1), JsonSerialization.Deserialize<MetaDuration>("\"0.01:00:00.0000000\""));
            Assert.AreEqual(MetaDuration.FromHours(24 * 111 + 1), JsonSerialization.Deserialize<MetaDuration>("\"111.01:00:00.0000000\""));
        }

        public class NullCollectionSerializationTestType
        {
            public int[] IntArr = new int[] { 1, 2, 3 };
            [JsonSerializeNullCollectionAsEmpty]
            public int[] IntArrNcae = new int[] { 1, 2, 3 };

            public int[] EmptyIntArr = new int[] { };
            [JsonSerializeNullCollectionAsEmpty]
            public int[] EmptyIntArrNcae = new int[] { };

            public int[] NullIntArr = null;
            [JsonSerializeNullCollectionAsEmpty]
            public int[] NullIntArrNcae = null;

            public List<int> NullIntList = null;
            [JsonSerializeNullCollectionAsEmpty]
            public List<int> NullIntListNcae = null;

            public HashSet<int> IntSet = new() { 5 }; // just one as the order might change
            [JsonSerializeNullCollectionAsEmpty]
            public HashSet<int> IntSetNcae = new() { 5 };

            public HashSet<int> NullIntSet = null;
            [JsonSerializeNullCollectionAsEmpty]
            public HashSet<int> NullIntSetNcae = null;

            public MetaDictionary<string, string> Str2StrDict = new() { { "foo", "bar" },  { "fiz", "buz" } };
            [JsonSerializeNullCollectionAsEmpty]
            public MetaDictionary<string, string> Str2StrDictNcae = new() { { "foo", "bar" },  { "fiz", "buz" } };

            public MetaDictionary<string, string> EmptyStr2StrDict { get; } = new();
            [JsonSerializeNullCollectionAsEmpty]
            public MetaDictionary<string, string> EmptyStr2StrDictNcae { get; } = new();

            public MetaDictionary<string, string> NullStr2StrDict { get; } = null;
            [JsonSerializeNullCollectionAsEmpty]
            public MetaDictionary<string, string> NullStr2StrDictNcae { get; } = null;
        }

        [Test]
        public void SerializeJsonSerializeNullCollectionAsEmpty()
        {
            const string expected =
                "{\"intArr\":[1,2,3],"
                + "\"intArrNcae\":[1,2,3],"
                + "\"emptyIntArr\":[],"
                + "\"emptyIntArrNcae\":[],"
                + "\"nullIntArr\":null,"
                + "\"nullIntArrNcae\":[],"
                + "\"nullIntList\":null,"
                + "\"nullIntListNcae\":[],"
                + "\"intSet\":[5],"
                + "\"intSetNcae\":[5],"
                + "\"nullIntSet\":null,"
                + "\"nullIntSetNcae\":[],"
                + "\"str2StrDict\":{\"foo\":\"bar\",\"fiz\":\"buz\"},"
                + "\"str2StrDictNcae\":{\"foo\":\"bar\",\"fiz\":\"buz\"},"
                + "\"emptyStr2StrDict\":{},"
                + "\"emptyStr2StrDictNcae\":{},"
                + "\"nullStr2StrDict\":null,"
                + "\"nullStr2StrDictNcae\":{}"
                + "}";
            string actual = JsonSerialization.SerializeToString(new NullCollectionSerializationTestType());
            Assert.AreEqual(expected, actual);
        }

        public class NullCollectionDeserializationTestType
        {
            [JsonSerializeNullCollectionAsEmpty]
            public int[] Arr = new int[] {1, 2, 3};

            [JsonSerializeNullCollectionAsEmpty]
            public List<int> List = new() {1, 2, 3};

            [JsonSerializeNullCollectionAsEmpty]
            public MetaDictionary<string, string> Dict = new() { {"1", "2"} };
        }

        [Test]
        public void DeserializeJsonSerializeNullCollectionAsEmpty()
        {
            // Null-into-empty serialization does not affect deserialization
            NullCollectionDeserializationTestType testValue = JsonSerialization.Deserialize<NullCollectionDeserializationTestType>("{\"Arr\":null,\"List\":null,\"Dict\":null}");
            Assert.IsNull(testValue.Arr);
            Assert.IsNull(testValue.List);
            Assert.IsNull(testValue.Dict);
        }

        class SensitiveAttributeSerializationTestType
        {
            [Sensitive]
            public string A = "secret1";

            [Sensitive]
            public string B = null;

            [Sensitive]
            public string C { get; } = "secret2";
        }

        [Test]
        public void SerializeSensitiveAttribute()
        {
            Assert.AreEqual("{\"a\":\"XXX\",\"b\":null,\"c\":\"XXX\"}", JsonSerialization.SerializeToString(new SensitiveAttributeSerializationTestType()));
        }

        class SensitiveAttributeDeserializationTestType
        {
            [Sensitive]
            public string A = "default";
        }

        [Test]
        public void DeserializeSensitiveAttribute()
        {
            SensitiveAttributeDeserializationTestType testValue = JsonSerialization.Deserialize<SensitiveAttributeDeserializationTestType>("{\"A\":\"secret\"}");
            Assert.AreEqual("secret", testValue.A);
        }

        [Test]
        public void SerializeCalendarPeriod()
        {
            void Test(string expected, MetaCalendarPeriod period)
            {
                string serialized = JsonSerialization.SerializeToString(period);
                Assert.AreEqual(expected, serialized);
            }

            Test("\"P123Y456M789DT1230H4560M7890S\"", new MetaCalendarPeriod { Years = 123, Months = 456, Days = 789, Hours = 1230, Minutes = 4560, Seconds = 7890 });
            Test("\"P1Y2M3DT12H34M56S\"", new MetaCalendarPeriod { Years = 1, Months = 2, Days = 3, Hours = 12, Minutes = 34, Seconds = 56 });
            Test("\"P0Y0M0DT0H0M0S\"", new MetaCalendarPeriod { });
            Test("\"P0Y0M0DT0H0M1000S\"", new MetaCalendarPeriod { Seconds = 1000 });
            Test("\"P0Y0M0DT0H1000M0S\"", new MetaCalendarPeriod { Minutes = 1000 });
            Test("\"P0Y0M0DT1000H0M0S\"", new MetaCalendarPeriod { Hours = 1000 });
            Test("\"P0Y0M1000DT0H0M0S\"", new MetaCalendarPeriod { Days = 1000 });
            Test("\"P0Y1000M0DT0H0M0S\"", new MetaCalendarPeriod { Months = 1000 });
            Test("\"P1000Y0M0DT0H0M0S\"", new MetaCalendarPeriod { Years = 1000 });
            Test("\"P-123Y456M-789DT-1230H4560M-7890S\"", new MetaCalendarPeriod { Years = -123, Months = 456, Days = -789, Hours = -1230, Minutes = 4560, Seconds = -7890 });
            Test("\"P0Y0M0DT0H0M-1000S\"", new MetaCalendarPeriod { Seconds = -1000 });
            Test("\"P0Y0M0DT0H-1000M0S\"", new MetaCalendarPeriod { Minutes = -1000 });
            Test("\"P0Y0M0DT-1000H0M0S\"", new MetaCalendarPeriod { Hours = -1000 });
            Test("\"P0Y0M-1000DT0H0M0S\"", new MetaCalendarPeriod { Days = -1000 });
            Test("\"P0Y-1000M0DT0H0M0S\"", new MetaCalendarPeriod { Months = -1000 });
            Test("\"P-1000Y0M0DT0H0M0S\"", new MetaCalendarPeriod { Years = -1000 });
        }

        [Test]
        public void DeserializeCalendarPeriod()
        {
            void Test(string jsonPeriod, MetaCalendarPeriod expected)
            {
                MetaCalendarPeriod deserialized = JsonSerialization.Deserialize<MetaCalendarPeriod>(jsonPeriod);
                Assert.AreEqual(expected, deserialized);
            }

            Test("\"P123Y456M789DT1230H4560M7890S\"", new MetaCalendarPeriod { Years = 123, Months = 456, Days = 789, Hours = 1230, Minutes = 4560, Seconds = 7890 });
            Test("\"P1Y2M3DT12H34M56S\"", new MetaCalendarPeriod { Years = 1, Months = 2, Days = 3, Hours = 12, Minutes = 34, Seconds = 56 });
            Test("\"P0Y0M0DT0H0M0S\"", new MetaCalendarPeriod { });
            Test("\"P0Y0M0DT0H0M1000S\"", new MetaCalendarPeriod { Seconds = 1000 });
            Test("\"P0Y0M0DT0H1000M0S\"", new MetaCalendarPeriod { Minutes = 1000 });
            Test("\"P0Y0M0DT1000H0M0S\"", new MetaCalendarPeriod { Hours = 1000 });
            Test("\"P0Y0M1000DT0H0M0S\"", new MetaCalendarPeriod { Days = 1000 });
            Test("\"P0Y1000M0DT0H0M0S\"", new MetaCalendarPeriod { Months = 1000 });
            Test("\"P1000Y0M0DT0H0M0S\"", new MetaCalendarPeriod { Years = 1000 });
            Test("\"P-123Y456M-789DT-1230H4560M-7890S\"", new MetaCalendarPeriod { Years = -123, Months = 456, Days = -789, Hours = -1230, Minutes = 4560, Seconds = -7890 });
            Test("\"P0Y0M0DT0H0M-1000S\"", new MetaCalendarPeriod { Seconds = -1000 });
            Test("\"P0Y0M0DT0H-1000M0S\"", new MetaCalendarPeriod { Minutes = -1000 });
            Test("\"P0Y0M0DT-1000H0M0S\"", new MetaCalendarPeriod { Hours = -1000 });
            Test("\"P0Y0M-1000DT0H0M0S\"", new MetaCalendarPeriod { Days = -1000 });
            Test("\"P0Y-1000M0DT0H0M0S\"", new MetaCalendarPeriod { Months = -1000 });
            Test("\"P-1000Y0M0DT0H0M0S\"", new MetaCalendarPeriod { Years = -1000 });

            Test("\"P0Y0M0DT0H0M0S\"", new MetaCalendarPeriod { });
            Test("\"P0Y0DT0H0M\"", new MetaCalendarPeriod { });
            Test("\"P0YT0S\"", new MetaCalendarPeriod { });
            Test("\"P0Y\"", new MetaCalendarPeriod { });
            Test("\"P0M\"", new MetaCalendarPeriod { });
            Test("\"P0D\"", new MetaCalendarPeriod { });
            Test("\"PT0H\"", new MetaCalendarPeriod { });
            Test("\"PT0M\"", new MetaCalendarPeriod { });
            Test("\"PT0S\"", new MetaCalendarPeriod { });
            Test("\"PT-0S\"", new MetaCalendarPeriod { });

            Test("\"P123Y\"", new MetaCalendarPeriod { Years = 123 });
            Test("\"P123M\"", new MetaCalendarPeriod { Months = 123 });
            Test("\"P123D\"", new MetaCalendarPeriod { Days = 123 });
            Test("\"PT123H\"", new MetaCalendarPeriod { Hours = 123 });
            Test("\"PT123M\"", new MetaCalendarPeriod { Minutes = 123 });
            Test("\"PT123S\"", new MetaCalendarPeriod { Seconds = 123 });
            Test("\"P-123Y\"", new MetaCalendarPeriod { Years = -123 });
            Test("\"P-123M\"", new MetaCalendarPeriod { Months = -123 });
            Test("\"P-123D\"", new MetaCalendarPeriod { Days = -123 });
            Test("\"PT-123H\"", new MetaCalendarPeriod { Hours = -123 });
            Test("\"PT-123M\"", new MetaCalendarPeriod { Minutes = -123 });
            Test("\"PT-123S\"", new MetaCalendarPeriod { Seconds = -123 });
            Test("\"P1Y\"", new MetaCalendarPeriod { Years = 1 });
            Test("\"P1M\"", new MetaCalendarPeriod { Months = 1 });
            Test("\"P1D\"", new MetaCalendarPeriod { Days = 1 });
            Test("\"PT1H\"", new MetaCalendarPeriod { Hours = 1 });
            Test("\"PT1M\"", new MetaCalendarPeriod { Minutes = 1 });
            Test("\"PT1S\"", new MetaCalendarPeriod { Seconds = 1 });

            Test("\"P123Y789DT1230H4560M\"", new MetaCalendarPeriod { Years = 123, Days = 789, Hours = 1230, Minutes = 4560 });
            Test("\"P1Y2M\"", new MetaCalendarPeriod { Years = 1, Months = 2 });
            Test("\"P1Y2D\"", new MetaCalendarPeriod { Years = 1, Days = 2 });
            Test("\"P1YT2H\"", new MetaCalendarPeriod { Years = 1, Hours = 2 });
            Test("\"P1YT2M\"", new MetaCalendarPeriod { Years = 1, Minutes = 2 });
            Test("\"P1YT2S\"", new MetaCalendarPeriod { Years = 1, Seconds = 2 });
            Test("\"P1MT2M\"", new MetaCalendarPeriod { Months = 1, Minutes = 2 });
            Test("\"PT1H2M\"", new MetaCalendarPeriod { Hours = 1, Minutes = 2 });
            Test("\"PT1H2S\"", new MetaCalendarPeriod { Hours = 1, Seconds = 2 });
            Test("\"PT1M2S\"", new MetaCalendarPeriod { Minutes = 1, Seconds = 2 });
            Test("\"PT1H2M3S\"", new MetaCalendarPeriod { Hours = 1, Minutes = 2, Seconds = 3 });
        }

        [Test]
        public void DeserializeCalendarPeriodInvalid()
        {
            void Test(string jsonPeriod, Type exceptionType = null, string exceptionMessageSubstring = null)
            {
                Exception ex = Assert.Catch(() => JsonSerialization.Deserialize<MetaCalendarPeriod>(jsonPeriod));
                if (exceptionType != null)
                    Assert.IsAssignableFrom(expected: exceptionType, ex);
                if (exceptionMessageSubstring != null)
                    StringAssert.Contains(expected: exceptionMessageSubstring, actual: ex.Message);
            }

            Test("123");
            Test("null");
            Test("{}");
            Test("");

            Test("\"\"", typeof(FormatException));
            Test("\"123\"", typeof(FormatException));
            Test("\"123Y\"", typeof(FormatException));
            Test("\"T123H\"", typeof(FormatException));
            Test("\"P123\"", typeof(FormatException));
            Test("\"PT123\"", typeof(FormatException));
            Test("\"P\"", typeof(FormatException));
            Test("\"PT\"", typeof(FormatException));
            Test("\"P0\"", typeof(FormatException));
            Test("\"PT0\"", typeof(FormatException));
            Test("\"P1YT\"", typeof(FormatException));
            Test("\"P0Y0Y\"", typeof(FormatException));
            Test("\"P1Y2Y\"", typeof(FormatException));
            Test("\"P1M2Y\"", typeof(FormatException));
            Test("\"PT0S0S\"", typeof(FormatException));
            Test("\"PT1S2S\"", typeof(FormatException));
            Test("\"PT1S2M\"", typeof(FormatException));
            Test("\"PT1Y\"", typeof(FormatException));
            Test("\"PT1D\"", typeof(FormatException));
            Test("\"P1H\"", typeof(FormatException));
            Test("\"P2S\"", typeof(FormatException));
            Test("\"P1YT1Y\"", typeof(FormatException));
            Test("\"P1YT2HT2H\"", typeof(FormatException));
            Test("\"P1YTT2H\"", typeof(FormatException));
            Test("\"PY\"", typeof(FormatException));
            Test("\"PM\"", typeof(FormatException));
            Test("\"PD\"", typeof(FormatException));
            Test("\"PTH\"", typeof(FormatException));
            Test("\"PTM\"", typeof(FormatException));
            Test("\"PTS\"", typeof(FormatException));
            Test("\"P1YH\"", typeof(FormatException));
            Test("\"P1YD\"", typeof(FormatException));
            Test("\"PY1D\"", typeof(FormatException));
            Test("\"PT1HM\"", typeof(FormatException));
            Test("\"PT1HS\"", typeof(FormatException));
            Test("\"P 1Y T 2H\"", typeof(FormatException));
            Test("\"P 1YT2H\"", typeof(FormatException));
            Test("\"P1Y T 2H\"", typeof(FormatException));
            Test("\"P 1Y T2H\"", typeof(FormatException));
            Test("\"P1YT2H P1YT2H\"", typeof(FormatException));
            Test("\" P1YT2H\"", typeof(FormatException));
            Test("\"P1YT2H \"", typeof(FormatException));
            Test("\"-P1Y\"", typeof(FormatException));
            Test("\"P--1Y\"", typeof(FormatException));
            Test("\"P1Y-\"", typeof(FormatException));

            Test("\"P.5Y\"", typeof(FormatException));
            Test("\"P.5M\"", typeof(FormatException));
            Test("\"P.5D\"", typeof(FormatException));
            Test("\"PT.5H\"", typeof(FormatException));
            Test("\"PT.5M\"", typeof(FormatException));
            Test("\"PT.5S\"", typeof(FormatException));
            Test("\"P,5Y\"", typeof(FormatException));
            Test("\"P,5M\"", typeof(FormatException));
            Test("\"P,5D\"", typeof(FormatException));
            Test("\"PT,5H\"", typeof(FormatException));
            Test("\"PT,5M\"", typeof(FormatException));
            Test("\"PT,5S\"", typeof(FormatException));

            Test("\"P0.Y\"", typeof(FormatException));
            Test("\"P0.M\"", typeof(FormatException));
            Test("\"P0.D\"", typeof(FormatException));
            Test("\"PT0.H\"", typeof(FormatException));
            Test("\"PT0.M\"", typeof(FormatException));
            Test("\"PT0.S\"", typeof(FormatException));
            Test("\"P0,Y\"", typeof(FormatException));
            Test("\"P0,M\"", typeof(FormatException));
            Test("\"P0,D\"", typeof(FormatException));
            Test("\"PT0,H\"", typeof(FormatException));
            Test("\"PT0,M\"", typeof(FormatException));
            Test("\"PT0,S\"", typeof(FormatException));

            // \note These fractionals are permitted by ISO 8601, but MetaCalendarPeriod does not currently support them.
            Test("\"P0.5Y\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"P0.5M\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"P0.5D\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0.5H\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0.5M\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0.5S\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"P0,5Y\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"P0,5M\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"P0,5D\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0,5H\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0,5M\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
            Test("\"PT0,5S\"", typeof(NotSupportedException), "MetaCalendarPeriod does not support fractional components");
        }
    }
}

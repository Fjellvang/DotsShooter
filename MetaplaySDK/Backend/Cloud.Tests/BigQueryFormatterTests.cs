// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Google.Cloud.BigQuery.V2;
using Metaplay.Cloud.Analytics;
using Metaplay.Core;
using Metaplay.Core.Analytics;
using Metaplay.Core.Math;
using Metaplay.Core.Model;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Cloud.Tests
{
    public class BigQueryFormatterTests
    {
        #pragma warning disable CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'

        [MetaSerializable]
        public enum TestEnum
        {
            Zero = 0,
            One,
            Two,
        }

        [AnalyticsEvent(10_001, "TestEvent")]
        [AnalyticsEventCategory("Test")]
        public class TestBasicAnalyticsEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public class Element
            {
                [MetaMember(1)] public int Field;
            }

            [MetaSerializable]
            public abstract class BaseType
            {
                [MetaMember(1)] public int Field;
            }
            [MetaSerializableDerived(1)]
            public class ConcreteType : BaseType
            {
                [MetaMember(2)] public int ChildField = 2;
            }

            [MetaSerializable]
            [BigQueryAnalyticsFormatAttribute(BigQueryAnalyticsFormatMode.Ignore)]
            public class IgnoredClass
            {
                [MetaMember(1)] public int Field;
            }

            [MetaSerializable]
            [BigQueryAnalyticsFormatAttribute(BigQueryAnalyticsFormatMode.Ignore)]
            public struct IgnoredStruct
            {
                [MetaMember(1)] public int Field;
            }

            [MetaMember(1)] public int Int;
            [MetaMember(2)] public bool Bool;
            [MetaMember(3)] public float Float;
            [MetaMember(4)] public F32 F32;
            [MetaMember(5)] public string String = "string";
            [MetaMember(6)] public string Null = null;
            [MetaMember(7)] public MetaGuid Guid = new MetaGuid(MetaUInt128.One);
            [MetaMember(8)] public Element[] Array = new Element[] { new Element { Field = 1 }, new Element { Field = 2 } };
            [MetaMember(9)] public TestEnum Enum;

            [MetaMember(11)] public int? IntMaybeYes = 1;
            [MetaMember(12)] public bool? BoolMaybeYes = true;
            [MetaMember(13)] public float? FloatMaybeYes = 1.0f;
            [MetaMember(14)] public F32? F32MaybeYes = F32.One;
            [MetaMember(15)] public MetaGuid? GuidMaybeYes = new MetaGuid(MetaUInt128.One);
            [MetaMember(16)] public TestEnum? EnumMaybeYes = TestEnum.One;

            [MetaMember(21)] public int? IntMaybeNo;
            [MetaMember(22)] public bool? BoolMaybeNo;
            [MetaMember(23)] public float? FloatMaybeNo;
            [MetaMember(24)] public F32? F32MaybeNo;
            [MetaMember(25)] public MetaGuid? GuidMaybeNo;
            [MetaMember(26)] public TestEnum? EnumMaybeNo;

            [MetaMember(31)] public BaseType TypedNull;
            [MetaMember(32)] public BaseType TypedSpecific = new ConcreteType();
            [MetaMember(33)] public ConcreteType ConcreteNull;

            [BigQueryAnalyticsFormatAttribute(BigQueryAnalyticsFormatMode.Ignore)]
            [MetaMember(41)] public int IgnoredField;
            [BigQueryAnalyticsFormatAttribute(BigQueryAnalyticsFormatMode.Ignore)]
            [MetaMember(42)] public int IgnoredProperty { get; set; }
            [MetaMember(43)] public IgnoredClass IgnoredClassField = new IgnoredClass();
            [MetaMember(44)] public IgnoredStruct IgnoredStructField;
        }

        [AnalyticsEvent(10_002, "TestNamedEvent")]
        [AnalyticsEventCategory("Test")]
        [BigQueryAnalyticsNameAttribute("MyEventName")]
        public class TestNamedAnalyticsEvent : AnalyticsEventBase
        {
            [BigQueryAnalyticsNameAttribute("my_field")]
            [MetaMember(51)] public int NamedField;
            [BigQueryAnalyticsNameAttribute("my_property")]
            [MetaMember(52)] public int NamedProperty { get; set; }
        }

        [AnalyticsEvent(10_003, "TestIllegalRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class IllegalRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public class Recursive
            {
                [MetaMember(1)] public Recursive Child;
            }
            [MetaMember(1)] public Recursive Head;
        }

        [AnalyticsEvent(10_004, "TestIllegalCoRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class IllegalCoRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public class Left
            {
                [MetaMember(1)] public Right Right;
            }
            [MetaSerializable]
            public class Right
            {
                [MetaMember(1)] public Left Left;
            }
            [MetaMember(1)] public Right First;
        }

        [AnalyticsEvent(10_005, "TestIllegalTypeRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class IllegalTypeRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public abstract class Base
            {
            }
            [MetaSerializableDerived(1)]
            public class Derived1 : Base
            {
                [MetaMember(1)] public int Int;
            }
            [MetaSerializableDerived(2)]
            public class Derived2 : Base
            {
                [MetaSerializable]
                public class Container
                {
                    [MetaMember(1)] public Base Base;
                }
                [MetaMember(1)] public Container Field;
            }

            [MetaMember(1)] public Base Field;
        }

        [AnalyticsEvent(10_006, "TestSingleDirectRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class SingleDirectRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            [BigQueryAnalyticsRecursionLimit(1)]
            public class Recursive
            {
                [MetaMember(1)] public int Value;
                [MetaMember(2)] public Recursive Next;
            }
            [MetaMember(1)] public Recursive Head;
        }

        [AnalyticsEvent(10_007, "TestDoubleDirectRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class DoubleDirectRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            [BigQueryAnalyticsRecursionLimit(2)]
            public class Recursive
            {
                [MetaMember(1)] public int Value;
                [MetaMember(2)] public Recursive Next;
            }
            [MetaMember(1)] public Recursive Head;
        }

        // Regeression test for a known bug
        [AnalyticsEvent(10_008, "RecursiveEvent1")]
        [AnalyticsEventCategory("Test")]
        public class RecursiveEvent1 : AnalyticsEventBase
        {
            [MetaSerializable]
            [BigQueryAnalyticsRecursionLimit(1)]
            public class FooType
            {
                [MetaMember(1)] public string FooPayload = "fooPayload";
                [MetaMember(2)] public FooType Foo;
                [MetaMember(3)] public BarType Bar;
            }

            [MetaSerializable]
            public class BarType
            {
                [MetaMember(1)] public string BarPayload = "barPayload";
                [MetaMember(2)] public FooType Foo;
            }

            [MetaMember(1)] public FooType RootFoo;
            [MetaMember(2)] public BarType RootBar;
        }

        // Regeression test for a known bug
        [AnalyticsEvent(10_009, "RecursiveEvent2")]
        [AnalyticsEventCategory("Test")]
        public class RecursiveEvent2 : AnalyticsEventBase
        {
            [MetaSerializable]
            [BigQueryAnalyticsRecursionLimit(2)]
            public class FooType
            {
                [MetaMember(1)] public string FooPayload = "fooPayload";
                [MetaMember(2)] public FooType Foo;
                [MetaMember(3)] public BarType Bar;
            }

            [MetaSerializable]
            public class BarType
            {
                [MetaMember(1)] public string BarPayload = "barPayload";
                [MetaMember(2)] public FooType Foo;
            }

            [MetaMember(1)] public FooType RootFoo;
            [MetaMember(2)] public BarType RootBar;
        }

        // Test for a known reward-like use case
        [AnalyticsEvent(10_010, "RecursiveRewardEvent")]
        [AnalyticsEventCategory("Test")]
        public class RecursiveRewardEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public abstract class RewardBase
            {
            }
            [MetaSerializableDerived(1)]
            public class GoldReward : RewardBase
            {
                [MetaMember(1)] public int NumGold = 123;
            }
            [MetaSerializableDerived(2)]
            public class StringReward : RewardBase
            {
                [MetaMember(1)] public string Value = "Foo";
            }

            [MetaSerializableDerived(3)]
            [BigQueryAnalyticsRecursionLimit(1)]
            public class CompoundReward : RewardBase
            {
                [MetaMember(1)] public RewardBase[] Rewards;
            }

            [MetaMember(1)] public RewardBase Reward;
        }

        [AnalyticsEvent(10_011, "TestTooRecursiveEvent")]
        [AnalyticsEventCategory("Test")]
        public class TooRecursiveEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            [BigQueryAnalyticsRecursionLimit(10)]
            public class ExponentialRecursive
            {
                [MetaMember(1)] public int Value;
                [MetaMember(2)] public ExponentialRecursive Left;
                [MetaMember(3)] public ExponentialRecursive Right;
            }
            [MetaMember(1)] public ExponentialRecursive Head;
        }

        [AnalyticsEvent(10_012, "TestExtractedFieldsAnalyticsEvent")]
        [AnalyticsEventCategory("Test")]
        public class TestExtractedFieldsAnalyticsEvent : AnalyticsEventBase
        {
            [MetaSerializable]
            public class MyKeyValue<TKey, TValue>
            {
                [MetaMember(1)] public TKey Key;
                [MetaMember(2)] public TValue Value;
            }

            [BigQueryAnalyticsFormat(BigQueryAnalyticsFormatMode.ExtractDictionaryElements)]
            [MetaMember(1)] public Dictionary<string, int> DictionaryInts;

            [BigQueryAnalyticsFormat(BigQueryAnalyticsFormatMode.ExtractDictionaryElements)]
            [MetaMember(2)] public MetaDictionary<string, float> DictionaryFloat;

            [BigQueryAnalyticsFormat(BigQueryAnalyticsFormatMode.ExtractDictionaryElements)]
            [MetaMember(3)] public MyKeyValue<string, string>[] KVArray;

            [MetaMember(4)] public MetaDictionary<string, string> UnannotatedDictionary;
        }

        #pragma warning restore CS0649 // Field 'field' is never assigned to, and will always have its default value 'value'

        class TestBufferSink : IBigQueryBatchWriter.BatchBuffer
        {
        }

        [MetaSerializableDerived(12000)]
        public class TestPlayerAnalyticsContext : PlayerAnalyticsContext
        {
            [BigQueryAnalyticsName("context_one")]
            [MetaMember(201)]
            public int ContextOne = 1;

            [BigQueryAnalyticsFormat(BigQueryAnalyticsFormatMode.Ignore)]
            [MetaMember(202)]
            public int ContextIgnored = 2;

            public TestPlayerAnalyticsContext() : base(sessionNumber: 12, experiments: new MetaDictionary<string, string>())
            {
            }
        }

        [Test]
        public void TestBasicTypes()
        {
            string result = FormatEvent(new TestBasicAnalyticsEvent());
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: TestBasicAnalyticsEvent
                event_schema_version: 1
                event_params: [
                key: Int
                int_value: 0

                key: Bool
                int_value: 0

                key: Float
                double_value: 0

                key: F32
                double_value: 0

                key: String
                string_value: string

                key: Null

                key: Guid
                string_value: 000000000000000-0-0000000000000001

                key: Array:0:Field
                int_value: 1

                key: Array:1:Field
                int_value: 2

                key: Enum
                string_value: Zero

                key: IntMaybeYes
                int_value: 1

                key: BoolMaybeYes
                int_value: 1

                key: FloatMaybeYes
                double_value: 1

                key: F32MaybeYes
                double_value: 1

                key: GuidMaybeYes
                string_value: 000000000000000-0-0000000000000001

                key: EnumMaybeYes
                string_value: One

                key: IntMaybeNo

                key: BoolMaybeNo

                key: FloatMaybeNo

                key: F32MaybeNo

                key: GuidMaybeNo

                key: EnumMaybeNo

                key: TypedSpecific:$t
                string_value: BigQueryFormatterTests.TestBasicAnalyticsEvent.ConcreteType

                key: TypedSpecific:Field
                int_value: 0

                key: TypedSpecific:ChildField
                int_value: 2

                key: ConcreteNull:Field

                key: ConcreteNull:ChildField
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestNamed()
        {
            string result = FormatEvent(new TestNamedAnalyticsEvent());
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: MyEventName
                event_schema_version: 1
                event_params: [
                key: my_field
                int_value: 0

                key: my_property
                int_value: 0
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestContext()
        {
            {
                TestPlayerAnalyticsContext context = new TestPlayerAnalyticsContext();
                string result = FormatEvent(new TestNamedAnalyticsEvent(), context: context, addContextFieldsAsExtraEventParams: false);
                string expected = """
                    source_id: None
                    event_id: 00000000000000000000000000000001
                    event_timestamp: 1970-01-01 00:00:00.000000
                    event_name: MyEventName
                    event_schema_version: 1
                    player:
                    session_number: 12
                    experiments: []

                    event_params: [
                    key: my_field
                    int_value: 0

                    key: my_property
                    int_value: 0
                    ]
                    labels: []

                    """;
                Assert.AreEqual(expected, result);
            }
            {
                TestPlayerAnalyticsContext context = new TestPlayerAnalyticsContext();
                string result = FormatEvent(new TestNamedAnalyticsEvent(), context: context, addContextFieldsAsExtraEventParams: true);
                string expected = """
                    source_id: None
                    event_id: 00000000000000000000000000000001
                    event_timestamp: 1970-01-01 00:00:00.000000
                    event_name: MyEventName
                    event_schema_version: 1
                    player:
                    session_number: 12
                    experiments: []

                    event_params: [
                    key: my_field
                    int_value: 0

                    key: my_property
                    int_value: 0

                    key: $c:context_one
                    int_value: 1
                    ]
                    labels: []

                    """;
                Assert.AreEqual(expected, result);
            }
        }

        [Test]
        public void TestIllegalRecursiveEvent()
        {
            Exception ex = Assert.Catch(() =>
            {
                _ = FormatEvent(new IllegalRecursiveEvent());
            });
            Assert.That(ex != null && ex.ToString().Contains("BigQueryAnalyticsRecursionLimitAttribute"));
        }

        [Test]
        public void TestIllegalCoRecursiveEvent()
        {
            Exception ex = Assert.Catch(() =>
            {
                _ = FormatEvent(new IllegalCoRecursiveEvent());
            });
            Assert.That(ex != null && ex.ToString().Contains("BigQueryAnalyticsRecursionLimitAttribute"));
        }

        [Test]
        public void TestIllegalTypeRecursiveEvent()
        {
            Exception ex = Assert.Catch(() =>
            {
                _ = FormatEvent(new IllegalTypeRecursiveEvent());
            });
            Assert.That(ex != null && ex.ToString().Contains("BigQueryAnalyticsRecursionLimitAttribute"));
        }

        [Test]
        public void TestSingleDirectRecursiveEvent()
        {
            SingleDirectRecursiveEvent analyticsEvent = new SingleDirectRecursiveEvent()
            {
                Head = new SingleDirectRecursiveEvent.Recursive()
                {
                    Value = 1,
                    Next = new SingleDirectRecursiveEvent.Recursive()
                    {
                        Value = 2
                    }
                }
            };

            string result = FormatEvent(analyticsEvent);
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: SingleDirectRecursiveEvent
                event_schema_version: 1
                event_params: [
                key: Head:Value
                int_value: 1

                key: Head:Next
                string_value: __recursion_limit
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestDoubleDirectRecursiveEvent()
        {
            DoubleDirectRecursiveEvent analyticsEvent = new DoubleDirectRecursiveEvent()
            {
                Head = new DoubleDirectRecursiveEvent.Recursive()
                {
                    Value = 1,
                    Next = new DoubleDirectRecursiveEvent.Recursive()
                    {
                        Value = 2,
                        Next = new DoubleDirectRecursiveEvent.Recursive()
                        {
                            Value = 3
                        }
                    }
                }
            };

            string result = FormatEvent(analyticsEvent);
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: DoubleDirectRecursiveEvent
                event_schema_version: 1
                event_params: [
                key: Head:Value
                int_value: 1

                key: Head:Next:Value
                int_value: 2

                key: Head:Next:Next
                string_value: __recursion_limit
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestRecursiveEvent1()
        {
            RecursiveEvent1 analyticsEvent = new RecursiveEvent1()
            {
                RootFoo = new RecursiveEvent1.FooType()
                {
                    Foo = new RecursiveEvent1.FooType()
                    {
                        Bar = new RecursiveEvent1.BarType()
                        {
                            Foo = new RecursiveEvent1.FooType()
                            {
                            }
                        }
                    },
                    Bar = new RecursiveEvent1.BarType()
                    {
                        Foo = new RecursiveEvent1.FooType()
                        {
                            Foo = new RecursiveEvent1.FooType()
                            {
                            },
                            Bar = new RecursiveEvent1.BarType()
                            {
                            }
                        }
                    }
                },
                RootBar = new RecursiveEvent1.BarType()
                {
                    Foo = new RecursiveEvent1.FooType()
                    {
                        Foo = new RecursiveEvent1.FooType()
                        {
                        },
                        Bar = new RecursiveEvent1.BarType()
                        {
                            Foo = new RecursiveEvent1.FooType()
                            {
                            },
                        }
                    }
                }
            };

            string result = FormatEvent(analyticsEvent);
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: RecursiveEvent1
                event_schema_version: 1
                event_params: [
                key: RootFoo:FooPayload
                string_value: fooPayload

                key: RootFoo:Foo
                string_value: __recursion_limit

                key: RootFoo:Bar:BarPayload
                string_value: barPayload

                key: RootFoo:Bar:Foo
                string_value: __recursion_limit

                key: RootBar:BarPayload
                string_value: barPayload

                key: RootBar:Foo:FooPayload
                string_value: fooPayload

                key: RootBar:Foo:Foo
                string_value: __recursion_limit

                key: RootBar:Foo:Bar:BarPayload
                string_value: barPayload

                key: RootBar:Foo:Bar:Foo
                string_value: __recursion_limit
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestRecursiveEvent2()
        {
            RecursiveEvent2 analyticsEvent = new RecursiveEvent2()
            {
                RootFoo = new RecursiveEvent2.FooType()
                {
                    Foo = new RecursiveEvent2.FooType()
                    {
                        Bar = new RecursiveEvent2.BarType()
                        {
                            Foo = new RecursiveEvent2.FooType()
                            {
                            }
                        }
                    },
                    Bar = new RecursiveEvent2.BarType()
                    {
                        Foo = new RecursiveEvent2.FooType()
                        {
                            Foo = new RecursiveEvent2.FooType()
                            {
                            },
                            Bar = new RecursiveEvent2.BarType()
                            {
                            }
                        }
                    }
                },
                RootBar = new RecursiveEvent2.BarType()
                {
                    Foo = new RecursiveEvent2.FooType()
                    {
                        Foo = new RecursiveEvent2.FooType()
                        {
                        },
                        Bar = new RecursiveEvent2.BarType()
                        {
                            Foo = new RecursiveEvent2.FooType()
                            {
                            },
                        }
                    }
                }
            };

            string result = FormatEvent(analyticsEvent);
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: RecursiveEvent2
                event_schema_version: 1
                event_params: [
                key: RootFoo:FooPayload
                string_value: fooPayload

                key: RootFoo:Foo:FooPayload
                string_value: fooPayload

                key: RootFoo:Foo:Foo
                string_value: __recursion_limit

                key: RootFoo:Foo:Bar:BarPayload
                string_value: barPayload

                key: RootFoo:Foo:Bar:Foo
                string_value: __recursion_limit

                key: RootFoo:Bar:BarPayload
                string_value: barPayload

                key: RootFoo:Bar:Foo:FooPayload
                string_value: fooPayload

                key: RootFoo:Bar:Foo:Foo
                string_value: __recursion_limit

                key: RootFoo:Bar:Foo:Bar:BarPayload
                string_value: barPayload

                key: RootFoo:Bar:Foo:Bar:Foo
                string_value: __recursion_limit

                key: RootBar:BarPayload
                string_value: barPayload

                key: RootBar:Foo:FooPayload
                string_value: fooPayload

                key: RootBar:Foo:Foo:FooPayload
                string_value: fooPayload

                key: RootBar:Foo:Foo:Foo
                string_value: __recursion_limit

                key: RootBar:Foo:Foo:Bar:BarPayload

                key: RootBar:Foo:Foo:Bar:Foo
                string_value: __recursion_limit

                key: RootBar:Foo:Bar:BarPayload
                string_value: barPayload

                key: RootBar:Foo:Bar:Foo:FooPayload
                string_value: fooPayload

                key: RootBar:Foo:Bar:Foo:Foo
                string_value: __recursion_limit

                key: RootBar:Foo:Bar:Foo:Bar:BarPayload

                key: RootBar:Foo:Bar:Foo:Bar:Foo
                string_value: __recursion_limit
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestRecursiveRewardEvent()
        {
            RecursiveRewardEvent analyticsEvent = new RecursiveRewardEvent()
            {
                Reward = new RecursiveRewardEvent.CompoundReward()
                {
                    Rewards = new RecursiveRewardEvent.RewardBase[]
                    {
                        new RecursiveRewardEvent.GoldReward() { NumGold = 1 },
                        new RecursiveRewardEvent.CompoundReward()
                        {
                            Rewards = new RecursiveRewardEvent.RewardBase[]
                            {
                                new RecursiveRewardEvent.StringReward() { Value = "wont_be_seen" },
                            }
                        }
                    }
                }
            };

            string result = FormatEvent(analyticsEvent);
            string expected = """
                source_id: None
                event_id: 00000000000000000000000000000001
                event_timestamp: 1970-01-01 00:00:00.000000
                event_name: RecursiveRewardEvent
                event_schema_version: 1
                event_params: [
                key: Reward:$t
                string_value: BigQueryFormatterTests.RecursiveRewardEvent.CompoundReward

                key: Reward:Rewards:0:$t
                string_value: BigQueryFormatterTests.RecursiveRewardEvent.GoldReward

                key: Reward:Rewards:0:NumGold
                int_value: 1

                key: Reward:Rewards:1
                string_value: __recursion_limit
                ]
                labels: []

                """;
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void TestTooRecursiveEvent()
        {
            Assert.Catch(() =>
            {
                _ = FormatEvent(new TooRecursiveEvent());
            });
        }

        [Test]
        public void TestExtracted()
        {
            {
                string result = FormatEvent(new TestExtractedFieldsAnalyticsEvent()
                {
                    DictionaryInts = new Dictionary<string, int>() { { "field_1", 1 }, { "field_2", 2 } },
                    DictionaryFloat = new MetaDictionary<string, float> { { "field_1", 1.0f }, { "field_2", 2.0f } },
                    KVArray = new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>[]
                    {
                        new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>() { Key = "key_1", Value = "value1" },
                        new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>() { Key = "key_2", Value = "value2" }
                    }
                });
                string expected = """
                    source_id: None
                    event_id: 00000000000000000000000000000001
                    event_timestamp: 1970-01-01 00:00:00.000000
                    event_name: TestExtractedFieldsAnalyticsEvent
                    event_schema_version: 1
                    event_params: [
                    key: DictionaryInts:field_1
                    int_value: 1

                    key: DictionaryInts:field_2
                    int_value: 2

                    key: DictionaryFloat:field_1
                    double_value: 1

                    key: DictionaryFloat:field_2
                    double_value: 2

                    key: KVArray:key_1
                    string_value: value1

                    key: KVArray:key_2
                    string_value: value2
                    ]
                    labels: []

                    """;
                Assert.AreEqual(expected, result);
            }
            {
                string result = FormatEvent(new TestExtractedFieldsAnalyticsEvent()
                {
                    DictionaryInts = null,
                    DictionaryFloat = new MetaDictionary<string, float> { { null, 1.0f }, { "field_2", 2.0f } },
                    KVArray = new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>[]
                    {
                        new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>() { Key = null, Value = "value1" },
                        null,
                        new TestExtractedFieldsAnalyticsEvent.MyKeyValue<string, string>() { Key = "key_2", Value = "value2" }
                    }
                });
                string expected = """
                    source_id: None
                    event_id: 00000000000000000000000000000001
                    event_timestamp: 1970-01-01 00:00:00.000000
                    event_name: TestExtractedFieldsAnalyticsEvent
                    event_schema_version: 1
                    event_params: [
                    key: DictionaryFloat:field_2
                    double_value: 2

                    key: KVArray:key_2
                    string_value: value2
                    ]
                    labels: []

                    """;
                Assert.AreEqual(expected, result);
            }
            {
                string result = FormatEvent(new TestExtractedFieldsAnalyticsEvent()
                {
                    UnannotatedDictionary = new MetaDictionary<string, string>()
                    {
                        { "key_1", "value_1" },
                        { null, "value_null" },
                        { "key_2", "value_2" },
                    }
                });
                string expected = """
                    source_id: None
                    event_id: 00000000000000000000000000000001
                    event_timestamp: 1970-01-01 00:00:00.000000
                    event_name: TestExtractedFieldsAnalyticsEvent
                    event_schema_version: 1
                    event_params: [
                    key: UnannotatedDictionary:0:key
                    string_value: key_1

                    key: UnannotatedDictionary:0:value
                    string_value: value_1

                    key: UnannotatedDictionary:1:key

                    key: UnannotatedDictionary:1:value
                    string_value: value_null

                    key: UnannotatedDictionary:2:key
                    string_value: key_2

                    key: UnannotatedDictionary:2:value
                    string_value: value_2
                    ]
                    labels: []

                    """;
                Assert.AreEqual(expected, result);
            }
        }

        string FormatEvent<T>(T eventObject, AnalyticsContextBase context = null, bool addContextFieldsAsExtraEventParams = false) where T : AnalyticsEventBase
        {
            AnalyticsEventSpec spec = new AnalyticsEventSpec(typeof(T), typeof(T).GetCustomAttribute<AnalyticsEventAttribute>(), typeof(T).GetCustomAttribute<AnalyticsEventCategoryAttribute>());
            BigQueryFormatter formatter = new BigQueryFormatter(new []{ spec }, addContextFieldsAsExtraEventParams);

            TestBufferSink sink = new TestBufferSink();
            formatter.WriteEvent(sink, new AnalyticsEventEnvelope(EntityId.None, MetaTime.Epoch, MetaTime.Epoch, MetaUInt128.One, "TestEvent", 1, eventObject, context, new MetaDictionary<AnalyticsLabel, string>()), enableBigQueryEventDeduplication: false);

            Assert.AreEqual(1, sink.NumRows);
            return RowToString(sink.Rows[0]);
        }

        static string RowToString(BigQueryInsertRow row)
        {
            string result = "";
            foreach (KeyValuePair<string, object> item in row)
            {
                string value = ValueToString(item.Value);
                result += item.Key + ":";
                if (value != "" && !value.StartsWith('\n'))
                        result += " ";
                result += value + "\n";
            }
            return result;
        }

        static string ValueToString(object value)
        {
            if (value is null)
            {
                return "null";
            }
            else if (value is BigQueryInsertRow innerRow)
            {
                return ValueToString("\n" + RowToString(innerRow));
            }
            else if (value is string s)
            {
                return s;
            }
            else if (value is IEnumerable enumerable)
            {
                string result = "[";
                foreach (var elem in enumerable)
                    result += ValueToString(elem);
                return result += "]";
            }
            else
                return Util.ObjectToStringInvariant(value);
        }
    }
}

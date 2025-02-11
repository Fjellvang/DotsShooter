// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Metaplay.Core.Tests
{
    [TestFixture]
    public class DynamicEnumTests
    {
        [MetaSerializable]
        public class TestDynamicEnum : DynamicEnum<TestDynamicEnum>
        {
            public static readonly TestDynamicEnum TestValue0 = new TestDynamicEnum(0, nameof(TestValue0));
            public static readonly TestDynamicEnum TestValue1 = new TestDynamicEnum(1, nameof(TestValue1));

            public static TestDynamicEnum CreateTestValue(int value)
            {
                return new TestDynamicEnum(value, "dummy");
            }

            public TestDynamicEnum(int value, string name) : base(value, name, isValid: true)
            {
            }
        }

        [Test]
        public void Comparisons()
        {
            #pragma warning disable CS1718 // "Comparison made to same variable; did you mean to compare something else?"
            Assert.True(TestDynamicEnum.TestValue0 == TestDynamicEnum.TestValue0);
            Assert.True(TestDynamicEnum.TestValue1 == TestDynamicEnum.TestValue1);
            #pragma warning restore CS1718
            Assert.True(TestDynamicEnum.TestValue0 == TestDynamicEnum.CreateTestValue(0));
            Assert.True(TestDynamicEnum.CreateTestValue(0) == TestDynamicEnum.TestValue0);
            Assert.False(TestDynamicEnum.TestValue0 == TestDynamicEnum.TestValue1);
            Assert.True(TestDynamicEnum.TestValue1 == TestDynamicEnum.CreateTestValue(1));
            Assert.True(TestDynamicEnum.CreateTestValue(1) == TestDynamicEnum.TestValue1);
            Assert.False(TestDynamicEnum.TestValue1 == TestDynamicEnum.TestValue0);
            Assert.False(TestDynamicEnum.TestValue0 == null);
            Assert.False(null == TestDynamicEnum.TestValue0);
            Assert.True((TestDynamicEnum)null == (TestDynamicEnum)null);

            #pragma warning disable CS1718 // "Comparison made to same variable; did you mean to compare something else?"
            Assert.False(TestDynamicEnum.TestValue0 != TestDynamicEnum.TestValue0);
            Assert.False(TestDynamicEnum.TestValue1 != TestDynamicEnum.TestValue1);
            #pragma warning restore CS1718
            Assert.True(TestDynamicEnum.TestValue0 != TestDynamicEnum.TestValue1);
            Assert.True(TestDynamicEnum.TestValue1 != TestDynamicEnum.TestValue0);
            Assert.True(TestDynamicEnum.TestValue0 != null);
            Assert.True(null != TestDynamicEnum.TestValue0);
            Assert.False((TestDynamicEnum)null != (TestDynamicEnum)null);

            Assert.True(((object)TestDynamicEnum.TestValue0).Equals(TestDynamicEnum.TestValue0));
            Assert.False(((object)TestDynamicEnum.TestValue0).Equals(TestDynamicEnum.TestValue1));
            Assert.False(((object)TestDynamicEnum.TestValue1).Equals(TestDynamicEnum.TestValue0));
            Assert.False(((object)TestDynamicEnum.TestValue0).Equals(null));

            Assert.True(((IEquatable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).Equals(TestDynamicEnum.TestValue0));
            Assert.False(((IEquatable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).Equals(TestDynamicEnum.TestValue1));
            Assert.False(((IEquatable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue1).Equals(TestDynamicEnum.TestValue0));
            Assert.False(((IEquatable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).Equals(null));

            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).CompareTo(TestDynamicEnum.TestValue0) == 0);
            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).CompareTo(TestDynamicEnum.TestValue1) < 0);
            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue0).CompareTo(null) > 0);
            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue1).CompareTo(TestDynamicEnum.TestValue1) == 0);
            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue1).CompareTo(TestDynamicEnum.TestValue0) > 0);
            Assert.True(((IComparable<DynamicEnum<TestDynamicEnum>>)TestDynamicEnum.TestValue1).CompareTo(null) > 0);

            Assert.True(((IComparable)TestDynamicEnum.TestValue0).CompareTo(TestDynamicEnum.TestValue0) == 0);
            Assert.True(((IComparable)TestDynamicEnum.TestValue0).CompareTo(TestDynamicEnum.TestValue1) < 0);
            Assert.True(((IComparable)TestDynamicEnum.TestValue0).CompareTo(null) > 0);
            Assert.True(((IComparable)TestDynamicEnum.TestValue1).CompareTo(TestDynamicEnum.TestValue1) == 0);
            Assert.True(((IComparable)TestDynamicEnum.TestValue1).CompareTo(TestDynamicEnum.TestValue0) > 0);
            Assert.True(((IComparable)TestDynamicEnum.TestValue1).CompareTo(null) > 0);
        }

        [MetaSerializable]
        [DynamicEnumFactory(nameof(GenerateInstances))]
        public class TestDynamicEnumWithFactory : DynamicEnum<TestDynamicEnumWithFactory>
        {
            protected TestDynamicEnumWithFactory(int value, string name) : base(value, name, isValid: true)
            {
            }

            static IEnumerable<TestDynamicEnumWithFactory> GenerateInstances()
            {
                return new TestDynamicEnumWithFactory[]
                {
                    new TestDynamicEnumWithFactory(1, "TestValue0"),
                    new TestDynamicEnumWithFactory(2, "TestValue1"),
                };
            }
        }

        [MetaSerializable]
        [DynamicEnumFactory(nameof(GenerateInstances))]
        public sealed class TestDynamicEnumWithFactoryDerived : TestDynamicEnumWithFactory
        {
            TestDynamicEnumWithFactoryDerived(int value, string name) : base(value, name)
            {
            }

            static IEnumerable<TestDynamicEnumWithFactory> GenerateInstances()
            {
                return new TestDynamicEnumWithFactory[]
                {
                    new TestDynamicEnumWithFactoryDerived(3, "TestValue2"),
                    new TestDynamicEnumWithFactoryDerived(4, "TestValue3"),
                };
            }
        }

        [MetaSerializable]
        [DynamicEnumFactory(nameof(GenerateInstances))]
        public class TestDynamicEnumWithFactoryAndMembers : DynamicEnum<TestDynamicEnumWithFactoryAndMembers>
        {
            public static TestDynamicEnumWithFactoryAndMembers IllegalMember = new TestDynamicEnumWithFactoryAndMembers(3, "TestValue2");

            TestDynamicEnumWithFactoryAndMembers(int value, string name) : base(value, name, isValid: true)
            {
            }

            static IEnumerable<TestDynamicEnumWithFactoryAndMembers> GenerateInstances()
            {
                return new TestDynamicEnumWithFactoryAndMembers[]
                {
                    new TestDynamicEnumWithFactoryAndMembers(1, "TestValue0"),
                    new TestDynamicEnumWithFactoryAndMembers(2, "TestValue1"),
                };
            }
        }

        [Test]
        public void FactoryTest()
        {
            Assert.True(TestDynamicEnumWithFactory.AllValues.Count == 4);
            Assert.True(TestDynamicEnumWithFactory.FromId(1).Name == "TestValue0");
            Assert.True(TestDynamicEnumWithFactory.FromId(2).Name == "TestValue1");
            Assert.True(TestDynamicEnumWithFactory.FromId(3).Name == "TestValue2");
            Assert.True(TestDynamicEnumWithFactory.FromId(4).Name == "TestValue3");

            InvalidOperationException ex = Assert.Catch<InvalidOperationException>(() => _ = TestDynamicEnumWithFactoryAndMembers.AllValues);
            Assert.True(ex.Message.Contains("has both a factory method"));
        }
    }
}

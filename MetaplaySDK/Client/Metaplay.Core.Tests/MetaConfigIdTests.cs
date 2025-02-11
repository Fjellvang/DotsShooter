using Metaplay.Core.Config;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static System.FormattableString;

namespace Metaplay.Core.Tests
{
    public class MetaConfigIdTests
    {
        public class TestIntItem : IGameConfigData<int>
        {
            public int ConfigKey { get; set; }
            public override string ToString() => Invariant($"TestIntItem({ConfigKey})");
        }

        public class TestStringItem : IGameConfigData<string>
        {
            public string ConfigKey { get; set; }
            public override string ToString() => $"TestStringItem({ConfigKey})";
        }

        public class TestDerivedStringItem : TestStringItem
        {
            public override string ToString() => $"TestDerivedStringItem({ConfigKey})";
        }

        [Test]
        public void TestMetaConfigIdEquality()
        {
            Resolver resolver = new Resolver();
            resolver.Add(1, new TestIntItem { ConfigKey = 1 });
            resolver.Add(2, new TestIntItem { ConfigKey = 2 });
            resolver.Add("a", new TestStringItem { ConfigKey = "a" });
            resolver.Add("b", new TestStringItem { ConfigKey = "b" });
            resolver.Add("c", new TestDerivedStringItem { ConfigKey = "c" });

            // Test integer item
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            Assert.AreEqual(typeof(TestIntItem), metaConfigId.ItemType);
            Assert.AreEqual(1, metaConfigId.KeyObject);

            // Test equality of references
            MetaConfigId<TestIntItem> metaConfigId2 = new MetaConfigId<TestIntItem>(1);
            Assert.AreEqual(typeof(TestIntItem), metaConfigId2.ItemType);
            Assert.AreEqual(1, metaConfigId2.KeyObject);
            Assert.AreEqual(metaConfigId2, metaConfigId);

            // Test that the item can be resolved
            Assert.DoesNotThrow(() => metaConfigId.GetItem(resolver));
            TestIntItem item = metaConfigId.GetItem(resolver);
            Assert.AreEqual(1, item.ConfigKey);

            // Test string item
            MetaConfigId<TestStringItem> metaConfigId3 = new MetaConfigId<TestStringItem>("b");
            Assert.AreEqual(typeof(TestStringItem), metaConfigId3.ItemType);
            Assert.AreEqual("b", metaConfigId3.KeyObject);
            
            // Test that the item can be resolved
            Assert.DoesNotThrow(() => metaConfigId3.GetItem(resolver));
            TestStringItem item2 = metaConfigId3.GetItem(resolver);
            Assert.AreEqual("b", item2.ConfigKey);

            // Test derived string item
            MetaConfigId<TestDerivedStringItem> metaConfigId4 = new MetaConfigId<TestDerivedStringItem>("b");
            Assert.AreEqual(typeof(TestDerivedStringItem), metaConfigId4.ItemType);
            Assert.AreEqual("b", metaConfigId4.KeyObject);

            // Test equality of references with different types that are assignable from one to the other
            Assert.AreEqual(metaConfigId3, metaConfigId4);
        }

        [Test]
        public void TestMetaConfigIdFromKey()
        {
            MetaConfigId<TestIntItem> metaConfigId = MetaConfigId<TestIntItem>.FromKey(1);
            Assert.AreEqual(typeof(TestIntItem), metaConfigId.ItemType);
            Assert.AreEqual(1, metaConfigId.KeyObject);
        }

        [Test]
        public void TestMetaConfigIdCheckKeyValidity()
        {
            Assert.Throws<ArgumentNullException>(() => new MetaConfigId<TestIntItem>(null));
            Assert.Throws<InvalidOperationException>(() => new MetaConfigId<TestIntItem>("a"));
        }

        [Test]
        public void TestMetaConfigIdToString()
        {
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            Assert.AreEqual("1", metaConfigId.ToString());
        }

        [Test]
        public void TestMetaConfigIdEquals()
        {
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId2 = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId3 = new MetaConfigId<TestIntItem>(2);
            MetaConfigId<TestStringItem> metaConfigId4 = new MetaConfigId<TestStringItem>("a");

            Assert.AreEqual(metaConfigId, metaConfigId2);
            Assert.AreNotEqual(metaConfigId, metaConfigId3);
            Assert.AreNotEqual(metaConfigId, metaConfigId4);
            Assert.AreNotEqual(metaConfigId, null);
        }

        [Test]
        public void TestMetaConfigIdGetHashCode()
        {
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            Assert.AreEqual(1.GetHashCode(), metaConfigId.GetHashCode());
        }

        [Test]
        public void TestMetaConfigIdOperatorEquals()
        {
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId2 = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId3 = new MetaConfigId<TestIntItem>(2);

            Assert.IsTrue(metaConfigId == metaConfigId2);
            Assert.IsFalse(metaConfigId == metaConfigId3);
        }

        [Test]
        public void TestMetaConfigIdOperatorNotEquals()
        {
            MetaConfigId<TestIntItem> metaConfigId = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId2 = new MetaConfigId<TestIntItem>(1);
            MetaConfigId<TestIntItem> metaConfigId3 = new MetaConfigId<TestIntItem>(2);

            Assert.IsFalse(metaConfigId != metaConfigId2);
            Assert.IsTrue(metaConfigId != metaConfigId3);
        }

        static object GetConfigKey(IGameConfigData item)
        {
            return item
                .GetType()
                .GetGenericInterface(typeof(IHasGameConfigKey<>))
                .GetProperty("ConfigKey")
                .GetValue(item);
        }
        
        class Resolver : IGameConfigDataResolver
        {
            Dictionary<(Type, object), IGameConfigData> _items = new Dictionary<(Type, object), IGameConfigData>();

            public Resolver(params IGameConfigData[] items)
            {
                foreach (IGameConfigData item in items)
                    Add(item);
            }

            public void Add(IGameConfigData item)
            {
                Add(GetConfigKey(item), item);
            }

            public void Add(object configKey, IGameConfigData item)
            {
                _items.Add(
                    (item.GetType(), configKey),
                    item);
            }

            public object TryResolveReference(Type type, object configKey)
                => _items.GetValueOrDefault((type, configKey), null);
        }
    }
}

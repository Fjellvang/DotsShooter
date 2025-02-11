// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Serialization;
using NUnit.Framework;
using System;

namespace Cloud.Tests
{
    [TestFixture]
    public class EntityIdTests
    {
        [Test]
        public void Comparisons()
        {
            Assert.True(EntityId.Create(EntityKindCore.Player, 123) == EntityId.Create(EntityKindCore.Player, 123));
            Assert.False(EntityId.Create(EntityKindCore.Player, 123) != EntityId.Create(EntityKindCore.Player, 123));
            Assert.False(EntityId.Create(EntityKindCore.Player, 123) == EntityId.Create(EntityKindCloudCore.Connection, 123));
            Assert.True(EntityId.Create(EntityKindCore.Player, 123) != EntityId.Create(EntityKindCloudCore.Connection, 123));
            Assert.False(EntityId.Create(EntityKindCore.Player, 123) == EntityId.Create(EntityKindCore.Player, 300));
            Assert.True(EntityId.Create(EntityKindCore.Player, 123) != EntityId.Create(EntityKindCore.Player, 300));
            Assert.False(EntityId.None == EntityId.Create(EntityKindCore.Player, 300));
            Assert.True(EntityId.None != EntityId.Create(EntityKindCore.Player, 300));
            Assert.True(EntityId.None == EntityId.None);
            Assert.False(EntityId.None != EntityId.None);
        }

        [Test]
        public void IdCharacterOrdinalOrder()
        {
            for (int ndx = 0; ndx < EntityId.NumValidIdCharacters - 1; ndx++)
                Assert.Greater(EntityId.ValidIdCharacters[ndx + 1], EntityId.ValidIdCharacters[ndx]);
        }

        [Test]
        public void ToStringOrdinalOrderSmall()
        {
            for (int iter = 0; iter < 10_000; iter++)
            {
                ulong value = (uint)iter;
                EntityId entityId = EntityId.Create(EntityKindCore.Player, value);
                EntityId nextId = EntityId.Create(EntityKindCore.Player, value + 1);

                Assert.AreNotEqual(nextId.Value, entityId.Value);
                Assert.AreNotEqual(nextId.ToString(), entityId.ToString());
                Assert.Greater(nextId.Value, entityId.Value);
                Assert.Greater(nextId, entityId);
                Assert.GreaterOrEqual(string.CompareOrdinal(nextId.ToString(), entityId.ToString()), 1);
            }
        }

        [Test]
        public void ToStringOrdinalOrderRandomSequential()
        {
            Random rnd = new Random();
            for (int iter = 0; iter < 10_000; iter++)
            {
                ulong value = (((ulong)(uint)rnd.Next() << 32) + (ulong)(uint)rnd.Next()) & EntityId.ValueMask;
                if (value == EntityId.ValueMask)
                    continue;
                EntityId entityId = EntityId.Create(EntityKindCore.Player, value);
                EntityId nextId = EntityId.Create(EntityKindCore.Player, value + 1);

                Assert.AreNotEqual(nextId.Value, entityId.Value);
                Assert.AreNotEqual(nextId.ToString(), entityId.ToString());
                Assert.Greater(nextId.Value, entityId.Value);
                Assert.Greater(nextId, entityId);
                Assert.GreaterOrEqual(string.CompareOrdinal(nextId.ToString(), entityId.ToString()), 1);
            }
        }

        [Test]
        public void ToStringOrdinalOrderRandomPairs()
        {
            Random rnd = new Random();
            for (int iter = 0; iter < 10_000; iter++)
            {
                ulong valueA = (((ulong)(uint)rnd.Next() << 32) + (ulong)(uint)rnd.Next()) & EntityId.ValueMask;
                ulong valueB = (((ulong)(uint)rnd.Next() << 32) + (ulong)(uint)rnd.Next()) & EntityId.ValueMask;
                EntityId entityIdA = EntityId.Create(EntityKindCore.Player, valueA);
                EntityId entityIdB = EntityId.Create(EntityKindCore.Player, valueB);

                int valueCompare = valueA.CompareTo(valueB);
                int untypedCompare = ((IComparable)valueA).CompareTo(valueB);
                int stringCompare = string.CompareOrdinal(entityIdA.ToString(), entityIdB.ToString());

                Assert.AreEqual(Math.Sign(valueCompare), Math.Sign(untypedCompare));
                Assert.AreEqual(Math.Sign(valueCompare), Math.Sign(stringCompare));
            }
        }

        [Test]
        public void OrdinalOrderRandomEntityKinds()
        {
            Random rnd = new Random(123456);
            for (int iter = 0; iter < 10_000; iter++)
            {
                EntityKind entityKindA = EntityKind.FromValue(rnd.Next() % EntityKind.MaxValue);
                EntityKind entityKindB = EntityKind.FromValue(rnd.Next() % EntityKind.MaxValue);
                ulong value = (((ulong)(uint)rnd.Next() << 32) + (ulong)(uint)rnd.Next()) & EntityId.ValueMask;
                EntityId entityIdA = EntityId.Create(entityKindA, entityKindA.Value == 0 ? 0 : value);
                EntityId entityIdB = EntityId.Create(entityKindB, entityKindB.Value == 0 ? 0 : value);

                int entityKindCompare = entityKindA.CompareTo(entityKindB);
                int entityIdCompare = entityIdA.CompareTo(entityIdB);

                Assert.AreEqual(Math.Sign(entityKindCompare), Math.Sign(entityIdCompare));
            }
        }


        [Test]
        public void ParseFromString()
        {
            Assert.AreEqual(EntityId.None, EntityId.ParseFromString("None"));
            Assert.AreEqual(EntityId.Create(EntityKindCore.Player, 0), EntityId.ParseFromString("Player:0000000000"));
            Assert.AreEqual(EntityId.Create(EntityKindCore.Player, 1), EntityId.ParseFromString("Player:0000000002"));
            Assert.AreEqual(EntityId.Create(EntityKindCore.Player, EntityId.ValueMask), EntityId.ParseFromString("Player:ZH0toCzB90"));
        }

        [Test]
        public void ConvertToString()
        {
            Assert.AreEqual("None", EntityId.None.ToString());
            Assert.AreEqual("InvalidNone:0000000002", EntityId.CreateUncheckedTestingOnly(EntityKind.None, 1ul).ToString());
            Assert.AreEqual("Player:0000000000", EntityId.Create(EntityKindCore.Player, 0).ToString());
            Assert.AreEqual("Player:0000000002", EntityId.Create(EntityKindCore.Player, 1).ToString());
            Assert.AreEqual("Player:ZH0toCzB90", EntityId.Create(EntityKindCore.Player, EntityId.ValueMask).ToString());
        }

        [Test]
        public void ParseFromStringInvalid()
        {
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("None:"));
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("None:0000000000")); // None cannot have id
            Assert.Throws<FormatException>(() => EntityId.ParseFromString(":0000000000")); // no kind
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player")); // no value
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player:")); // no value
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player:0a2345678")); // too short
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player:0a23456789a")); // too long
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player:0123456789")); // invalid char
            Assert.Throws<FormatException>(() => EntityId.ParseFromString("Player:ZH0toCzB92")); // too large: 1 << 58
        }

        [Test]
        public void CreateEntityKind()
        {
            Assert.AreEqual(EntityKindCloudCore.Connection, EntityKind.FromName(nameof(EntityKindCloudCore.Connection)));
            Assert.AreEqual(EntityKindCloudCore.DiagnosticTool, EntityKind.FromName(nameof(EntityKindCloudCore.DiagnosticTool)));

            Assert.AreEqual(EntityKindCloudCore.Connection, EntityKind.FromValue(EntityKindCloudCore.Connection.Value));
            Assert.AreEqual(EntityKindCloudCore.DiagnosticTool, EntityKind.FromValue(EntityKindCloudCore.DiagnosticTool.Value));
        }

        [Test]
        public void InvalidEntityKind()
        {
            Assert.Throws<InvalidOperationException>(() => EntityKind.FromName("non-existent name"));
            Assert.DoesNotThrow(() => EntityKind.FromValue(63));
        }

        [Test]
        public void InvalidEntityId()
        {
            // Check that invalid EntityKind in EntityId doesn't throw and returns the invalid EntityKind
            EntityId id = EntityId.Create(new EntityKind(63), 0xffffffff);
            Assert.AreEqual(63, id.Kind.Value);
            Assert.IsFalse(EntityKindRegistry.IsValid(id.Kind));
            Assert.AreEqual("Invalid#63:000070SPSr", id.ToString());
        }

        [MetaSerializable]
        public struct LegacyEntityId
        {
            [MetaMember(1)] public ulong RawValue;

            LegacyEntityId(ulong raw) => RawValue = raw;

            public static LegacyEntityId FromRaw(ulong raw) => new LegacyEntityId(raw);
            public static LegacyEntityId Create(int kind, ulong value) => new LegacyEntityId(((ulong)kind << 58) | value);
        }

        [MetaSerializable]
        public class LegacyContainer
        {
            [MetaMember(1)] public LegacyEntityId EntityId { get; set; }

            LegacyContainer() { }
            public LegacyContainer(LegacyEntityId entityId) { EntityId = entityId; }
        }

        [MetaSerializable]
        public class EntityIdContainer
        {
            [MetaMember(1)] public EntityId EntityId { get; set; }
        }

        [Test]
        public void LegacyFormatDeserialization()
        {
            ulong[] values = [0ul, 1, 1ul << 57, EntityId.ValueMask];
            // Check full legacy EntityKind range
            for (int entityKind = 0; entityKind < 64; entityKind++)
            {
                foreach (ulong value in values)
                {
                    LegacyEntityId src = LegacyEntityId.Create(entityKind, value);
                    byte[] bytes = MetaSerialization.SerializeTagged(new LegacyContainer(src), MetaSerializationFlags.IncludeAll, logicVersion: null);
                    EntityId copy = MetaSerialization.DeserializeTagged<EntityIdContainer>(bytes, MetaSerializationFlags.IncludeAll, resolver: null, logicVersion: null).EntityId;
                    Assert.That(copy.Kind.Value, Is.EqualTo(entityKind));
                    Assert.That(copy.Value, Is.EqualTo(value));
                }
            }
        }
    }
}

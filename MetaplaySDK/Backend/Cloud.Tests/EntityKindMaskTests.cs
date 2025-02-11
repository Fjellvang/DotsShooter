// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cloud.Tests
{
    [TestFixture]
    public class EntityKindMaskTests
    {
        [Test]
        public void Basic()
        {
            // Check empty mask
            EntityKindMask none = EntityKindMask.None;
            Assert.That(none.IsEmpty, Is.True);
            Assert.That(none.GetKinds(), Is.EqualTo(new EntityKind[] { }));

            // Parameterless ctor
            EntityKindMask ctorNone = new EntityKindMask();
            Assert.That(none, Is.EqualTo(ctorNone));

            // default(EntityKindMask)
            EntityKindMask defaultNone = default;
            Assert.That(none, Is.EqualTo(defaultNone));

            // Check single-kind masks
            for (int value = 0; value < EntityKind.MaxValue; value++)
            {
                EntityKind kind = EntityKind.FromValue(value);
                EntityKind[] kinds = new EntityKind[] { kind };
                EntityKindMask single = new EntityKindMask(kinds);
                Assert.That(single.IsEmpty, Is.False);
                Assert.That(single.GetKinds(), Is.EqualTo(kinds));
                Assert.That(single.IsSet(kind), Is.True);
                Assert.That(single == EntityKindMask.FromEntityKind(kind), Is.True);
                Assert.That(single != EntityKindMask.FromEntityKind(kind), Is.False);

                Assert.That(single == none, Is.False);
                Assert.That(single != none, Is.True);
            }

            // Check all (valid kinds)
            EntityKindMask all = EntityKindMask.All;
            Assert.That(all.IsEmpty, Is.False);
            Assert.That(all.GetKinds().Count() >= 5);

            Assert.That(all == none, Is.False);
            Assert.That(all != none, Is.True);
        }

        [Test]
        public void ConvertToString()
        {
            // Check empty mask
            EntityKindMask none = EntityKindMask.None;
            Assert.That(none.ToString(), Is.EqualTo(""));

            // Check single-kind masks
            for (int value = 0; value < EntityKind.MaxValue; value++)
            {
                EntityKind kind = EntityKind.FromValue(value);
                EntityKindMask single = EntityKindMask.FromEntityKind(kind);
                Assert.That(single.ToString(), Is.EqualTo(kind.Name));
            }

            // Check all (valid kinds)
            EntityKindMask all = EntityKindMask.All;
            Assert.That(all.ToString().Length, Is.GreaterThanOrEqualTo(20));
            Assert.That(all.ToString().Split(" | ").Length, Is.GreaterThanOrEqualTo(5));
            EntityKindMask allCopy = new EntityKindMask(all.ToString().Split(" | ").Select(EntityKind.FromName));
            Assert.That(all, Is.EqualTo(allCopy));
        }

        [Test]
        public void SetOperations()
        {
            static IEnumerable<int> IterateBits(int baseOffset, ulong mask)
            {
                for (int offset = 0; offset < 64; offset++)
                {
                    if ((mask & (1ul << offset)) != 0)
                        yield return baseOffset + offset;
                }
            }

            static ulong KindsToMask(int baseOffset, EntityKindMask kindMask)
            {
                ulong bits = 0;
                foreach (EntityKind kind in kindMask.GetKinds())
                {
                    int offset = kind.Value - baseOffset;
                    MetaDebug.Assert(offset >= 0 && offset < 64, "Internal error, offset must fit into an ulong!");
                    bits |= 1ul << offset;
                }
                return bits;
            }

            RandomPCG rng = RandomPCG.CreateFromSeed(98765);
            for (int ndx = 0; ndx < 100; ndx++)
            {
                // Generate two random 64-bit values
                ulong bitsA = rng.NextULong();
                ulong bitsB = rng.NextULong();

                // Shift the values by random offset into an EntityKindMask (to test full range)
                int baseOffset = rng.NextInt() % (EntityKind.MaxValue - 64 + 1);
                EntityKindMask maskA = new EntityKindMask(IterateBits(baseOffset, bitsA).Select(EntityKind.FromValue));
                EntityKindMask maskB = new EntityKindMask(IterateBits(baseOffset, bitsB).Select(EntityKind.FromValue));

                // Compute set operations for the bits
                ulong bitsIntersect = bitsA & bitsB;
                ulong bitsUnion = bitsA | bitsB;
                ulong bitsExcept = bitsA & ~bitsB;

                // Compute set operations for the shifted EntityKindMasks
                EntityKindMask maskIntersect = maskA & maskB;
                EntityKindMask maskUnion = maskA | maskB;
                EntityKindMask maskExcept = EntityKindMask.Except(maskA, maskB);

                // Shift bits back to ulong range for comparison against the bitwise operation results
                Assert.That(KindsToMask(baseOffset, maskIntersect), Is.EqualTo(bitsIntersect));
                Assert.That(KindsToMask(baseOffset, maskUnion), Is.EqualTo(bitsUnion));
                Assert.That(KindsToMask(baseOffset, maskExcept), Is.EqualTo(bitsExcept));
            }
        }
    }
}

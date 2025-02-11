// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using NUnit.Framework;
using System;

namespace Cloud.Tests
{
    [TestFixture]
    public class JournalPositionTests
    {
        [Test]
        public void TestComparison()
        {
            RandomPCG rnd = RandomPCG.CreateFromSeed(1);

            for (int i = 0; i < 1000; i++)
            {
                // \note Using small domains for the random numbers so that we're likely to actually get some equal JournalPositions.
                (long Tick, int Operation, int Step) tuple0 = (rnd.NextInt(3), rnd.NextInt(3), rnd.NextInt(3));
                (long Tick, int Operation, int Step) tuple1 = (rnd.NextInt(3), rnd.NextInt(3), rnd.NextInt(3));

                JournalPosition pos0 = JournalPosition.FromTickOperationStep(tuple0.Tick, tuple0.Operation, tuple0.Step);
                JournalPosition pos1 = JournalPosition.FromTickOperationStep(tuple1.Tick, tuple1.Operation, tuple1.Step);

                Assert.AreEqual(tuple0.Equals(tuple1), pos0.Equals(pos1));
                Assert.AreEqual(tuple0.Equals(tuple1), ((object)pos0).Equals(pos1));

                Assert.AreEqual(tuple0 == tuple1, pos0 == pos1);
                Assert.AreEqual(tuple0 != tuple1, pos0 != pos1);
                Assert.AreEqual(tuple0.CompareTo(tuple1) < 0, pos0 < pos1);
                Assert.AreEqual(tuple0.CompareTo(tuple1) <= 0, pos0 <= pos1);
                Assert.AreEqual(tuple0.CompareTo(tuple1) > 0, pos0 > pos1);
                Assert.AreEqual(tuple0.CompareTo(tuple1) >= 0, pos0 >= pos1);

                Assert.AreEqual(Math.Sign(tuple0.CompareTo(tuple1)), Math.Sign(pos0.CompareTo(pos1)));

                if (pos0 == pos1)
                    Assert.True(pos0.GetHashCode() == pos1.GetHashCode());
                else
                {
                    // \note Not guaranteed, but likely enough for these test cases, and works as a sanity check.
                    //       Remove/tweak if this ever becomes a problem.
                    Assert.False(pos0.GetHashCode() == pos1.GetHashCode());
                }
            }
        }
    }
}

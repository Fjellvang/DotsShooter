// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using NUnit.Framework;
using System.Collections.Generic;

namespace Cloud.Tests
{
    public class ShardingStrategyTests
    {
        [Test]
        public void TestValidManualShardingStrategyEntityIds()
        {
            RandomPCG rng = RandomPCG.CreateNew();
            ManualShardingStrategy strategy = new ManualShardingStrategy();
            List<int> nodeSetIndices = new List<int>();
            List<int> nodeIndices = new List<int>();
            List<ulong> runningIds = new List<ulong>();

            for (int ndx = 0; ndx < 5; ++ndx)
                nodeSetIndices.Add(ndx);
            for (int ndx = 0; ndx < 30; ++ndx)
                nodeSetIndices.Add(rng.NextInt(ManualShardingStrategy.MaxNodeSetIndex + 1));
            nodeSetIndices.Add(ManualShardingStrategy.MaxNodeSetIndex);

            for (int ndx = 0; ndx < 5; ++ndx)
                nodeIndices.Add(ndx);
            for (int ndx = 0; ndx < 30; ++ndx)
                nodeIndices.Add(rng.NextInt(ManualShardingStrategy.MaxNodeIndex + 1));
            nodeIndices.Add(ManualShardingStrategy.MaxNodeIndex);

            for (int ndx = 0; ndx < 5; ++ndx)
                runningIds.Add((ulong)ndx);
            for (int ndx = 0; ndx < 30; ++ndx)
                runningIds.Add(rng.NextULong() % (ManualShardingStrategy.MaxValue + 1));
            runningIds.Add(ManualShardingStrategy.MaxValue);

            foreach (int nodeSetIndex in nodeSetIndices)
            {
                foreach (int nodeIndex in nodeIndices)
                {
                    foreach (ulong runningId in runningIds)
                    {
                        EntityId entityId = ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex, nodeIndex), runningId);
                        EntityShardId shardId = strategy.ResolveShardId(entityId);
                        Assert.AreEqual(new EntityShardId(EntityKindCore.Player, nodeSetIndex, nodeIndex), shardId);
                    }
                }
            }
        }

        [Test]
        public void TestInvalidManualShardingStrategyEntityIds()
        {
            Assert.Catch(() => ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: -1, nodeIndex: 0), runningId: 0));
            Assert.Catch(() => ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: ManualShardingStrategy.MaxNodeSetIndex + 1, nodeIndex: 0), runningId: 0));

            Assert.Catch(() => ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: 0, nodeIndex: -1), runningId: 0));
            Assert.Catch(() => ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: 0, nodeIndex: ManualShardingStrategy.MaxNodeIndex + 1), runningId: 0));

            Assert.Catch(() => ManualShardingStrategy.CreateEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: 0, nodeIndex: 0), runningId: ManualShardingStrategy.MaxValue + 1));
        }

        [Test]
        public void TestManualShardingStrategyLimits()
        {
            Assert.AreEqual(ManualShardingStrategy.MaxNodeSetIndex, EntityShardId.MaxNodeSetIndex);
            Assert.AreEqual(ManualShardingStrategy.MaxNodeIndex, EntityShardId.MaxNodeIndex);
        }

        [Test]
        public void TestValidDynamicServiceShardingStrategy()
        {
            RandomPCG rng = RandomPCG.CreateNew();
            DynamicServiceShardingStrategy strategy = new DynamicServiceShardingStrategy();
            List<int> nodeSetIndices = new List<int>();
            List<int> nodeIndices = new List<int>();

            for (int ndx = 0; ndx < 5; ++ndx)
                nodeSetIndices.Add(ndx);
            for (int ndx = 0; ndx < 30; ++ndx)
                nodeSetIndices.Add(rng.NextInt(DynamicServiceShardingStrategy.MaxNodeSetIndex + 1));
            nodeSetIndices.Add(DynamicServiceShardingStrategy.MaxNodeSetIndex);

            for (int ndx = 0; ndx < 5; ++ndx)
                nodeIndices.Add(ndx);
            for (int ndx = 0; ndx < 30; ++ndx)
            {
                // DynamicServiceShardingStrategy.MaxNodeIndex might be over int limit. Detect that at runtime to
                // support changes in future.
                ulong originalIndex = rng.NextULong() % (DynamicServiceShardingStrategy.MaxNodeIndex + 1);
                if (originalIndex <= (ulong)int.MaxValue)
                    nodeIndices.Add((int)originalIndex);
                else
                {
                    // Use whole int range.
                    nodeIndices.Add(rng.NextInt());
                }
            }

            // Maximal end
            if (DynamicServiceShardingStrategy.MaxNodeIndex > (ulong)int.MaxValue)
                nodeIndices.Add(int.MaxValue);
            else
                nodeIndices.Add((int)DynamicServiceShardingStrategy.MaxNodeIndex);

            foreach (int nodeSetIndex in nodeSetIndices)
            {
                foreach (int nodeIndex in nodeIndices)
                {
                    EntityId entityId = DynamicServiceShardingStrategy.CreatePlacedEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex, nodeIndex));
                    EntityShardId shardId = strategy.ResolveShardId(entityId);
                    Assert.AreEqual(new EntityShardId(EntityKindCore.Player, nodeSetIndex, nodeIndex), shardId);
                }
            }
        }

        [Test]
        public void TestInvalidDynamicServiceShardingStrategyEntityIds()
        {
            Assert.Catch(() => DynamicServiceShardingStrategy.CreatePlacedEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: -1, nodeIndex: 0)));
            Assert.Catch(() => DynamicServiceShardingStrategy.CreatePlacedEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: DynamicServiceShardingStrategy.MaxNodeSetIndex + 1, nodeIndex: 0)));

            Assert.Catch(() => DynamicServiceShardingStrategy.CreatePlacedEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: 0, nodeIndex: -1)));

            if (DynamicServiceShardingStrategy.MaxNodeIndex + 1 <= (ulong)int.MaxValue)
                Assert.Catch(() => DynamicServiceShardingStrategy.CreatePlacedEntityId(new EntityShardId(EntityKindCore.Player, nodeSetIndex: 0, nodeIndex: (int)(DynamicServiceShardingStrategy.MaxNodeIndex + 1))));
        }

        [Test]
        public void TestDynamicServiceShardingStrategyLimits()
        {
            Assert.AreEqual(DynamicServiceShardingStrategy.MaxNodeSetIndex, EntityShardId.MaxNodeSetIndex);
            Assert.AreEqual(DynamicServiceShardingStrategy.MaxNodeIndex, EntityShardId.MaxNodeIndex);
        }
    }
}

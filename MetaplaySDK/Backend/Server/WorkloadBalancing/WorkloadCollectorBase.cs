// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Cluster;
using Metaplay.Core;
using Metaplay.Core.Model;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Metaplay.Server.WorkloadBalancing
{
    public class WorkloadCollectorBase : IMetaIntegrationSingleton<WorkloadCollectorBase>
    {
        /// <summary>
        /// This is called by the <see cref="WorkloadTrackingServiceActor"/> to collect the workload of each node.
        /// Collecting the workload is done in a high cadence to ensure that up-to-date information is available.
        /// Therefore, it is important that the execution of this method is fast,
        /// if you need to wait a longer time to collect data, it should be cached and only returned here.
        /// </summary>
        /// <returns>An implementation of your own workload, or null if node is not available for loadbalancing</returns>
        public virtual Task<WorkloadBase> Collect()
        {
            return Task.FromResult<WorkloadBase>(null);
        }
    }

    /// <summary>
    /// Inheriting from this class lets you specify you own workload for the workload tracking system.
    /// We recommend that you use <see cref="WorkloadCounter"/> and <see cref="WorkloadGauge"/> as it is important to keep the data size small.
    /// </summary>
    [MetaSerializable]
    public abstract class WorkloadBase : IComparable<WorkloadBase>
    {
        public abstract int CompareTo(WorkloadBase other);

        /// <summary>
        /// Determines whether the workload is within the soft limit, if false, we will stop trying to schedule work on this
        /// node until it is either no longer outside the soft limit, or until all nodes are outside the soft limit.
        /// You can use this in combination with <see cref="IsWithinHardLimit"/> to create a limit when the optimal load of a node has been reached,
        /// while allowing to stretch if demand is high (with e.g. degraded performance)
        /// </summary>
        public virtual bool IsWithinSoftLimit(NodeSetConfig nodeSetConfig, ClusterNodeAddress nodeAddress)
        {
            return true;
        }

        /// <summary>
        /// Determines whether the workload is within the hard limit, this is a hard stop to stop allocating work on this node when using <see cref="WorkloadSchedulingUtility"/>.
        /// You can use this in combination with <see cref="IsWithinSoftLimit"/> to create a limit when the optimal load of a node has been reached,
        /// while allowing to stretch if demand is high (with e.g. degraded performance)
        /// </summary>
        public virtual bool IsWithinHardLimit(NodeSetConfig nodeSetConfig, ClusterNodeAddress nodeAddress)
        {
            return true;
        }

        /// <summary>
        /// Determines whether a node is currently doing work, this is used to determine whether we should scale down,
        /// or whether a node is ready to be shutdown. You can return <c>true</c> for nodes that still have work ongoing.
        /// However, keep in mind that those entities will be shutdown, which you will have to handle gracefully.
        /// </summary>
        public abstract bool IsEmpty();
    }

    [MetaSerializable]
    public struct WorkloadCounter : IComparable<WorkloadCounter>
    {
        [MetaMember(1), JsonIgnore]
        volatile int _counter;
        [MetaMember(2), JsonIgnore]
        volatile int _decrCounter;

        public int Counter     => _counter;
        public int DecrCounter => _decrCounter;

        public int Value => _counter - _decrCounter;

        public void Increment()
        {
            Interlocked.Increment(ref _counter);
        }

        public void Decrement()
        {
            Interlocked.Increment(ref _decrCounter);
        }

        public int CompareTo(WorkloadCounter other)
        {
            return Value.CompareTo(other.Value);
        }
    }

    [MetaSerializable]
    public struct WorkloadGauge : IComparable<WorkloadGauge>
    {
        [MetaMember(1), JsonIgnore]
        volatile int _value;

        public int Value => _value;

        public void Update(int value)
        {
            Interlocked.Exchange(ref _value, value);
        }

        public int CompareTo(WorkloadGauge other)
        {
            return _value.CompareTo(other._value);
        }
    }
}

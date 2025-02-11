// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Persistence
{
    /// <summary>
    /// Throttles database operations before accessing to underlying database. The common use-case
    /// is to limit the number of concurrent DB operation.
    /// </summary>
    public interface IDatabaseThrottle : IDisposable
    {
        public Task<IDisposable> LockAsync(DatabaseReplica replica, int shardNdx);
    }

    public class DatabaseThrottleNop : IDatabaseThrottle
    {
        public static DatabaseThrottleNop Instance { get; } = new DatabaseThrottleNop();
        static readonly Task<IDisposable> Completed = Task.FromResult<IDisposable>(null);

        public Task<IDisposable> LockAsync(DatabaseReplica replica, int shardNdx)
        {
            return Completed;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Helper for managing per-shard data in <see cref="IDatabaseThrottle"/>.
    /// </summary>
    public class DatabaseThrottlePerShardState<TState> : IDisposable
        where TState : IDisposable
    {
        readonly TState[] _states;
        readonly int _numShards;

        public DatabaseThrottlePerShardState(int numShards, Func<DatabaseReplica, int, TState> stateInitializer)
        {
            _numShards = numShards;
            _states = new TState[2 * numShards];
            for (int ndx = 0; ndx < numShards; ndx++)
                _states[ndx] = stateInitializer(DatabaseReplica.ReadWrite, ndx);
            for (int ndx = 0; ndx < numShards; ndx++)
                _states[ndx+numShards] = stateInitializer(DatabaseReplica.ReadOnly, ndx);
        }

        public TState GetState(DatabaseReplica replica, int shardNdx)
        {
            if (replica == DatabaseReplica.ReadWrite)
                return _states[shardNdx];
            else
                return _states[shardNdx + _numShards];
        }

        public void Dispose()
        {
            foreach (TState state in _states)
                state?.Dispose();
        }
    }
}

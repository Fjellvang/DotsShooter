// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Metaplay.Server.StatisticsEvents
{
    /// <summary>
    /// Locking mechanism for providing monotonically increasing timestamps such
    /// that the user can perform database writes using the timestamps with a
    /// acquire-write-release pattern. While the write lock is being held, the
    /// <see cref="GetSafeWatermarkTime"/> does not advance behind the oldest lock,
    /// so that we know all outstanding writes will not be using older timestamps
    /// than that.
    ///
    /// This class is fully thread-safe!
    /// </summary>
    static class StatisticsEventClock
    {
        /// <summary> Lock for the rest of the data to get thread-safety. </summary>
        static object           _lock               = new object();
        /// <summary> Latest timestamp that has been given to a lock or that can be given to a lock. </summary>
        static MetaTime         _latestTimestamp    = MetaTime.Now;
        /// <summary> List of active locks. Note: Using a list is inefficient with lots of locks but the number should be limited in practice. </summary>
        static List<MetaTime>   _lockedTimestamps   = new List<MetaTime>();

        /// <summary>
        /// Acquire a timestamp lock. Return the locked timestamp. The lock must
        /// be released with <see cref="ReleaseTimestampLock(MetaTime)"/>
        /// </summary>
        /// <returns></returns>
        public static MetaTime AcquireTimestampLock()
        {
            lock (_lock)
            {
                // Acquire a time, ensuring it cannot go backward
                MetaTime timestamp = MetaTime.Max(MetaTime.Now, _latestTimestamp);

                // Lock the value.
                _lockedTimestamps.Add(timestamp);

                return timestamp;
            }
        }

        /// <summary>
        /// Release a given timestamp lock.
        /// </summary>
        /// <param name="timestamp"></param>
        public static void ReleaseTimestampLock(MetaTime timestamp)
        {
            lock (_lock)
            {
                _lockedTimestamps.Remove(timestamp);
            }
        }

        /// <summary>
        /// Return the current safe time. The safe time is either the earliest lock
        /// timestamp if any exist, or otherwise, the current monotonic wallclock time.
        /// Note: The returned safe time is always smaller than any active lock (current
        /// or future).
        /// </summary>
        /// <returns></returns>
        public static MetaTime GetSafeWatermarkTime()
        {
            MetaTime timestamp;
            int lockedTimestampsBefore = 0;
            int lockedTimestampsAfter = 0;

            lock (_lock)
            {
                // If we have any timestamp locks, return the oldest one
                if (_lockedTimestamps.Count > 0)
                {
                    // To be safe, delete all events older than 10sec (and collect counts)
                    // \todo The 10sec limit is rather short -- consider some kind of detection for anomalous
                    //       situations that applies a global delay of a few minutes or something like that.
                    lockedTimestampsBefore = _lockedTimestamps.Count;
                    MetaTime filterThreshold = MetaTime.Now - MetaDuration.FromSeconds(10);
                    _lockedTimestamps.RemoveAll(ts => ts < filterThreshold);
                    lockedTimestampsAfter = _lockedTimestamps.Count;

                    if (_lockedTimestamps.Count == 0)
                    {
                        // If we have no locks left, move the latest timestamp to current time
                        _latestTimestamp = MetaTime.Now;
                        timestamp = MetaTime.Now - MetaDuration.FromMilliseconds(1);
                    }
                    else
                    {
                        // Return the oldest lock time, minus epsilon (to ensure write timestamps are always larger than watermark)
                        MetaTime oldestLock = _lockedTimestamps.Min();
                        timestamp = oldestLock - MetaDuration.FromMilliseconds(1);
                    }
                }
                else
                {
                    // Otherwise, move the latest timestamp (for write locks) to current time
                    _latestTimestamp = MetaTime.Now;

                    // And return the timestamp minus epsilon (to ensure write timestamps are always larger than watermark)
                    timestamp = _latestTimestamp - MetaDuration.FromMilliseconds(1);
                }
            }

            // If too many locks or if locks were purged, warn about it
            if (lockedTimestampsBefore >= 100 || lockedTimestampsBefore != lockedTimestampsAfter)
            {
                if (lockedTimestampsBefore != lockedTimestampsAfter)
                    Serilog.Log.Warning("StatisticsEventClock: Locks were purged due to timeout (before={LocksBefore}, after={LocksAfter})", lockedTimestampsBefore, lockedTimestampsAfter);
                else
                    Serilog.Log.Warning("StatisticsEventClock: Too many active locks simultaneously (before={LocksBefore}, after={LocksAfter})", lockedTimestampsBefore, lockedTimestampsAfter);
            }

            return timestamp;
        }
    }
}
